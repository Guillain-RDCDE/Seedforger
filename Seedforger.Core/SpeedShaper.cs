using System;

namespace Seedforger {

  /// <summary>
  /// Produces a believable per-second byte delta for one transfer direction.
  /// Real BitTorrent clients do not jump to full speed instantly, nor hold a
  /// perfectly constant rate: they ramp up while peers are discovered, then
  /// fluctuate smoothly around the target. This shapes the raw target rate with
  /// a ramp-up and a mean-reverting (autocorrelated) random walk, instead of the
  /// old independent-uniform noise which looked machine-generated to trackers.
  ///
  /// Pure and deterministic given an injected <see cref="Random"/> so it can be
  /// unit-tested.
  /// </summary>
  internal sealed class SpeedShaper {

    private readonly Random rand;
    private readonly int rampUpSeconds;
    private readonly double variance;    // e.g. 0.15 => +/-15% around target
    private readonly double reversion;   // pull of the walk back toward 1.0 (0..1)
    private readonly double step;        // max change of the factor per second

    private double factor = 1.0;         // current multiplicative factor
    private int elapsed;                 // seconds since (re)start

    /// <param name="rand">RNG (inject a seeded instance for tests).</param>
    /// <param name="rampUpSeconds">Seconds to reach full speed from a standing start.</param>
    /// <param name="variance">Fractional band around the target rate (0.15 = +/-15%).</param>
    /// <param name="reversion">Mean-reversion strength toward 1.0 each second (0..1).</param>
    /// <param name="step">Max random change of the factor per second.</param>
    internal SpeedShaper(Random rand, int rampUpSeconds = 45, double variance = 0.15,
      double reversion = 0.15, double step = 0.07) {
      this.rand = rand ?? throw new ArgumentNullException(nameof(rand));
      this.rampUpSeconds = Math.Max(0, rampUpSeconds);
      this.variance = Math.Max(0.0, variance);
      this.reversion = Math.Min(1.0, Math.Max(0.0, reversion));
      this.step = Math.Max(0.0, step);
    }

    /// <summary>Restart the ramp-up (e.g. when transfer (re)starts).</summary>
    internal void Reset() {
      factor = 1.0;
      elapsed = 0;
    }

    /// <summary>
    /// Bytes to add for the next one-second slice, given the current target rate
    /// in bytes/second. Never negative. When <paramref name="targetBytesPerSec"/>
    /// is 0 the result is 0.
    /// </summary>
    internal long NextSecondBytes(long targetBytesPerSec) {
      if (targetBytesPerSec <= 0) {
        elapsed++;
        return 0;
      }

      // Mean-reverting random walk of the multiplicative factor.
      var shock = (rand.NextDouble() * 2.0 - 1.0) * step; // [-step, +step]
      factor += shock - reversion * (factor - 1.0);
      if (factor < 1.0 - variance) factor = 1.0 - variance;
      if (factor > 1.0 + variance) factor = 1.0 + variance;

      // Smooth ramp-up (ease-out) over the first rampUpSeconds.
      var ramp = 1.0;
      if (rampUpSeconds > 0 && elapsed < rampUpSeconds) {
        var t = (double) elapsed / rampUpSeconds; // 0..1
        ramp = 1.0 - (1.0 - t) * (1.0 - t);       // ease-out quad
      }

      elapsed++;

      var bytes = targetBytesPerSec * ramp * factor;
      if (bytes < 0) bytes = 0;
      return (long) bytes;
    }
  }
}
