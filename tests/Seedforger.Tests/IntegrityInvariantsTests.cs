using System.Collections.Generic;
using Seedforger.Integrity;
using Xunit;

namespace Seedforger.Tests {

  public class IntegrityInvariantsTests {

    [Fact]
    public void Physical_WithinCapacity_IsZero() {
      // 500 kB/s over a 1000 s window on a 1 MB/s link — well within capacity.
      double r = Invariants.PhysicalResidual(500_000L * 1000, 1000, 1_000_000);
      Assert.Equal(0.0, r, 6);
    }

    [Fact]
    public void Physical_DoubleTheLink_IsAboutOne() {
      double r = Invariants.PhysicalResidual(2_000_000L * 1000, 1000, 1_000_000);
      Assert.Equal(1.0, r, 6);
    }

    [Fact]
    public void Physical_DegenerateInputs_AreZero() {
      Assert.Equal(0.0, Invariants.PhysicalResidual(1_000_000, 0, 1_000_000), 6);
      Assert.Equal(0.0, Invariants.PhysicalResidual(1_000_000, 1000, 0), 6);
    }

    [Fact]
    public void MassBalance_HonestOnlySwarm_IsNearZero() {
      var cfg = new SwarmConfig { CheaterFraction = 0.0 };
      double worst = 0.0;
      for (int seed = 1; seed <= 20; seed++) {
        var sw = SwarmSim.Generate(cfg, seed);
        double r = System.Math.Abs(Invariants.MassBalanceResidual(sw.Peers));
        if (r > worst) worst = r;
      }
      Assert.True(worst < 0.02, $"healthy mass balance drifted to {worst}");
    }

    [Fact]
    public void MassBalance_CheatedSwarm_TipsPositive() {
      var cfg = new SwarmConfig { CheaterFraction = 0.15 };
      double mean = 0.0;
      for (int seed = 1; seed <= 20; seed++)
        mean += Invariants.MassBalanceResidual(SwarmSim.Generate(cfg, seed).Peers);
      mean /= 20;
      Assert.True(mean > 0.05, $"cheated swarm barely tipped: {mean}");
    }

    [Fact]
    public void MassBalance_EmptySwarm_IsZero() {
      Assert.Equal(0.0, Invariants.MassBalanceResidual(new List<SwarmPeer>()), 9);
    }

    [Fact]
    public void Corroboration_HonestFullyWitnessed_IsZero() {
      // declared 1000 pieces' worth, witnessed exactly the expected coverage share.
      long declared = 1_000_000;
      double c = 0.2;
      long witnessed = (long)(c * declared);
      Assert.Equal(0.0, Invariants.CorroborationDeficit(declared, witnessed, c), 6);
    }

    [Fact]
    public void Corroboration_FabricatedUpload_IsOne() {
      Assert.Equal(1.0, Invariants.CorroborationDeficit(1_000_000, 0, 0.2), 6);
    }

    [Fact]
    public void Corroboration_NoCoverage_YieldsNoSignal() {
      // Coverage 0 = no monitoring peers deployed: the invariant is silent, not a false alarm.
      Assert.Equal(0.0, Invariants.CorroborationDeficit(1_000_000, 0, 0.0), 6);
    }

    [Fact]
    public void Corroboration_IsClampedToUnitInterval() {
      // Over-witnessed (noise above expectation) clamps at 0, never negative.
      Assert.Equal(0.0, Invariants.CorroborationDeficit(1000, 5000, 0.2), 6);
    }
  }
}
