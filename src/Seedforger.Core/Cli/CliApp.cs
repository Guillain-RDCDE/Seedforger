using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Seedforger.BitTorrent;
using Seedforger.Net;

namespace Seedforger.Cli {

  /// <summary>
  /// The headless command line, shared by the console executable and the Windows
  /// GUI executable (which switches to it when started with these options). Four
  /// commands — help, list clients, dry-run announce, seed (one torrent or a
  /// daemon over a folder) — on top of the same <see cref="SeedEngine"/> as the
  /// windows. Writes to the streams it is given, so it is testable.
  /// </summary>
  internal static class CliApp {

    /// <summary>Exit codes.</summary>
    internal const int Ok = 0, Failed = 1, Usage = 2;

    private static readonly string[] KnownOptions = {
      "--help", "-h", "-?", "/?", "--list-clients", "--cli", "--headless", "--nogui",
      "--test-announce", "--dry-run", "--daemon", "--torrent", "-t", "--folder", "--magnet",
      "--web-port", "--web-bind", "--client", "--client-version", "--randomize-client",
      "--upload", "-u", "--download", "-d", "--seed", "--leech", "--finished", "--serve-real",
      "--connection", "--interval", "--port", "--stop-after", "--stop-uploaded", "--stop-ratio",
      "--realistic", "--swarm-aware", "--proxy-type", "--proxy-host", "--proxy-port",
      "--proxy-user", "--proxy-pass", "--duration", "--quiet", "-q",
    };

    /// <summary>True when the arguments ask for the command line rather than a window.</summary>
    internal static bool IsCliInvocation(string[] args) =>
      new CommandLine(args).Has("--help", "-h", "-?", "/?", "--list-clients", "--cli", "--headless", "--nogui",
                                "--test-announce", "--dry-run", "--daemon", "--folder");

    internal static int Run(string[] args, TextWriter stdout, TextWriter stderr) {
      Encodings.Register();
      var opt = new CommandLine(args);

      if (opt.IsEmpty || opt.Has("--help", "-h", "-?", "/?")) { PrintHelp(stdout); return Ok; }
      if (opt.Has("--list-clients")) { PrintClients(stdout); return Ok; }

      var unknown = opt.Unknown(KnownOptions);
      if (unknown.Count > 0) {
        stderr.WriteLine("error: unknown option(s): " + string.Join(", ", unknown));
        stderr.WriteLine("       run with --help for the full list of options.");
        return Usage;
      }

      var torrentPath = opt.Value("--torrent", "-t");
      var folder = opt.Value("--folder");
      var daemon = opt.Has("--daemon") || !string.IsNullOrEmpty(folder);
      if (opt.Value("--magnet") != null && string.IsNullOrEmpty(torrentPath) && string.IsNullOrEmpty(folder)) {
        stderr.WriteLine("error: --magnet isn't supported headless (magnets carry no size). Use --torrent.");
        return Usage;
      }
      if (string.IsNullOrEmpty(torrentPath) && string.IsNullOrEmpty(folder)) {
        stderr.WriteLine("error: give a torrent with --torrent <file.torrent>, or a folder with --folder <dir>.");
        stderr.WriteLine("       run with --help for the full list of options.");
        return Usage;
      }
      if (!string.IsNullOrEmpty(torrentPath)) {
        torrentPath = Path.GetFullPath(torrentPath);
        if (!File.Exists(torrentPath)) { stderr.WriteLine("error: torrent not found: " + torrentPath); return Usage; }
      }

      var quiet = opt.Has("--quiet", "-q");
      Action<string> log = quiet ? _ => { } : stdout.WriteLine;
      SecureDns.Log = quiet ? null : stdout.WriteLine;

      var stealth = new StealthOptions {
        RealisticSpeed = opt.Bool(true, "--realistic"),
        SwarmAware = opt.Bool(true, "--swarm-aware"),
      };
      var budget = new UpstreamBudget();
      var profileName = opt.Value("--connection");
      var profile = profileName == null ? null : ConnectionProfiles.Find(profileName);
      if (profileName != null && profile == null)
        stderr.WriteLine($"warning: unknown connection profile \"{profileName}\" (see --help); using explicit speeds.");
      if (profile != null) budget.TotalKBps = profile.UpKBps;

      var family = opt.Value("--client") ?? TorrentClientFactory.DefaultFamily;
      var version = opt.Value("--client-version");
      var randomize = opt.Has("--randomize-client");
      var finished = opt.Has("--leech") ? 0 : Math.Clamp(opt.Int(100, "--finished"), 0, 100);
      var upload = opt.Int(profile?.UpKBps ?? 0, "--upload", "-u");
      var download = finished >= 100 ? 0 : opt.Int(profile?.DownKBps ?? 0, "--download", "-d");
      var proxy = BuildProxy(opt);

      SeedOptions Options(Torrent t, Random rand) => new SeedOptions {
        Torrent = t,
        Client = randomize ? TorrentClientFactory.PickRandomModern(rand) : TorrentClientFactory.Resolve(family, version),
        Proxy = proxy,
        UploadKBps = upload,
        DownloadKBps = download,
        FinishedPercent = finished,
        AnnounceIntervalSeconds = opt.Int(0, "--interval"),
        Port = opt.Int(0, "--port"),
        StopWhen = StopRuleFrom(opt),
        Stealth = stealth,
        Budget = budget,
        Log = log,
      };

      if (daemon) return RunDaemon(opt, torrentPath, folder, Options, stdout, stderr, log);

      Torrent torrent;
      try { torrent = new Torrent(torrentPath); }
      catch (Exception ex) { stderr.WriteLine("error: couldn't read that .torrent: " + ex.Message); return Usage; }

      if (opt.Has("--test-announce", "--dry-run")) return DryRun(torrent, Options(torrent, new Random()), stdout, stderr, log);
      return Seed(torrent, Options(torrent, new Random()), opt, stdout);
    }

    // ---- commands ----

    private static int DryRun(Torrent torrent, SeedOptions o, TextWriter stdout, TextWriter stderr, Action<string> log) {
      stdout.WriteLine($"Dry run: announcing \"{torrent.Name}\" once as a complete seeder ({o.Client.Name})…");
      var probe = AnnounceProbe.Run(torrent, o.Client, o.Proxy, log);
      if (probe.Accepted) {
        stdout.WriteLine("accepted as a seeder.");
        stdout.WriteLine($"  seeders (complete):    {probe.Seeders}");
        stdout.WriteLine($"  leechers (incomplete): {probe.Leechers}");
        stdout.WriteLine($"  announce interval:     {probe.Interval}s");
        if (probe.Leechers == 0) stdout.WriteLine("  note: nobody is downloading this torrent, so there is nobody to upload to.");
        return Ok;
      }
      if (!string.IsNullOrEmpty(probe.FailureReason)) stderr.WriteLine("the tracker rejected the announce: " + probe.FailureReason);
      else stderr.WriteLine("no usable answer from the tracker: " + (probe.Error ?? "see the log above"));
      return Failed;
    }

    private static int Seed(Torrent torrent, SeedOptions o, CommandLine opt, TextWriter stdout) {
      var real = opt.Value("--serve-real");
      if (!string.IsNullOrEmpty(real)) o.RealFile = Path.GetFullPath(real);
      using var engine = new SeedEngine(o);

      stdout.WriteLine($"Seeding \"{torrent.Name}\" as {o.Client.Name}" + (o.UploadKBps > 0 ? $" at ~{o.UploadKBps} kB/s" : "")
                       + (o.StopWhen.IsSet ? $", stopping {o.StopWhen}" : "") + ".");
      engine.Start();

      var minutes = opt.Int(0, "--duration");
      using var stop = new ManualResetEventSlim(false);
      engine.AutoStopped += (e, reason) => stop.Set();
      Console.CancelKeyPress += (s, e) => { e.Cancel = true; stop.Set(); };
      if (minutes > 0) {
        stdout.WriteLine($"Will stop automatically after {minutes} minute(s). Press Ctrl+C to stop sooner.");
        stop.Wait(TimeSpan.FromMinutes(minutes));
      }
      else {
        stdout.WriteLine("Running until Ctrl+C.");
        stop.Wait();
      }

      stdout.WriteLine("Stopping…");
      engine.Stop();
      stdout.WriteLine($"Done. Reported {Format.Bytes(engine.UploadedBytes)} uploaded in {Format.Duration(engine.Elapsed)}.");
      return Ok;
    }

    /// <summary>Runs one torrent or a whole folder headless behind a live web
    /// dashboard, until Ctrl+C, a duration, or the page's stop button.</summary>
    private static int RunDaemon(CommandLine opt, string torrentPath, string folder, Func<Torrent, Random, SeedOptions> options,
                                 TextWriter stdout, TextWriter stderr, Action<string> log) {
      var paths = new List<string>();
      if (!string.IsNullOrEmpty(folder)) {
        var dir = Path.GetFullPath(folder);
        if (!Directory.Exists(dir)) { stderr.WriteLine("error: folder not found: " + dir); return Usage; }
        paths.AddRange(Directory.GetFiles(dir, "*.torrent"));
        if (paths.Count == 0) { stderr.WriteLine("error: no .torrent files in " + dir); return Usage; }
      }
      else {
        paths.Add(torrentPath);
      }

      var realSeed = opt.Value("--serve-real"); // a single file, or a folder matched by name
      var rand = new Random();
      using var host = new DaemonHost(log);
      foreach (var p in paths) {
        Torrent t;
        try { t = new Torrent(p); }
        catch (Exception ex) { log("skipping " + Path.GetFileName(p) + ": " + ex.Message); continue; }
        var o = options(t, rand);
        if (!string.IsNullOrEmpty(realSeed)) {
          if (Directory.Exists(realSeed)) {
            var cand = Path.Combine(realSeed, t.Name);
            if (File.Exists(cand)) o.RealFile = Path.GetFullPath(cand);
          }
          else if (File.Exists(realSeed) && paths.Count == 1) o.RealFile = Path.GetFullPath(realSeed);
        }
        host.Add(new SeedEngine(o));
      }
      if (host.Count == 0) { stderr.WriteLine("error: no usable torrents to seed."); return Usage; }

      var bind = opt.Value("--web-bind") ?? "127.0.0.1";
      var port = opt.Int(8080, "--web-port");
      var stopping = 0;
      Console.CancelKeyPress += (s, e) => { e.Cancel = true; if (Interlocked.Exchange(ref stopping, 1) == 0) host.SignalStop(); };

      try { host.Start(bind, port); }
      catch (Exception ex) {
        stderr.WriteLine($"error: couldn't start the dashboard on {bind}:{port} — {ex.Message}");
        if (bind != "127.0.0.1") stderr.WriteLine("       binding to a non-loopback address may need admin rights / a urlacl on Windows.");
        host.Stop();
        return Failed;
      }

      stdout.WriteLine($"Daemon: {host.Count} torrent(s) online. Dashboard → {host.DashboardUrl}");
      stdout.WriteLine("Press Ctrl+C to stop (or use the dashboard's Stop button).");
      host.WaitForStop(opt.Int(0, "--duration"));

      stdout.WriteLine("Stopping daemon…");
      host.Stop();
      stdout.WriteLine("Daemon stopped.");
      return Ok;
    }

    // ---- option helpers ----

    private static StopRule StopRuleFrom(CommandLine opt) {
      if (opt.Value("--stop-after") != null) return StopRule.After(StopKind.AfterMinutes, opt.Double(0, "--stop-after"));
      if (opt.Value("--stop-uploaded") != null) return StopRule.After(StopKind.UploadedMB, opt.Double(0, "--stop-uploaded"));
      if (opt.Value("--stop-ratio") != null) return StopRule.After(StopKind.RatioAbove, opt.Double(0, "--stop-ratio"));
      return StopRule.Never;
    }

    internal static ProxySettings BuildProxy(CommandLine opt) => ProxySettings.From(
      ProxySettings.ParseType(opt.Value("--proxy-type")),
      opt.Value("--proxy-host") ?? "",
      opt.Int(0, "--proxy-port"),
      opt.Value("--proxy-user") ?? "",
      opt.Value("--proxy-pass") ?? "");

    private static void PrintClients(TextWriter w) {
      w.WriteLine("Available clients to impersonate (--client / --client-version):\n");
      foreach (var family in TorrentClientFactory.GetFamilies()) {
        var versions = TorrentClientFactory.GetVersions(family);
        w.WriteLine("  " + family);
        if (versions.Count > 0) w.WriteLine("      " + string.Join(", ", versions));
      }
      w.WriteLine("\nConnection profiles (--connection):\n");
      foreach (var p in ConnectionProfiles.All) w.WriteLine($"  \"{p.Name}\"  ({p.UpKBps} kB/s up, {p.DownKBps} kB/s down)");
    }

    private static void PrintHelp(TextWriter w) {
      w.WriteLine($@"{AppInfo.Name} v{AppInfo.Version} — report torrent stats without moving bytes (headless, cross-platform).

USAGE
  Seedforger.Cli --test-announce -t movie.torrent
  Seedforger.Cli -t movie.torrent -u 800 --duration 120
  Seedforger.Cli --folder ~/torrents --connection ""Fibre  100 / 100 Mbps"" --web-port 8080

TORRENT (required)
  --torrent, -t <file>         Path to a .torrent file.
  --folder <dir>               Seed every .torrent in a folder (implies --daemon).

MODES
  --test-announce, --dry-run   Announce once as a seeder, report accepted/rejected and the swarm, exit.
  --daemon                     Run 24/7 behind a live web dashboard (seedbox/NAS).
  --list-clients               List every client/version and connection profile.
  --help, -h                   Show this help.

DAEMON / WEB DASHBOARD
  --web-port <n>               Dashboard port (default 8080).
  --web-bind <addr>            Bind address (default 127.0.0.1; 0.0.0.0 for LAN).

IMPERSONATE
  --client <name>              e.g. qBittorrent, Transmission (default qBittorrent).
  --client-version <ver>       e.g. 5.2.4 (defaults to the newest known).
  --randomize-client           Pick a random current client fingerprint instead.
  --port <n>                   Announced listening port (default: random, like a real client).

SPEED & MODE
  --upload, -u <kB/s>          Reported upload speed.
  --download, -d <kB/s>        Reported download speed (leecher mode).
  --connection <profile>       A believable line (sets upload/download and the shared budget).
  --leech                      Leecher: finished 0 %.
  --finished <0-100>           Explicit finished percentage (default 100 = seeder).
  --interval <sec>             Base announce interval (the tracker's own always wins).
  --serve-real <file|dir>      Serve genuine hash-valid pieces of the real file(s)
                               (defeats a tracker's monitoring peers).

STOP BY ITSELF
  --stop-after <minutes>       …after that long.
  --stop-uploaded <MB>         …once that much upload was reported.
  --stop-ratio <ratio>         …once uploaded ÷ downloaded reaches it.
  --duration <minutes>         Hard limit for the whole run (0 = until Ctrl+C).

BELIEVABILITY
  --realistic on|off           Ramp-up + smooth variation (default on).
  --swarm-aware on|off         Scale to real demand (default on).

PROXY
  --proxy-type none|http|socks4|socks4a|socks5
  --proxy-host <h>  --proxy-port <p>  --proxy-user <u>  --proxy-pass <p>

OUTPUT
  --quiet, -q                  Suppress the per-announce log.

Educational / security-research tool. Faking ratio breaks most private trackers'
rules and can get you banned — use it only where you're allowed to.");
    }
  }
}
