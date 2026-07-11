using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Seedforger {

  /// <summary>
  /// Headless / scriptable entry point. Lets an automation drive the same proven
  /// announce engine (<see cref="RM"/>) from the command line, with no window —
  /// load a torrent, impersonate a client, seed at a chosen speed for a duration,
  /// or just dry-run an announce. All output goes to the parent console.
  /// </summary>
  internal static class Cli {

    // ---- console plumbing (a WinExe has no console of its own) ----
    [DllImport("kernel32.dll")] private static extern bool AttachConsole(int pid);
    [DllImport("kernel32.dll")] private static extern bool AllocConsole();
    private const int AttachParent = -1;

    /// <summary>True when the arguments ask for the command-line rather than the GUI.</summary>
    internal static bool IsCliInvocation(string[] args) {
      foreach (var a in args) {
        switch (a.ToLowerInvariant()) {
          case "--help": case "-h": case "-?": case "/?":
          case "--list-clients":
          case "--cli": case "--headless": case "--nogui":
          case "--test-announce": case "--dry-run":
            return true;
        }
      }
      return false;
    }

    internal static int Run(string[] args) {
      EnsureConsole();
      var opt = new Options(args);

      if (opt.Has("--help", "-h", "-?", "/?")) { PrintHelp(); return 0; }
      if (opt.Has("--list-clients")) { PrintClients(); return 0; }

      var dryRun = opt.Has("--test-announce", "--dry-run");

      var torrent = opt.Value("--torrent", "-t");
      var magnet = opt.Value("--magnet");
      if (string.IsNullOrEmpty(torrent) && string.IsNullOrEmpty(magnet)) {
        Console.Error.WriteLine("error: give a torrent with --torrent <file.torrent> or --magnet <uri>.");
        Console.Error.WriteLine("       run with --help for the full list of options.");
        return 2;
      }
      if (!string.IsNullOrEmpty(torrent)) {
        torrent = Path.GetFullPath(torrent);
        if (!File.Exists(torrent)) { Console.Error.WriteLine("error: torrent not found: " + torrent); return 2; }
      }

      // Global believability toggles (default to the safe, realistic ones).
      AppOptions.RealisticSpeed = opt.Bool("--realistic", AppOptions.RealisticSpeed);
      AppOptions.SwarmAware = opt.Bool("--swarm-aware", AppOptions.SwarmAware);
      if (opt.Has("--randomize-client")) AppOptions.RandomizeClientOnStart = true;

      var host = new HeadlessHost();
      var engine = host.Engine;

      var exitCode = 0;
      Exception fatal = null;

      // Wire console logging before anything is announced.
      engine.LogLineAdded += (s, line) => { if (!opt.Has("--quiet", "-q")) Console.WriteLine(line); };
      SecureDns.Log = m => Console.WriteLine(m);

      host.Ready += () => {
        try {
          // 1) Load the torrent / magnet.
          if (!string.IsNullOrEmpty(torrent)) engine.LoadTorrentFileInfo(torrent);
          else engine.LoadMagnet(magnet);

          // 2) Impersonate a client.
          var family = opt.Value("--client");
          var version = opt.Value("--client-version", "--version");
          if (!string.IsNullOrEmpty(family)) {
            if (string.IsNullOrEmpty(version)) {
              var vers = TorrentClientFactory.GetVersions(family);
              if (vers != null && vers.Count > 0) version = vers[0];
            }
            engine.SetClientSelection(family, version);
          }

          // 3) Proxy (optional).
          var proxyType = opt.Value("--proxy-type");
          if (!string.IsNullOrEmpty(proxyType))
            engine.ConfigureProxy(proxyType, opt.Value("--proxy-host"),
              opt.Int("--proxy-port", 0), opt.Value("--proxy-user"), opt.Value("--proxy-pass"));

          // 4) Connection profile (sets up/down caps + global budget), then explicit speeds win.
          var profile = opt.Value("--connection");
          if (!string.IsNullOrEmpty(profile)) ApplyProfile(engine, profile);

          // 5) Mode + speeds.
          var finished = opt.Has("--leech") ? 0 : opt.Int("--finished", 100);
          engine.SetFinishedPercent(finished);
          if (finished >= 100) engine.SetDownloadKBps(0);
          else if (opt.Has("--download", "-d")) engine.SetDownloadKBps(opt.Int("--download", 0, "-d"));
          if (opt.Has("--upload", "-u")) engine.SetUploadKBps(opt.Int("--upload", 0, "-u"));
          if (opt.Has("--interval")) engine.SetAnnounceIntervalSeconds(opt.Int("--interval", 0));

          if (dryRun) {
            Console.WriteLine("Dry-run: announcing once as a seeder (nothing is faked, nothing is sent)…");
            engine.ProbeAsSeeder(p => { PrintProbe(p); host.CloseHost(); });
            host.ArmTimeout(TimeSpan.FromSeconds(30), () => {
              Console.Error.WriteLine("error: tracker did not answer within 30s.");
              exitCode = 1; host.CloseHost();
            });
            return;
          }

          // 6) Seed.
          Console.WriteLine($"Seeding \"{engine.TorrentDisplayName}\" as {(string.IsNullOrEmpty(family) ? "the selected client" : family)}" +
            (opt.Has("--upload", "-u") ? $" at ~{opt.Int("--upload", 0, "-u")} kB/s" : "") + ".");
          engine.CampaignStart();

          var minutes = opt.Int("--duration", 0);
          if (minutes > 0) {
            Console.WriteLine($"Will stop automatically after {minutes} minute(s). Press Ctrl+C to stop sooner.");
            host.ArmTimeout(TimeSpan.FromMinutes(minutes), () => {
              Console.WriteLine("Duration reached — stopping.");
              engine.CampaignStop(); host.CloseHost();
            });
          }
          else {
            Console.WriteLine("Running until Ctrl+C.");
          }
        }
        catch (Exception ex) { fatal = ex; host.CloseHost(); }
      };

      // Ctrl+C: stop cleanly.
      Console.CancelKeyPress += (s, e) => {
        e.Cancel = true;
        try { host.BeginInvokeSafe(() => { engine.CampaignStop(); host.CloseHost(); }); } catch { }
      };

      Application.Run(host);

      if (fatal != null) { Console.Error.WriteLine("error: " + fatal.Message); return 1; }
      return exitCode;
    }

    // ---- helpers ----

    private static void ApplyProfile(RM engine, string name) {
      foreach (var p in ConnectionProfiles.All)
        if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) {
          engine.SetUploadKBps(p.UpKBps);
          engine.SetDownloadKBps(p.DownKBps);
          Bandwidth.GlobalUpKBps = p.UpKBps;
          return;
        }
      Console.Error.WriteLine($"warning: unknown connection profile \"{name}\" (see --help).");
    }

    private static void PrintProbe(AnnounceProbe p) {
      if (p == null) { Console.Error.WriteLine("no response."); return; }
      if (!string.IsNullOrEmpty(p.Error)) { Console.Error.WriteLine("tracker error: " + p.Error); return; }
      if (!string.IsNullOrEmpty(p.FailureReason)) { Console.Error.WriteLine("rejected: " + p.FailureReason); return; }
      Console.WriteLine("accepted as a seeder.");
      Console.WriteLine($"  seeders (complete):   {p.Seeders}");
      Console.WriteLine($"  leechers (incomplete): {p.Leechers}");
      if (p.Interval > 0) Console.WriteLine($"  announce interval:    {p.Interval}s ({p.Interval / 60} min)");
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
      Console.WriteLine($@"{AppInfo.Name} v{AppInfo.Version} — report torrent stats without moving bytes.

USAGE
  Seedforger.exe                       Launch the classic GUI.
  Seedforger.exe --new                 Launch the new interface.
  Seedforger.exe --cli --torrent F …   Seed headless (no window).
  Seedforger.exe --test-announce -t F  Dry-run one announce and exit.

MODES
  --cli, --headless, --nogui   Run without a window (automation).
  --test-announce, --dry-run   Announce once as a seeder, print the result, exit.
  --list-clients               List every client/version you can impersonate.
  --help, -h                   Show this help.

TORRENT (one required in --cli / dry-run)
  --torrent, -t <file>         Path to a .torrent file.
  --magnet <uri>               A magnet link instead.

IMPERSONATE
  --client <name>              e.g. qBittorrent, Transmission, µTorrent.
  --client-version <ver>       e.g. 5.2.3 (defaults to the newest known).
  --randomize-client           Pick a random modern client on start.

SPEED & MODE
  --upload, -u <kB/s>          Reported upload speed.
  --download, -d <kB/s>        Reported download speed (leecher mode).
  --seed                       Seeder: Finished 100%, download forced to 0 (default).
  --leech                      Leecher: Finished 0%.
  --finished <0-100>           Explicit finished percentage.
  --connection <profile>       Apply a connection profile's caps (see below).
  --interval <sec>             Base announce interval (tracker may override).

BELIEVABILITY
  --realistic on|off           Ramp-up + smooth variation (default on).
  --swarm-aware on|off         Scale speed to real demand (default on).

PROXY
  --proxy-type none|http|socks4|socks4a|socks5
  --proxy-host <h>  --proxy-port <p>  --proxy-user <u>  --proxy-pass <p>

RUN
  --duration <minutes>         Stop automatically after N minutes (0 = until Ctrl+C).
  --quiet, -q                  Suppress the per-announce log.

EXAMPLES
  Seedforger.exe --test-announce -t movie.torrent --client qBittorrent
  Seedforger.exe --cli -t movie.torrent -u 800 --duration 120
  Seedforger.exe --cli -t movie.torrent --connection ""VDSL2 (100/40)"" --randomize-client

Connection profiles:");
      foreach (var p in ConnectionProfiles.All) Console.WriteLine($"  {p.Name}  (up {p.UpKBps} / down {p.DownKBps} kB/s)");
      Console.WriteLine();
      Console.WriteLine("Educational / security-research tool. Faking ratio breaks most private");
      Console.WriteLine("trackers' rules and can get you banned — use it only where you're allowed to.");
    }

    private static void EnsureConsole() {
      if (!AttachConsole(AttachParent)) AllocConsole();
      try {
        var stdout = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
        Console.SetOut(stdout);
        var stderr = new StreamWriter(Console.OpenStandardError()) { AutoFlush = true };
        Console.SetError(stderr);
      }
      catch { /* console already usable */ }
    }

    /// <summary>Parsed command-line options: flags and "--key value" pairs.</summary>
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

      /// <summary>Reads --flag on|off|true|false; returns fallback when absent.</summary>
      internal bool Bool(string name, bool fallback) {
        var v = Value(name);
        if (v == null) return fallback;
        v = v.Trim().ToLowerInvariant();
        return v == "on" || v == "true" || v == "1" || v == "yes";
      }
    }

    /// <summary>An invisible form that keeps the engine's handle and timers alive
    /// while a message loop runs, with no window ever shown.</summary>
    private sealed class HeadlessHost : Form {
      internal readonly RM Engine = new RM();
      internal event Action Ready;
      private readonly Timer timeout = new Timer();
      private Action onTimeout;

      internal HeadlessHost() {
        ShowInTaskbar = false; FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual; Location = new Point(-32000, -32000);
        Size = new Size(1, 1); Opacity = 0;
        var panel = new Panel { Size = new Size(1, 1) };
        Engine.Dock = DockStyle.None; Engine.Location = new Point(0, 0);
        panel.Controls.Add(Engine);
        Controls.Add(panel);
        // Force handle creation so control marshalling and the announce timers work
        // even though the form is never displayed.
        _ = Handle; _ = panel.Handle; _ = Engine.Handle;
        timeout.Tick += (s, e) => { timeout.Stop(); onTimeout?.Invoke(); };
      }

      // Never actually paint a window on screen.
      protected override void SetVisibleCore(bool value) => base.SetVisibleCore(false);

      protected override void OnShown(EventArgs e) { base.OnShown(e); }

      protected override void OnHandleCreated(EventArgs e) {
        base.OnHandleCreated(e);
        BeginInvoke((Action) (() => Ready?.Invoke()));
      }

      internal void ArmTimeout(TimeSpan after, Action action) {
        onTimeout = action;
        timeout.Interval = Math.Max(1, (int) after.TotalMilliseconds);
        timeout.Start();
      }

      internal void BeginInvokeSafe(Action action) {
        if (IsHandleCreated) BeginInvoke(action); else action();
      }

      internal void CloseHost() {
        try { timeout.Stop(); } catch { }
        BeginInvokeSafe(Close);
      }
    }
  }
}
