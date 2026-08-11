using System;

namespace Seedforger.Integrity {

  /// <summary>
  /// The client/server information-asymmetry game — the negative result of the
  /// second paper, made executable.
  ///
  /// A client does real work <c>trueUp</c> and then declares <c>declaredUp ≥ trueUp</c>,
  /// inflating by <c>x = declaredUp − trueUp</c>. The server credits the declared
  /// figure but runs the corroboration-deficit detector, which flags when the
  /// witnessed fraction falls a margin τ below expectation:
  ///
  ///     deficit = 1 − trueUp / declaredUp ,   flagged when deficit &gt; τ.
  ///
  /// Solving the boundary gives the sharp result: the largest declared upload that
  /// survives is <c>trueUp / (1 − τ)</c>. Credit is therefore capped at a fixed
  /// *multiple of real work*, and at <c>trueUp = 0</c> the cap is 0 — there is no
  /// free ratio. The multiplier depends on τ, never on the client's cleverness,
  /// because the client cannot observe the variable it is being graded on.
  /// </summary>
  public static class AsymmetryGame {

    /// <summary>
    /// Largest declared upload that stays below the detector's flag, given real work
    /// <paramref name="trueUp"/> and detector margin <paramref name="tau"/> ∈ (0,1).
    /// A τ ≥ 1 is a disabled detector (no ceiling).
    /// </summary>
    public static double MaxUndetectedDeclaredUp(double trueUp, double tau) {
      if (trueUp < 0) trueUp = 0;
      if (tau >= 1.0) return double.PositiveInfinity;
      if (tau <= 0.0) return trueUp; // zero tolerance: no inflation survives
      return trueUp / (1.0 - tau);
    }

    /// <summary>
    /// The credit multiplier over real work, <c>1/(1−τ)</c> — independent of effort,
    /// of tooling, of everything the client controls. This single number is the paper.
    /// </summary>
    public static double MaxCreditMultiplier(double tau) {
      if (tau >= 1.0) return double.PositiveInfinity;
      if (tau <= 0.0) return 1.0;
      return 1.0 / (1.0 - tau);
    }

    /// <summary>
    /// A blind client searching for the most it can declare. It cannot read the
    /// corroboration signal, so it can only probe: push the declared figure up until
    /// the server flags, then back off. This binary search converges to the same
    /// <c>trueUp/(1−τ)</c> ceiling the closed form predicts — an optimisation that
    /// plateaus because it is blind to the graded variable. Returns the largest
    /// declared upload the client confirmed as unflagged.
    /// </summary>
    public static double BlindClientBestDeclared(double trueUp, double tau, double hi, int probes = 60) {
      if (trueUp < 0) trueUp = 0;
      // The only oracle the client has: "does declaring D get me flagged?"
      Func<double, bool> flagged = d => {
        if (d <= 0) return false;
        double deficit = 1.0 - trueUp / d; // = 1 − trueUp/declaredUp
        return deficit > tau + 1e-12;
      };
      double lo = trueUp; // declaring your real work is always safe
      if (!flagged(hi)) return hi; // detector effectively off within the probe range
      for (int i = 0; i < probes; i++) {
        double mid = 0.5 * (lo + hi);
        if (flagged(mid)) hi = mid; else lo = mid;
      }
      return lo;
    }
  }
}
