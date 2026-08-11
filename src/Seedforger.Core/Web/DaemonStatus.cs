using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Seedforger.Web {

  /// <summary>One torrent's live figures as shown on the dashboard / JSON API.
  /// Plain data, WinForms-free, trivially serialisable and testable.</summary>
  internal sealed class StatusSnapshot {
    public string Name { get; set; } = "";
    public string Client { get; set; } = "";
    public long Uploaded { get; set; }
    public long Downloaded { get; set; }
    public double Ratio { get; set; }
    public int Seeders { get; set; } = -1;
    public int Leechers { get; set; } = -1;
    public int Interval { get; set; }
    public bool Running { get; set; }
    public int Trackers { get; set; } = 1;
    public bool RealSeed { get; set; }
  }

  /// <summary>The whole daemon snapshot: app identity, uptime, per-torrent rows and
  /// the aggregate totals. Building the totals here (not in the view) keeps the
  /// arithmetic in one testable place.</summary>
  internal sealed class StatusReport {
    public string App { get; set; } = "Seedforger";
    public string Version { get; set; } = "";
    public long UptimeSeconds { get; set; }
    public IReadOnlyList<StatusSnapshot> Torrents { get; set; } = Array.Empty<StatusSnapshot>();

    public int Count => Torrents.Count;
    public int RunningCount { get { var n = 0; foreach (var t in Torrents) if (t.Running) n++; return n; } }
    public long TotalUploaded { get { long s = 0; foreach (var t in Torrents) s += t.Uploaded; return s; } }
    public long TotalDownloaded { get { long s = 0; foreach (var t in Torrents) s += t.Downloaded; return s; } }
    public double TotalRatio => TotalDownloaded > 0 ? (double) TotalUploaded / TotalDownloaded : 0;
  }

  /// <summary>Serialises a <see cref="StatusReport"/> to the exact JSON the dashboard
  /// polls. Kept separate from the HTTP server so it can be unit-tested.</summary>
  internal static class StatusJson {
    private static readonly JsonSerializerOptions Opts = new JsonSerializerOptions { WriteIndented = false };

    public static string Serialize(StatusReport r) {
      var rows = new List<object>(r.Torrents.Count);
      foreach (var t in r.Torrents)
        rows.Add(new {
          name = t.Name, client = t.Client,
          uploaded = t.Uploaded, downloaded = t.Downloaded, ratio = Round(t.Ratio),
          seeders = t.Seeders, leechers = t.Leechers, interval = t.Interval,
          running = t.Running, trackers = t.Trackers, realSeed = t.RealSeed,
        });

      var payload = new {
        app = r.App,
        version = r.Version,
        uptimeSeconds = r.UptimeSeconds,
        totals = new {
          uploaded = r.TotalUploaded, downloaded = r.TotalDownloaded,
          ratio = Round(r.TotalRatio), running = r.RunningCount, count = r.Count,
        },
        torrents = rows,
      };
      return JsonSerializer.Serialize(payload, Opts);
    }

    private static double Round(double v) => Math.Round(v, 3, MidpointRounding.AwayFromZero);
  }
}
