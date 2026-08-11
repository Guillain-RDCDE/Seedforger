using System;

namespace Seedforger.Integrity {

  /// <summary>
  /// Location estimators, from the fragile to the robust. The mature-tracker point
  /// is that the *statistic you pick* decides whether a coordinated minority can
  /// move your notion of "normal". The arithmetic mean has breakdown point 0 — a
  /// single unbounded liar drags it anywhere. The median and the Huber M-estimator
  /// have breakdown point ~0.5 — they hold until half the swarm colludes. The
  /// papers quantify exactly that.
  /// </summary>
  public static class RobustEstimator {

    public static double Mean(double[] xs) {
      if (xs == null || xs.Length == 0) return 0.0;
      double s = 0.0;
      for (int i = 0; i < xs.Length; i++) s += xs[i];
      return s / xs.Length;
    }

    public static double Median(double[] xs) {
      if (xs == null || xs.Length == 0) return 0.0;
      var copy = (double[])xs.Clone();
      Array.Sort(copy);
      int n = copy.Length;
      return (n % 2 == 1) ? copy[n / 2] : 0.5 * (copy[n / 2 - 1] + copy[n / 2]);
    }

    /// <summary>Median absolute deviation, scaled to be a consistent estimator of σ for normal data.</summary>
    public static double Mad(double[] xs) {
      if (xs == null || xs.Length == 0) return 0.0;
      double med = Median(xs);
      var dev = new double[xs.Length];
      for (int i = 0; i < xs.Length; i++) dev[i] = Math.Abs(xs[i] - med);
      return 1.4826 * Median(dev);
    }

    /// <summary>
    /// Huber M-estimator of location by iteratively reweighted least squares. Points
    /// within k robust-σ of the current centre count fully; points beyond it are
    /// down-weighted ∝ 1/|residual|, so a far-away colluding cluster contributes a
    /// bounded pull rather than an unbounded one.
    /// </summary>
    public static double HuberLocation(double[] xs, double k = 1.345, int maxIter = 100, double tol = 1e-9) {
      if (xs == null || xs.Length == 0) return 0.0;
      double mu = Median(xs);
      double scale = Mad(xs);
      if (scale <= 0.0) return mu; // no spread (or ≤2 distinct values) — median is the answer
      for (int iter = 0; iter < maxIter; iter++) {
        double wsum = 0.0, wxsum = 0.0;
        for (int i = 0; i < xs.Length; i++) {
          double r = (xs[i] - mu) / scale;
          double w = (Math.Abs(r) <= k) ? 1.0 : k / Math.Abs(r);
          wsum += w;
          wxsum += w * xs[i];
        }
        if (wsum <= 0.0) break;
        double next = wxsum / wsum;
        if (Math.Abs(next - mu) <= tol * (1.0 + Math.Abs(mu))) { mu = next; break; }
        mu = next;
      }
      return mu;
    }
  }
}
