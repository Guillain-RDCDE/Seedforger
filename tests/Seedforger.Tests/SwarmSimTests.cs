using System;
using Seedforger.Integrity;
using Xunit;

namespace Seedforger.Tests {

  public class SwarmSimTests {

    private static (long up, long down, long trueUp, long trueDown) Totals(Swarm sw) {
      long up = 0, down = 0, tu = 0, td = 0;
      foreach (var p in sw.Peers) { up += p.DeclaredUp; down += p.DeclaredDown; tu += p.TrueUp; td += p.TrueDown; }
      return (up, down, tu, td);
    }

    [Fact]
    public void IsDeterministicInTheSeed() {
      var a = Totals(SwarmSim.Generate(new SwarmConfig(), 42));
      var b = Totals(SwarmSim.Generate(new SwarmConfig(), 42));
      Assert.Equal(a, b);
    }

    [Fact]
    public void DifferentSeeds_DifferentSwarms() {
      var a = Totals(SwarmSim.Generate(new SwarmConfig(), 1));
      var b = Totals(SwarmSim.Generate(new SwarmConfig(), 2));
      Assert.NotEqual(a.up, b.up);
    }

    [Fact]
    public void HonestOnlySwarm_ConservesMass() {
      var cfg = new SwarmConfig { CheaterFraction = 0.0 };
      for (int seed = 1; seed <= 15; seed++) {
        var (_, _, tu, td) = Totals(SwarmSim.Generate(cfg, seed));
        Assert.True(td > 0);
        double rel = Math.Abs(tu - td) / (double)td;
        Assert.True(rel < 0.02, $"seed {seed}: true up/down diverged by {rel:P2}");
      }
    }

    [Fact]
    public void PeerCounts_MatchTheConfiguredFractions() {
      var cfg = new SwarmConfig { PeerCount = 200, CheaterFraction = 0.15 };
      var sw = SwarmSim.Generate(cfg, 5);
      Assert.Equal(200, sw.Count);
      int cheaters = 0;
      foreach (var p in sw.Peers) if (p.IsCheater) cheaters++;
      Assert.Equal(30, cheaters); // round(200 * 0.15)
    }

    [Fact]
    public void EmptyConfig_YieldsEmptySwarm() {
      var sw = SwarmSim.Generate(new SwarmConfig { PeerCount = 0 }, 1);
      Assert.Equal(0, sw.Count);
    }

    [Fact]
    public void Cheaters_HaveHighCorroborationDeficit_HonestLow() {
      var cfg = new SwarmConfig();
      double cheaterMean = 0, honestMean = 0;
      int cN = 0, hN = 0;
      for (int seed = 1; seed <= 10; seed++) {
        var sw = SwarmSim.Generate(cfg, seed);
        foreach (var p in sw.Peers) {
          double d = Invariants.CorroborationDeficit(p.DeclaredUp, p.CorroboratedUp, sw.Coverage);
          if (p.IsCheater) { cheaterMean += d; cN++; } else { honestMean += d; hN++; }
        }
      }
      cheaterMean /= cN; honestMean /= hN;
      Assert.True(cheaterMean > 0.5, $"cheater deficit too low: {cheaterMean}");
      Assert.True(honestMean < 0.1, $"honest deficit too high: {honestMean}");
    }

    [Fact]
    public void WitnessedBytes_NeverExceedsTruth_AndIsNonNegative() {
      var rng = new Random(7);
      for (int i = 0; i < 500; i++) {
        long trueUp = (long)(rng.NextDouble() * 5_000_000);
        double c = rng.NextDouble();
        long w = SwarmSim.WitnessedBytes(trueUp, c, rng);
        Assert.True(w >= 0);
        Assert.True(w <= trueUp);
      }
    }

    [Fact]
    public void ZeroCoverage_WitnessesNothing() {
      var cfg = new SwarmConfig { Coverage = 0.0 };
      var sw = SwarmSim.Generate(cfg, 3);
      foreach (var p in sw.Peers) Assert.Equal(0, p.CorroboratedUp);
    }
  }
}
