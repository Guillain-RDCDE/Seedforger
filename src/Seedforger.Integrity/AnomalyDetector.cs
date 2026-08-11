using System;

namespace Seedforger.Integrity {

  /// <summary>Relative weights on the per-peer invariants that make up an anomaly score.</summary>
  public sealed class DetectorWeights {
    public double Physical = 1.0;
    public double Corroboration = 4.0;
    public static DetectorWeights Default => new DetectorWeights();
  }

  /// <summary>
  /// Turns the invariants into a per-peer anomaly score and scores a whole swarm.
  /// Corroboration deficit and physical implausibility are per-peer signals; mass
  /// balance is a swarm-level alarm (a constant offset does not change per-peer
  /// ranking, so it is reported separately, not folded into the score).
  /// </summary>
  public static class AnomalyDetector {

    /// <summary>Anomaly score for a single peer from observable quantities only.</summary>
    public static double ScorePeer(SwarmPeer p, double coverage, DetectorWeights w) {
      double phys = Invariants.PhysicalResidual(p.DeclaredUp, p.WindowSeconds, p.LinkCapacityBytesPerSec);
      double corr = Invariants.CorroborationDeficit(p.DeclaredUp, p.CorroboratedUp, coverage);
      return w.Physical * phys + w.Corroboration * corr;
    }

    /// <summary>Scores every peer in the swarm.</summary>
    public static double[] Score(Swarm swarm, DetectorWeights w = null) {
      w = w ?? DetectorWeights.Default;
      var scores = new double[swarm.Count];
      for (int i = 0; i < swarm.Count; i++) scores[i] = ScorePeer(swarm.Peers[i], swarm.Coverage, w);
      return scores;
    }

    /// <summary>The swarm-level mass-balance alarm (see <see cref="Invariants.MassBalanceResidual"/>).</summary>
    public static double SwarmAlarm(Swarm swarm) => Invariants.MassBalanceResidual(swarm.Peers);
  }

  /// <summary>ROC evaluation for a scored swarm.</summary>
  public static class Roc {

    /// <summary>
    /// Area under the ROC curve via the Mann–Whitney U identity: AUC = P(score of a
    /// random cheater &gt; score of a random honest peer), ties counted as ½. Returns
    /// 0.5 (chance) when either class is empty.
    /// </summary>
    public static double Auc(double[] scores, bool[] positive) {
      long nPos = 0, nNeg = 0;
      double u = 0.0;
      for (int i = 0; i < scores.Length; i++) {
        if (positive[i]) continue;
        nNeg++;
      }
      for (int i = 0; i < scores.Length; i++) {
        if (!positive[i]) continue;
        nPos++;
        for (int j = 0; j < scores.Length; j++) {
          if (positive[j]) continue;
          if (scores[i] > scores[j]) u += 1.0;
          else if (scores[i] == scores[j]) u += 0.5;
        }
      }
      if (nPos == 0 || nNeg == 0) return 0.5;
      return u / ((double)nPos * nNeg);
    }

    /// <summary>
    /// The ROC curve as (fpr, tpr) points, swept over every distinct score threshold
    /// from most to least permissive. Suitable for plotting.
    /// </summary>
    public static (double[] fpr, double[] tpr) Curve(double[] scores, bool[] positive) {
      int n = scores.Length;
      var idx = new int[n];
      for (int i = 0; i < n; i++) idx[i] = i;
      Array.Sort(idx, (a, b) => scores[b].CompareTo(scores[a])); // descending score

      long totalPos = 0, totalNeg = 0;
      for (int i = 0; i < n; i++) { if (positive[i]) totalPos++; else totalNeg++; }
      if (totalPos == 0 || totalNeg == 0)
        return (new double[] { 0.0, 1.0 }, new double[] { 0.0, 1.0 });

      var fpr = new System.Collections.Generic.List<double>();
      var tpr = new System.Collections.Generic.List<double>();
      fpr.Add(0.0); tpr.Add(0.0);
      long tp = 0, fp = 0;
      double prevScore = double.PositiveInfinity;
      for (int rank = 0; rank < n; rank++) {
        int i = idx[rank];
        if (scores[i] != prevScore) {
          fpr.Add((double)fp / totalNeg);
          tpr.Add((double)tp / totalPos);
          prevScore = scores[i];
        }
        if (positive[i]) tp++; else fp++;
      }
      fpr.Add((double)fp / totalNeg);
      tpr.Add((double)tp / totalPos);
      return (fpr.ToArray(), tpr.ToArray());
    }
  }
}
