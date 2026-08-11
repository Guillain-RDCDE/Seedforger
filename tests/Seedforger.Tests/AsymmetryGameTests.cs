using System;
using Seedforger.Integrity;
using Xunit;

namespace Seedforger.Tests {

  public class AsymmetryGameTests {

    [Fact]
    public void NoFreeRatio_ZeroWorkCreditsNothing() {
      // The corollary that names the whole paper: with real work 0, the largest
      // credit that survives detection is 0, for any positive tolerance.
      Assert.Equal(0.0, AsymmetryGame.MaxUndetectedDeclaredUp(0.0, 0.20), 9);
      Assert.Equal(0.0, AsymmetryGame.MaxUndetectedDeclaredUp(0.0, 0.50), 9);
    }

    [Fact]
    public void Ceiling_IsWorkOverOneMinusTau() {
      Assert.Equal(10.0 / 0.8, AsymmetryGame.MaxUndetectedDeclaredUp(10.0, 0.20), 9);
      Assert.Equal(1.25, AsymmetryGame.MaxCreditMultiplier(0.20), 9);
    }

    [Fact]
    public void DisabledDetector_HasNoCeiling() {
      Assert.True(double.IsPositiveInfinity(AsymmetryGame.MaxUndetectedDeclaredUp(10.0, 1.0)));
      Assert.True(double.IsPositiveInfinity(AsymmetryGame.MaxCreditMultiplier(1.0)));
    }

    [Fact]
    public void ZeroTolerance_AllowsNoInflation() {
      Assert.Equal(10.0, AsymmetryGame.MaxUndetectedDeclaredUp(10.0, 0.0), 9);
      Assert.Equal(1.0, AsymmetryGame.MaxCreditMultiplier(0.0), 9);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(2.0)]
    [InlineData(6.0)]
    [InlineData(9.5)]
    public void BlindClient_ConvergesToTheClosedFormCeiling(double work) {
      double tau = 0.20;
      double got = AsymmetryGame.BlindClientBestDeclared(work, tau, hi: 100.0);
      double expected = AsymmetryGame.MaxUndetectedDeclaredUp(work, tau);
      Assert.True(Math.Abs(got - expected) < 1e-3, $"work={work}: got {got}, expected {expected}");
    }

    [Fact]
    public void BlindClient_WithZeroWork_StaysAtZero() {
      Assert.True(AsymmetryGame.BlindClientBestDeclared(0.0, 0.20, hi: 100.0) < 1e-9);
    }
  }
}
