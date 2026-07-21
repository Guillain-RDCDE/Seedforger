using System;
using System.Collections.Generic;
using System.Threading;
using Seedforger.Web;

namespace Seedforger.Cli {

  /// <summary>
  /// The daemon: owns a set of <see cref="SeedEngine"/> instances (one per torrent),
  /// runs them 24/7, and serves a live web dashboard over <see cref="WebDashboard"/>.
  /// Ideal on a seedbox or NAS — start it and watch/stop it from a browser. It holds
  /// the process open until Ctrl+C, a duration elapses, or the page hits /api/stop.
  /// </summary>
  internal sealed class DaemonHost {

    private readonly List<SeedEngine> engines = new List<SeedEngine>();
    private readonly Action<string> log;
    private readonly ManualResetEventSlim stopSignal = new ManualResetEventSlim(false);
    private WebDashboard web;
    private DateTime startedUtc;

    public DaemonHost(Action<string> log) { this.log = log; }

    public int Count => engines.Count;
    public void Add(SeedEngine engine) { if (engine != null) engines.Add(engine); }

    public void Start(string bind, int port) {
      startedUtc = DateTime.UtcNow;
      foreach (var e in engines) e.Start();
      web = new WebDashboard(bind, port, BuildStatusJson, SignalStop, log);
      web.Start();
      log?.Invoke($"daemon: {engines.Count} torrent(s) online — open {web.Url} in a browser.");
    }

    /// <summary>Blocks until stop is requested, or (when minutes &gt; 0) that many
    /// minutes elapse. A Ctrl+C handler and the dashboard both call SignalStop.</summary>
    public void WaitForStop(int minutes) {
      if (minutes > 0) stopSignal.Wait(TimeSpan.FromMinutes(minutes));
      else stopSignal.Wait();
    }

    public void SignalStop() => stopSignal.Set();

    public void Stop() {
      try { web?.Stop(); } catch { }
      foreach (var e in engines) { try { e.Stop(); } catch { } }
    }

    private string BuildStatusJson() {
      var rows = new List<StatusSnapshot>(engines.Count);
      foreach (var e in engines)
        rows.Add(new StatusSnapshot {
          Name = e.TorrentName,
          Client = e.ClientName,
          Uploaded = Math.Max(0, e.UploadedBytes),
          Downloaded = Math.Max(0, e.DownloadedBytes),
          Ratio = e.Ratio,
          Seeders = e.SeederCount,
          Leechers = e.LeecherCount,
          Interval = e.IntervalSeconds,
          Running = e.IsRunning,
          Trackers = e.TrackerCount,
          RealSeed = e.RealSeedEnabled,
        });

      var report = new StatusReport {
        Version = AppInfo.Version,
        UptimeSeconds = (long) (DateTime.UtcNow - startedUtc).TotalSeconds,
        Torrents = rows,
      };
      return StatusJson.Serialize(report);
    }
  }
}
