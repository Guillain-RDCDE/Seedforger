using System;
using System.Collections.Generic;

namespace Seedforger.Integrity {

  /// <summary>
  /// The three integrity invariants a mature tracker can reconcile from data it
  /// actually holds. Each is a pure function returning a residual: 0 means "consistent
  /// with an honest swarm", larger means "harder to explain without fabrication".
  /// None of them reads a peer's private truth — they operate only on declared
  /// figures, the physical link ceiling, and the monitoring-peer corroboration.
  /// That restriction is the whole point: a detector that needed the truth would
  /// be useless.
  /// </summary>
  public static class Invariants {

    /// <summary>
    /// (1) Physical plausibility. A declared upload rate above the link ceiling is
    /// free evidence: nobody uploads faster than their uplink. Returns the fractional
    /// overshoot (0 if within capacity).
    /// </summary>
    public static double PhysicalResidual(long declaredUp, double windowSeconds, double linkCapacityBytesPerSec) {
      if (windowSeconds <= 0 || linkCapacityBytesPerSec <= 0) return 0.0;
      var speed = declaredUp / windowSeconds;
      return Math.Max(0.0, speed / linkCapacityBytesPerSec - 1.0);
    }

    /// <summary>
    /// (2) Swarm mass balance. Every byte downloaded by someone was uploaded by
    /// someone, so across a whole swarm total declared upload should equal total
    /// declared download. Fabricated upload with no matching download anywhere tips
    /// the balance positive. Returns a signed ratio in [-1, 1]; ~0 is healthy, &gt;0
    /// means net upload the swarm never received. This is a swarm-level alarm, not a
    /// per-peer verdict.
    /// </summary>
    public static double MassBalanceResidual(IEnumerable<SwarmPeer> peers) {
      long up = 0, down = 0;
      foreach (var p in peers) { up += p.DeclaredUp; down += p.DeclaredDown; }
      long denom = up + down;
      if (denom == 0) return 0.0;
      return (double)(up - down) / denom;
    }

    /// <summary>
    /// (3) Corroboration deficit. If coverage is c, an honest peer's declared upload
    /// should be witnessed by monitoring peers at rate ≈ c. The deficit is how far
    /// the witnessed fraction falls below that expectation, in [0, 1]: ~0 for an
    /// honest peer, →1 for a peer that declares upload nobody received. Note the
    /// coverage cancels out of the ratio, so *any* nonzero coverage yields signal —
    /// c only governs how noisy the witnessed count is (see the papers' boundary
    /// analysis).
    /// </summary>
    public static double CorroborationDeficit(long declaredUp, long corroboratedUp, double coverage) {
      if (coverage <= 0.0 || declaredUp <= 0) return 0.0;
      double expected = coverage * declaredUp;
      if (expected <= 0.0) return 0.0;
      double deficit = 1.0 - corroboratedUp / expected;
      if (deficit < 0.0) return 0.0;
      if (deficit > 1.0) return 1.0;
      return deficit;
    }
  }
}
