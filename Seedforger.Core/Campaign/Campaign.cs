using System;
using System.IO;
using System.Text.Json;

namespace Seedforger {

  /// <summary>
  /// A declarative goal-directed plan: you state the intent (a ratio/upload goal,
  /// a believability profile, a folder of torrents) and the orchestrator derives
  /// the actions over time — staggered starts, demand-driven budget allocation,
  /// pacing toward a deadline. Serialized to campaign.json.
  /// </summary>
  internal sealed class Campaign {

    // --- Goal ---
    /// <summary>"ratio" or "upload".</summary>
    public string Goal { get; set; } = "upload";
    public double TargetRatio { get; set; } = 2.0;
    public double UploadGoalGB { get; set; } = 100;
    /// <summary>Spread the goal over this many hours (0 = as fast as credible).</summary>
    public double DeadlineHours { get; set; } = 336; // ~2 weeks

    // --- Believability profile ---
    /// <summary>Name of a connection profile (see ConnectionProfiles) — sets the
    /// total upstream budget shared across torrents.</summary>
    public string Connection { get; set; } = "Fibre  300 / 300 Mbps";
    public bool UseActiveHours { get; set; } = true;
    public int ActiveHoursStart { get; set; } = 8;
    public int ActiveHoursEnd { get; set; } = 24;
    public bool RotateClient { get; set; } = true;

    // --- Torrents ---
    /// <summary>Folder scanned for *.torrent files.</summary>
    public string TorrentFolder { get; set; } = "";
    /// <summary>Optional folder with the matching downloaded files, to seed for real
    /// (matched by torrent name). Empty = announce-only.</summary>
    public string RealFileFolder { get; set; } = "";

    // --- Pacing / staggering ---
    public int StaggerMinMinutes { get; set; } = 3;
    public int StaggerMaxMinutes { get; set; } = 40;
    /// <summary>How many torrents may run at once (0 = no limit).</summary>
    public int MaxConcurrent { get; set; } = 6;

    private static readonly JsonSerializerOptions Opts = new JsonSerializerOptions {
      WriteIndented = true, PropertyNameCaseInsensitive = true,
    };

    internal static Campaign Load(string path) {
      try {
        if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize<Campaign>(File.ReadAllText(path), Opts);
      }
      catch { return null; }
    }

    internal void Save(string path) {
      try { File.WriteAllText(path, JsonSerializer.Serialize(this, Opts)); }
      catch { /* best effort */ }
    }

    /// <summary>Writes a documented sample campaign next to the exe (once).</summary>
    internal static void ExportSampleIfMissing() {
      try {
        var path = Path.Combine(AppContext.BaseDirectory, "campaign.sample.json");
        if (File.Exists(path)) return;
        new Campaign {
          Goal = "upload", UploadGoalGB = 200, DeadlineHours = 336,
          Connection = "Fibre  300 / 300 Mbps", UseActiveHours = true,
          ActiveHoursStart = 8, ActiveHoursEnd = 24, RotateClient = true,
          TorrentFolder = @"C:\torrents", RealFileFolder = "",
          StaggerMinMinutes = 3, StaggerMaxMinutes = 40, MaxConcurrent = 6,
        }.Save(path);
      }
      catch { /* ignore */ }
    }
  }
}
