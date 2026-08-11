using System;
using System.Linq;
using Seedforger;
using Xunit;

namespace Seedforger.Tests {

  public class SpeedShaperTests {

    private const long Target = 1_000_000; // 1 MB/s

    [Fact]
    public void ReturnsZero_WhenTargetIsZeroOrNegative() {
      var s = new SpeedShaper(new Random(1));
      Assert.Equal(0, s.NextSecondBytes(0));
      Assert.Equal(0, s.NextSecondBytes(-100));
    }

    [Fact]
    public void NeverNegative() {
      var s = new SpeedShaper(new Random(42));
      for (var i = 0; i < 500; i++)
        Assert.True(s.NextSecondBytes(Target) >= 0);
    }

    [Fact]
    public void RampsUp_EarlyOutputIsLowerThanLater() {
      var s = new SpeedShaper(new Random(7), rampUpSeconds: 45);
      // First few seconds (deep in ramp) vs. steady state well past the ramp.
      var early = Enumerable.Range(0, 5).Select(_ => s.NextSecondBytes(Target)).Average();
      for (var i = 0; i < 100; i++) s.NextSecondBytes(Target); // advance past ramp
      var later = Enumerable.Range(0, 20).Select(_ => s.NextSecondBytes(Target)).Average();
      Assert.True(early < later, $"early={early} should be < later={later}");
      Assert.True(early < Target * 0.5, $"first seconds should be well below target, got {early}");
    }

    [Fact]
    public void SteadyState_StaysWithinVarianceBand() {
      var s = new SpeedShaper(new Random(123), rampUpSeconds: 10, variance: 0.15);
      for (var i = 0; i < 50; i++) s.NextSecondBytes(Target); // finish ramp
      for (var i = 0; i < 1000; i++) {
        var v = s.NextSecondBytes(Target);
        Assert.InRange(v, (long) (Target * 0.80), (long) (Target * 1.20));
      }
    }

    [Fact]
    public void SteadyState_AveragesNearTarget() {
      var s = new SpeedShaper(new Random(999), rampUpSeconds: 10, variance: 0.15);
      for (var i = 0; i < 50; i++) s.NextSecondBytes(Target);
      var avg = Enumerable.Range(0, 3000).Select(_ => (double) s.NextSecondBytes(Target)).Average();
      // Mean-reverting walk should hover close to the target on average.
      Assert.InRange(avg, Target * 0.90, Target * 1.10);
    }

    [Fact]
    public void Reset_RestartsRampUp() {
      var s = new SpeedShaper(new Random(5), rampUpSeconds: 45);
      for (var i = 0; i < 100; i++) s.NextSecondBytes(Target); // reach full speed
      s.Reset();
      var firstAfterReset = s.NextSecondBytes(Target);
      Assert.True(firstAfterReset < Target * 0.5,
        $"after reset the first second should be back low, got {firstAfterReset}");
    }

    [Fact]
    public void Deterministic_ForSameSeed() {
      var a = new SpeedShaper(new Random(2024));
      var b = new SpeedShaper(new Random(2024));
      for (var i = 0; i < 100; i++)
        Assert.Equal(a.NextSecondBytes(Target), b.NextSecondBytes(Target));
    }
  }
}
