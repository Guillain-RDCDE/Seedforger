using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Seedforger;
using Seedforger.BitTorrent;
using Seedforger.BytesRoads;

namespace Seedforger.Cli {

  /// <summary>
  /// The cross-platform headless entry point (Windows / Linux / macOS). Drives the
  /// WinForms-free <see cref="SeedEngine"/> over the same proven announce core as
  /// the GUI. Ideal for a seedbox, a server or CI.
  /// </summary>
  internal static class Program {

    internal static int Main(string[] rawArgs) {
      Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
      var opt = new Options(rawArgs);

      if (opt.Has("--help", "-h", "-?", "/?") || rawArgs.Length == 0) { PrintHelp(); return 0; }
      if (opt.Has("--list-clients")) { PrintClients(); return 0; }

      var dryRun = opt.Has("--test-announce", "--dry-run");
      var torrentPath = opt.Value("--torrent", "-t");
      var magnet = opt.Value("--magnet");
      var folder = opt.Value("--folder");
      var daemon = opt.Has("--daemon") || !string.IsNullOrEmpty(folder);

      if (string.IsNullOrEmpty(torrentPath) && string.IsNullOrEmpty(magnet) && string.IsNullOrEmpty(folder)) {
        Console.Error.WriteLine("error: give a torrent with --torrent <file.torrent>, or a folder with --folder <dir>.");
        Console.Error.WriteLine("       run with --help for the full list of options.");
        return 2;
      }
      if (!string.IsNullOrEmpty(magnet) && string.IsNullOrEmpty(torrentPath) && string.IsNullOrEmpty(folder)) {
        Console.Error.WriteLine("error: --magnet isn't supported headless (magnets carry no size). Use --torrent.");
        return 2;
      }
      if (!string.IsNullOrEmpty(torrentPath)) {
        torrentPath = Path.GetFullPath(torrentPath);
        if (!File.Exists(torrentPath)) { Console.Error.WriteLine("error: torrent not found: " + torrentPath); return 2; }
      }

      // Believability toggles.
      AppOptions.RealisticSpeed = opt.Bool("--realistic", AppOptions.RealisticSpeed);
      AppOptions.SwarmAware = opt.Bool("--swarm-aware", AppOptions.SwarmAware);
      if (opt.Has("--randomize-client")) AppOptions.RandomizeClientOnStart = true;

      // In daemon mode the DaemonHost loads its own torrents (one or a whole
      // folder), so we don't need a single Torrent here.
      Torrent torrent = null;
      if (!daemon) {
        try { torrent = new Torrent(torrentPath); }
        catch (Exception ex) { Console.Error.WriteLine("error: couldn't read that .torrent: " + ex.Message); return 2; }
      }

      // Impersonated client.
      var family = opt.Value("--client") ?? "qBittorrent";
      var version = opt.Value("--client-version", "--version");
      if (string.IsNullOrEmpty(version)) {
        var vers = TorrentClientFactory.GetVersions(family);
        version = vers != null && vers.Count > 0 ? vers[0] : "";
      }
      var client = TorrentClientFactory.GetClient((family + " " + version).Trim());

      // Proxy (optional).
      var proxy = BuildProxy(opt);

      // Mode + speeds.
      var finished = opt.Has("--leech") ? 0 : opt.Int("--finished", 100);
      var upload = opt.Int("--upload", 0, "-u");
      var download = finished >= 100 ? 0 : opt.Int("--download", 0, "-d");

      Action<string> log = opt.Has("--quiet", "-q") ? _ => { } : Console.WriteLine;
      SecureDns.Log = Console.WriteLine;

      // Daemon: run one torrent or a whole folder 24/7 behind a live web dashboard.
      if (daemon) return RunDaemon(opt, torrentPath, folder, family, version, upload, download, finished, proxy, log);

      if (dryRun) {
        Console.WriteLine("Dry-run: announcing once as a seeder…");
        var engine0 = new SeedEngine(torrent, client, proxy, upload, download, finished) { Log = log };
        engine0.Start();
        Thread.Sleep(200);
        engine0.Stop();
        if (engine0.SeederCount >= 0) {
          Console.WriteLine("accepted as a seeder.");
          Console.WriteLine($"  seeders (complete):    {engine0.SeederCount}");
          Console.WriteLine($"  leechers (incomplete): {engine0.LeecherCount}");
          Console.WriteLine($"  announce interval:     {engine0.IntervalSeconds}s");
          return 0;
        }
        Console.Error.WriteLine("the tracker did not accept the announce (see the log above).");
        return 1;
      }

      var engine = new SeedEngine(torrent, client, proxy, upload, download, finished) { Log = log };
      var real = opt.Value("--serve-real");
      if (!string.IsNullOrEmpty(real)) engine.EnableRealSeed(System.IO.Path.GetFullPath(real));
      Console.WriteLine($"Seeding \"{torrent.Name}\" as {family} {version}" + (upload > 0 ? $" at ~{upload} kB/s" : "") + ".");
      engine.Start();

      var minutes = opt.Int("--duration", 0);
      var stop = new ManualResetEventSlim(false);
      Console.CancelKeyPress += (s, e) => { e.Cancel = true; stop.Set(); };
      if (minutes > 0) {
        Console.WriteLine($"Will stop automatically after {minutes} minute(s). Press Ctrl+C to stop sooner.");
        stop.Wait(TimeSpan.FromMinutes(minutes));
      }
      else {
        Console.WriteLine("Running until Ctrl+C.");
        stop.Wait();
      }

      Console.WriteLine("Stopping…");
      engine.Stop();
      Console.WriteLine($"Done. Reported {FormatSize(Math.Max(0, engine.UploadedBytes))} uploaded.");
      return 0;
    }

    /// <summary>Runs one torrent or a whole folder headless behind a live web
    /// dashboard, until Ctrl+C, a duration, or the page's stop button.</summary>
    private static int RunDaemon(Options opt, string torrentPath, string folder, string family, string version,
                                 int upload, int download, int finished, ProxyInfo proxy, Action<string> log) {
      var paths = new List<string>();
      if (!string.IsNullOrEmpty(folder)) {
        var dir = Path.GetFullPath(folder);
        if (!Directory.Exists(dir)) { Console.Error.WriteLine("error: folder not found: " + dir); return 2; }
        paths.AddRange(Directory.GetFiles(dir, "*.torrent"));
        if (paths.Count == 0) { Console.Error.WriteLine("error: no .torrent files in " + dir); return 2; }
      }
      else if (!string.IsNullOrEmpty(torrentPath)) {
        paths.Add(torrentPath);
      }
      else {
        Console.Error.WriteLine("error: daemon mode needs --torrent <file> or --folder <dir>.");
        return 2;
      }

      var realSeed = opt.Value("--serve-real"); // a single file, or a folder to match by name
      var randomize = opt.Has("--randomize-client") || AppOptions.RandomizeClientOnStart;
      var host = new DaemonHost(log);
      var rand = new Random();

      foreach (var p in paths) {
        Torrent t;
        try { t = new Torrent(p); }
        catch (Exception ex) { log("skipping " + Path.GetFileName(p) + ": " + ex.Message); continue; }
        var client = PickClient(family, version, randomize, rand);
        var engine = new SeedEngine(t, client, proxy, upload, download, finished) { Log = log };

        if (!string.IsNullOrEmpty(realSeed)) {
          string file = null;
          if (Directory.Exists(realSeed)) {
            var cand = Path.Combine(realSeed, t.Name);
            if (File.Exists(cand)) file = cand;
          }
          else if (File.Exists(realSeed) && paths.Count == 1) file = realSeed;
          if (file != null) engine.EnableRealSeed(Path.GetFullPath(file));
        }
        host.Add(engine);
      }

      if (host.Count == 0) { Console.Error.WriteLine("error: no usable torrents to seed."); return 2; }

      var bind = opt.Value("--web-bind") ?? "127.0.0.1";
      var port = opt.Int("--web-port", 8080);

      var stopping = false;
      Console.CancelKeyPress += (s, e) => { e.Cancel = true; if (!stopping) { stopping = true; host.SignalStop(); } };

      try { host.Start(bind, port); }
      catch (Exception ex) {
        Console.Error.WriteLine($"error: couldn't start the dashboard on {bind}:{port} — {ex.Message}");
        if (bind != "127.0.0.1")
          Console.Error.WriteLine("       binding to a non-loopback address may need admin rights / a urlacl on Windows.");
        host.Stop();
        return 1;
      }

      Console.WriteLine($"Daemon: {host.Count} torrent(s) online. Dashboard → http://{(bind == "0.0.0.0" ? "127.0.0.1" : bind)}:{port}/");
      Console.WriteLine("Press Ctrl+C to stop (or use the dashboard's Stop button).");
      var minutes = opt.Int("--duration", 0);
      host.WaitForStop(minutes);

      Console.WriteLine("Stopping daemon…");
      host.Stop();
      Console.WriteLine("Daemon stopped.");
      return 0;
    }

    private static TorrentClient PickClient(string family, string version, bool randomize, Random rand) {
      if (randomize) {
        var pool = TorrentClientFactory.ModernClients;
        if (pool != null && pool.Length > 0)
          return TorrentClientFactory.GetClient(pool[rand.Next(pool.Length)]);
      }
      if (string.IsNullOrEmpty(version)) {
        var vers = TorrentClientFactory.GetVersions(family);
        version = vers != null && vers.Count > 0 ? vers[0] : "";
      }
      return TorrentClientFactory.GetClient((family + " " + version).Trim());
    }

    private static string FormatSize(long bytes) {
      string[] u = { "bytes", "KB", "MB", "GB", "TB" };
      double v = bytes; var i = 0;
      while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
      return (i == 0 ? bytes.ToString() : v.ToString("0.00")) + " " + u[i];
    }

    private static ProxyInfo BuildProxy(Options opt) {
      var type = (opt.Value("--proxy-type") ?? "none").Trim().ToLowerInvariant();
      var pt = type switch {
        "http" or "httpconnect" or "https" => ProxyType.HttpConnect,
        "socks4" => ProxyType.Socks4,
        "socks4a" => ProxyType.Socks4a,
        "socks5" => ProxyType.Socks5,
        _ => ProxyType.None,
      };
      var enc = Encoding.GetEncoding(0x4e4);
      return new ProxyInfo {
        ProxyType = pt,
        ProxyServer = opt.Value("--proxy-host") ?? "",
        ProxyPort = opt.Int("--proxy-port", 0),
        ProxyUser = enc.GetBytes(opt.Value("--proxy-user") ?? ""),
        ProxyPassword = enc.GetBytes(opt.Value("--proxy-pass") ?? ""),
      };
    }

    private static void PrintClients() {
      Console.WriteLine("Available clients to impersonate (--client / --client-version):\n");
      foreach (var family in TorrentClientFactory.GetFamilies()) {
        var versions = TorrentClientFactory.GetVersions(family);
        Console.WriteLine("  " + family);
        if (versions != null && versions.Count > 0) Console.WriteLine("      " + string.Join(", ", versions));
      }
    }

    private static void PrintHelp() {
      Console.WriteLine($@"{AppInfo.Name} v{AppInfo.Version} — report torrent stats without moving bytes (headless, cross-platform).

USAGE
  Seedforger.Cli --test-announce -t movie.torrent
  Seedforger.Cli -t movie.torrent -u 800 --duration 120

TORRENT (required)
  --torrent, -t <file>         Path to a .torrent file.
  --folder <dir>               Seed every .torrent in a folder (implies --daemon).

MODES
  --test-announce, --dry-run   Announce once as a seeder, print the result, exit.
  --daemon                     Run 24/7 behind a live web dashboard (seedbox/NAS).
  --list-clients               List every client/version you can impersonate.
  --help, -h                   Show this help.

DAEMON / WEB DASHBOARD
  --web-port <n>               Dashboard port (default 8080).
  --web-bind <addr>            Bind address (default 127.0.0.1; 0.0.0.0 for LAN).

IMPERSONATE
  --client <name>              e.g. qBittorrent, Transmission (default qBittorrent).
  --client-version <ver>       e.g. 5.2.3 (defaults to the newest known).
  --randomize-client           Pick a random modern client fingerprint.

SPEED & MODE
  --upload, -u <kB/s>          Reported upload speed.
  --download, -d <kB/s>        Reported download speed (leecher mode).
  --leech                      Leecher: Finished 0%.
  --finished <0-100>           Explicit finished percentage (default 100 = seeder).
  --serve-real <file>          Serve genuine hash-valid pieces of a real file
                               (defeats a tracker's monitoring peers).

BELIEVABILITY
  --realistic on|off           Ramp-up + smooth variation (default on).
  --swarm-aware on|off         Scale to real demand (default on).

PROXY
  --proxy-type none|http|socks4|socks4a|socks5
  --proxy-host <h>  --proxy-port <p>  --proxy-user <u>  --proxy-pass <p>

RUN
  --duration <minutes>         Stop automatically after N minutes (0 = until Ctrl+C).
  --quiet, -q                  Suppress the per-announce log.

Educational / security-research tool. Faking ratio breaks most private trackers'
rules and can get you banned — use it only where you're allowed to.");
    }

    /// <summary>Flags and "--key value" pairs.</summary>
    private sealed class Options {
      private readonly string[] a;
      internal Options(string[] args) { a = args ?? Array.Empty<string>(); }

      internal bool Has(params string[] names) {
        foreach (var n in a) foreach (var name in names)
          if (string.Equals(n, name, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
      }

      internal string Value(params string[] names) {
        for (var i = 0; i < a.Length - 1; i++)
          foreach (var name in names)
            if (string.Equals(a[i], name, StringComparison.OrdinalIgnoreCase)) return a[i + 1];
        return null;
      }

      internal int Int(string name, int fallback, string alias = null) {
        var v = alias != null ? Value(name, alias) : Value(name);
        return int.TryParse(v, out var n) ? n : fallback;
      }

      internal bool Bool(string name, bool fallback) {
        var v = Value(name);
        if (v == null) return fallback;
        v = v.Trim().ToLowerInvariant();
        return v == "on" || v == "true" || v == "1" || v == "yes";
      }
    }
  }
}
