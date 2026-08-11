using System;

namespace Seedforger {

  /// <summary>
  /// Derives believable speed multipliers from the swarm reality reported by the
  /// tracker (seeders / leechers from scrape or the announce response). The point
  /// is that you can only physically upload as much as there is demand for: with
  /// zero leechers there is nobody to feed, and your share is diluted by every
  /// other seeder competing to feed the same leechers. Pure and testable.
  /// </summary>
  internal static class SwarmModel {

    /// <summary>
    /// Upload multiplier. Negative counts mean "no scrape data" -> no gating (1.0).
    /// 0 leechers -> a trickle. Otherwise your share ~ leechers / (seeders + 1).
    /// </summary>
    internal static double UploadFactor(int leechers, int seeders) {
      if (leechers < 0 || seeders < 0) return 1.0;
      if (leechers == 0) return 0.02;
      var share = (double) leechers / (seeders + 1);
      return Clamp(share, 0.05, 1.0);
    }

    /// <summary>
    /// Download multiplier, bounded by how many seeders can feed you.
    /// No data -> 1.0. 0 seeders -> 0 (nobody to download from).
    /// </summary>
    internal static double DownloadFactor(int seeders) {
      if (seeders < 0) return 1.0;
      if (seeders == 0) return 0.0;
      return Clamp(seeders / 10.0, 0.1, 1.0);
    }

    private static double Clamp(double v, double lo, double hi) => Math.Min(hi, Math.Max(lo, v));
  }
}
