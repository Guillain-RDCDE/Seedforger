using System;

namespace Seedforger {

  /// <summary>
  /// The pure, testable brain of the orchestrator. Given the campaign state and
  /// the observed swarm, it decides: how much upload you *should* have done by now
  /// (pacing toward the deadline so you don't finish suspiciously early), how to
  /// split the upstream budget across torrents by real demand, when to start each
  /// torrent (staggered like a human), and whether the goal is reached.
  /// </summary>
  internal static class CampaignPlanner {

    /// <summary>
    /// Cumulative upload you should have reached by <paramref name="elapsedMinutes"/>
    /// to finish around the deadline (linear pace). If ahead of this, throttle; if
    /// behind, push. Deadline ≤ 0 disables pacing (returns the full goal).
    /// </summary>
    internal static long PaceCeiling(long goalBytes, double elapsedMinutes, double deadlineMinutes) {
      if (goalBytes <= 0) return long.MaxValue;
      if (deadlineMinutes <= 0) return goalBytes;
      var frac = elapsedMinutes / deadlineMinutes;
      if (frac < 0) frac = 0;
      if (frac > 1) frac = 1;
      return (long) (goalBytes * frac);
    }

    /// <summary>
    /// Splits <paramref name="globalUpKBps"/> across torrents proportional to their
    /// leecher demand. Torrents with no leechers get nothing (you can't credibly
    /// upload to an empty swarm). If nobody has leechers, everyone gets 0.
    /// </summary>
    internal static int[] AllocateByDemand(int globalUpKBps, int[] leechers) {
      var n = leechers.Length;
      var result = new int[n];
      if (n == 0 || globalUpKBps <= 0) return result;

      long totalDemand = 0;
      foreach (var l in leechers) totalDemand += l > 0 ? l : 0;
      if (totalDemand == 0) return result;

      for (var i = 0; i < n; i++) {
        var demand = leechers[i] > 0 ? leechers[i] : 0;
        result[i] = (int) ((long) globalUpKBps * demand / totalDemand);
      }
      return result;
    }

    /// <summary>
    /// Cumulative start offsets (minutes) for <paramref name="count"/> torrents, so
    /// they come online spread out like a real user adding them over time.
    /// Deterministic for a given seed. First torrent starts at 0.
    /// </summary>
    internal static int[] StaggerOffsets(int count, int minGapMin, int maxGapMin, int seed) {
      var offsets = new int[count];
      if (count <= 0) return offsets;
      var lo = Math.Max(0, minGapMin);
      var hi = Math.Max(lo, maxGapMin);
      var rand = new Random(seed);
      var acc = 0;
      for (var i = 0; i < count; i++) {
        offsets[i] = acc;
        acc += rand.Next(lo, hi + 1);
      }
      return offsets;
    }

    internal static bool UploadGoalReached(long uploadedBytes, long goalBytes)
      => goalBytes > 0 && uploadedBytes >= goalBytes;

    internal static bool RatioGoalReached(long uploaded, long downloaded, double target)
      => downloaded > 0 && (double) uploaded / downloaded >= target;
  }
}
