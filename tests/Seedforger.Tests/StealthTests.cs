using System;
using Seedforger;
using Xunit;

namespace Seedforger.Tests {

  public class StealthTests {

    [Fact]
    public void JitterInterval_NeverEarlier_AndWithinBand() {
      var rand = new Random(1);
      for (var i = 0; i < 1000; i++) {
        var v = Stealth.JitterInterval(1800, rand);
        Assert.InRange(v, 1800, (int) (1800 * 1.12) + 1); // never sooner, up to ~+12%
      }
    }

    [Fact]
    public void JitterInterval_NonPositive_ReturnedAsIs() {
      Assert.Equal(0, Stealth.JitterInterval(0, new Random(1)));
      Assert.Equal(-5, Stealth.JitterInterval(-5, new Random(1)));
    }

    [Fact]
    public void DiurnalFactor_StaysInRange_AllDay() {
      for (var h = 0; h < 24; h++) {
        var f = Stealth.DiurnalFactor(new DateTime(2026, 1, 1, h, 0, 0));
        Assert.InRange(f, 0.5, 1.05);
      }
    }

    [Fact]
    public void DiurnalFactor_EveningHigherThanMorning() {
      var evening = Stealth.DiurnalFactor(new DateTime(2026, 1, 1, 20, 0, 0));
      var morning = Stealth.DiurnalFactor(new DateTime(2026, 1, 1, 8, 0, 0));
      Assert.True(evening > morning, $"evening {evening} should exceed morning {morning}");
    }

    [Fact]
    public void Believability_FlagsExcessiveUpload() {
      // ~800 Mbps upstream
      var w = Stealth.BelievabilityWarnings(100_000, 100_000);
      Assert.NotEmpty(w);
    }

    [Fact]
    public void Believability_FlagsUploadFarAboveDownload() {
      var w = Stealth.BelievabilityWarnings(5000, 500);
      Assert.Contains(w, m => m.Contains("exceeds download"));
    }

    [Theory]
    [InlineData(10, 8, 24, true)]   // inside a normal daytime window
    [InlineData(2, 8, 24, false)]   // outside it
    [InlineData(23, 22, 6, true)]   // inside a window that wraps midnight
    [InlineData(3, 22, 6, true)]    // still inside the wrapped window
    [InlineData(12, 22, 6, false)]  // outside the wrapped window
    [InlineData(5, 9, 9, true)]     // start == end => always active
    public void InActiveHours_HandlesWindowsAndWraps(int hour, int start, int end, bool expected) {
      var now = new DateTime(2026, 1, 1, hour, 30, 0);
      Assert.Equal(expected, Stealth.InActiveHours(now, start, end));
    }

    [Fact]
    public void Believability_NormalHomeLine_NoWarnings() {
      // ~10 Mbps up / 100 Mbps down (fibre-ish)
      var w = Stealth.BelievabilityWarnings(1220, 12200);
      Assert.Empty(w);
    }
  }
}
