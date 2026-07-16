using System;
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
      if (string.IsNullOrEmpty(torrentPath) && string.IsNullOrEmpty(magnet)) {
        Console.Error.WriteLine("error: give a torrent with --torrent <file.torrent> (--magnet needs a size, GUI only).");
        Console.Error.WriteLine("       run with --help for the full list of options.");
        return 2;
      }
      if (string.IsNullOrEmpty(torrentPath)) {
        Console.Error.WriteLine("error: --magnet isn't supported headless (magnets carry no size). Use --torrent.");
        return 2;
      }
      torrentPath = Path.GetFullPath(torrentPath);
      if (!File.Exists(torrentPath)) { Console.Error.WriteLine("error: torrent not found: " + torrentPath); return 2; }

      // Believability toggles.
      AppOptions.RealisticSpeed = opt.Bool("--realistic", AppOptions.RealisticSpeed);
      AppOptions.SwarmAware = opt.Bool("--swarm-aware", AppOptions.SwarmAware);
      if (opt.Has("--randomize-client")) AppOptions.RandomizeClientOnStart = true;

      Torrent torrent;
      try { torrent = new Torrent(torrentPath); }
      catch (Exception ex) { Console.Error.WriteLine("error: couldn't read that .torrent: " + ex.Message); return 2; }

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

MODES
  --test-announce, --dry-run   Announce once as a seeder, print the result, exit.
  --list-clients               List every client/version you can impersonate.
  --help, -h                   Show this help.

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
