using System;
using System.Globalization;

namespace Seedforger {

  /// <summary>What ends a run on its own.</summary>
  public enum StopKind {
    Never,
    /// <summary>After <see cref="StopRule.Value"/> minutes.</summary>
    AfterMinutes,
    /// <summary>Once the reported upload passes <see cref="StopRule.Value"/> MB.</summary>
    UploadedMB,
    /// <summary>Once the reported download passes <see cref="StopRule.Value"/> MB.</summary>
    DownloadedMB,
    /// <summary>Once uploaded ÷ downloaded passes <see cref="StopRule.Value"/>.</summary>
    RatioAbove,
    /// <summary>When the tracker reports fewer than <see cref="StopRule.Value"/> seeders.</summary>
    SeedersBelow,
    /// <summary>When the tracker reports fewer than <see cref="StopRule.Value"/> leechers.</summary>
    LeechersBelow,
  }

  /// <summary>
  /// An automatic stop condition, evaluated by the engine once a second. Pure:
  /// <see cref="Evaluate"/> takes the numbers and returns the reason to stop, or
  /// null to keep going, so the rules are unit-tested without a tracker.
  /// </summary>
  internal sealed class StopRule {

    public static readonly StopRule Never = new StopRule();

    public StopKind Kind { get; set; } = StopKind.Never;
    public double Value { get; set; }

    public bool IsSet => Kind != StopKind.Never;

    public static StopRule After(StopKind kind, double value) => new StopRule { Kind = kind, Value = value };

    /// <summary>Returns a human-readable reason when the run should stop now, else null.
    /// Swarm counts of -1 mean "unknown yet" and never trigger a stop.</summary>
    internal string Evaluate(TimeSpan elapsed, long uploaded, long downloaded, int seeders, int leechers) {
      const double mb = 1024.0 * 1024.0;
      switch (Kind) {
        case StopKind.AfterMinutes:
          return elapsed.TotalMinutes >= Value ? $"ran for {Value:0.#} minute(s)" : null;
        case StopKind.UploadedMB:
          return uploaded / mb >= Value ? $"uploaded {Format.Bytes(uploaded)} (target {Value:0.#} MB)" : null;
        case StopKind.DownloadedMB:
          return downloaded / mb >= Value ? $"downloaded {Format.Bytes(downloaded)} (target {Value:0.#} MB)" : null;
        case StopKind.RatioAbove:
          return downloaded > 0 && (double) uploaded / downloaded >= Value ? $"ratio reached {Format.Ratio(uploaded, downloaded)}" : null;
        case StopKind.SeedersBelow:
          return seeders >= 0 && seeders < Value ? $"only {seeders} seeder(s) left" : null;
        case StopKind.LeechersBelow:
          return leechers >= 0 && leechers < Value ? $"only {leechers} leecher(s) left" : null;
        default:
          return null;
      }
    }

    /// <summary>Parses the names used in settings files and on the command line
    /// (case-insensitive: never, minutes/time, uploaded, downloaded, ratio,
    /// seeders, leechers).</summary>
    public static StopRule Parse(string kind, string value) {
      double.TryParse((value ?? "").Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v);
      switch ((kind ?? "").Trim().ToLowerInvariant()) {
        case "minutes": case "time": case "aftertime": case "afterminutes": return After(StopKind.AfterMinutes, v);
        case "uploaded": case "upload": case "uploadedmb": return After(StopKind.UploadedMB, v);
        case "downloaded": case "download": case "downloadedmb": return After(StopKind.DownloadedMB, v);
        case "ratio": case "ratioabove": return After(StopKind.RatioAbove, v);
        case "seeders": case "seedersbelow": return After(StopKind.SeedersBelow, v);
        case "leechers": case "leechersbelow": return After(StopKind.LeechersBelow, v);
        default: return Never;
      }
    }

    public override string ToString() {
      switch (Kind) {
        case StopKind.AfterMinutes: return $"after {Value:0.#} min";
        case StopKind.UploadedMB: return $"uploaded ≥ {Value:0.#} MB";
        case StopKind.DownloadedMB: return $"downloaded ≥ {Value:0.#} MB";
        case StopKind.RatioAbove: return $"ratio ≥ {Value:0.##}";
        case StopKind.SeedersBelow: return $"seeders < {Value:0}";
        case StopKind.LeechersBelow: return $"leechers < {Value:0}";
        default: return "never";
      }
    }
  }
}
