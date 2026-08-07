using Seedforger.Integrity;
using Xunit;

namespace Seedforger.Tests {

  public class AnomalyDetectorTests {

    private static (double[] scores, bool[] pos) ScoreSwarm(SwarmConfig cfg, int seed) {
      var sw = SwarmSim.Generate(cfg, seed);
      var scores = AnomalyDetector.Score(sw);
      var pos = new bool[sw.Count];
      for (int i = 0; i < sw.Count; i++) pos[i] = sw.Peers[i].IsCheater;
      return (scores, pos);
    }

    private static double MeanAuc(SwarmConfig cfg, int seeds = 25) {
      double s = 0;
      for (int seed = 1; seed <= seeds; seed++) {
        var (scores, pos) = ScoreSwarm(cfg, seed);
        s += Roc.Auc(scores, pos);
      }
      return s / seeds;
    }

    [Fact]
    public void HealthyDeployment_SeparatesCheatersAlmostPerfectly() {
      double auc = MeanAuc(new SwarmConfig());
      Assert.True(auc > 0.98, $"AUC too low for a healthy deployment: {auc}");
    }

    [Fact]
    public void NaiveCheaters_AreEvenEasier() {
      var cfg = new SwarmConfig { CheaterPhysicalOvershoot = 0.6 };
      Assert.True(MeanAuc(cfg) > 0.98);
    }

    [Fact]
    public void FreshTorrentWithThinCoverage_DegradesButBeatsChance() {
      // The documented weak spot: a brand-new torrent (tiny window) with almost no
      // monitoring peers. Detection should sag well below the healthy case, yet stay
      // clearly above a coin flip.
      var weak = new SwarmConfig { Coverage = 0.02, WindowSeconds = 90 };
      double auc = MeanAuc(weak);
      Assert.True(auc > 0.6, $"should still beat chance: {auc}");
      Assert.True(auc < 0.97, $"should be visibly degraded: {auc}");
    }

    [Fact]
    public void CoverageHelps_EstablishedBeatsFreshAtThinCoverage() {
      var established = new SwarmConfig { Coverage = 0.02, WindowSeconds = 1800 };
      var fresh = new SwarmConfig { Coverage = 0.02, WindowSeconds = 90 };
      Assert.True(MeanAuc(established) > MeanAuc(fresh));
    }

    [Fact]
    public void SwarmAlarm_RisesWithCheating() {
      double healthy = 0, cheated = 0;
      for (int seed = 1; seed <= 15; seed++) {
        healthy += System.Math.Abs(AnomalyDetector.SwarmAlarm(SwarmSim.Generate(new SwarmConfig { CheaterFraction = 0.0 }, seed)));
        cheated += System.Math.Abs(AnomalyDetector.SwarmAlarm(SwarmSim.Generate(new SwarmConfig { CheaterFraction = 0.20 }, seed)));
      }
      Assert.True(cheated > 10 * healthy, $"alarm did not rise enough: healthy={healthy / 15}, cheated={cheated / 15}");
    }

    [Fact]
    public void Auc_IsChance_WhenAClassIsEmpty() {
      Assert.Equal(0.5, Roc.Auc(new[] { 1.0, 2.0, 3.0 }, new[] { true, true, true }), 9);
      Assert.Equal(0.5, Roc.Auc(new[] { 1.0, 2.0, 3.0 }, new[] { false, false, false }), 9);
    }

    [Fact]
    public void Auc_IsOne_ForPerfectSeparation() {
      var scores = new[] { 0.1, 0.2, 0.9, 1.0 };
      var pos = new[] { false, false, true, true };
      Assert.Equal(1.0, Roc.Auc(scores, pos), 9);
    }

    [Fact]
    public void RocCurve_IsMonotoneAndSpansUnitBox() {
      var (scores, pos) = ScoreSwarm(new SwarmConfig(), 3);
      var (fpr, tpr) = Roc.Curve(scores, pos);
      Assert.Equal(0.0, fpr[0], 9);
      Assert.Equal(0.0, tpr[0], 9);
      Assert.Equal(1.0, fpr[fpr.Length - 1], 9);
      Assert.Equal(1.0, tpr[tpr.Length - 1], 9);
      for (int i = 1; i < fpr.Length; i++) {
        Assert.True(fpr[i] >= fpr[i - 1] - 1e-12);
        Assert.True(tpr[i] >= tpr[i - 1] - 1e-12);
      }
    }
  }
}
