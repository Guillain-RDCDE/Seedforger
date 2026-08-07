using System;
using Seedforger.Integrity;
using Xunit;

namespace Seedforger.Tests {

  public class RobustEstimatorTests {

    [Fact]
    public void Mean_Median_Mad_Basics() {
      var xs = new[] { 1.0, 2.0, 3.0, 4.0, 5.0 };
      Assert.Equal(3.0, RobustEstimator.Mean(xs), 9);
      Assert.Equal(3.0, RobustEstimator.Median(xs), 9);
      var even = new[] { 1.0, 2.0, 3.0, 4.0 };
      Assert.Equal(2.5, RobustEstimator.Median(even), 9);
      Assert.True(RobustEstimator.Mad(xs) > 0);
    }

    [Fact]
    public void EmptyInput_IsZero() {
      Assert.Equal(0.0, RobustEstimator.Mean(new double[0]), 9);
      Assert.Equal(0.0, RobustEstimator.Median(new double[0]), 9);
      Assert.Equal(0.0, RobustEstimator.HuberLocation(new double[0]), 9);
    }

    [Fact]
    public void Huber_OnCleanData_TracksTheCentre() {
      var rng = new Random(12345);
      var xs = new double[500];
      for (int i = 0; i < xs.Length; i++) xs[i] = 10.0 + 0.5 * Gauss(rng);
      Assert.Equal(10.0, RobustEstimator.HuberLocation(xs), 1); // within ~0.05
    }

    [Fact]
    public void Huber_NoSpread_ReturnsMedian() {
      var xs = new[] { 7.0, 7.0, 7.0, 7.0 };
      Assert.Equal(7.0, RobustEstimator.HuberLocation(xs), 9);
    }

    [Fact]
    public void Breakdown_RobustEstimatorsHold_MeanDoesNot() {
      // 40% of a clean sample replaced by a far-away colluding value.
      const int n = 300;
      const double center = 1.0, outlier = 11.0;
      var rng = new Random(999);
      var xs = new double[n];
      for (int i = 0; i < n; i++) xs[i] = center + 0.12 * Gauss(rng);
      int k = (int)(0.40 * n);
      for (int i = 0; i < k; i++) xs[i] = outlier;

      double mean = RobustEstimator.Mean(xs);
      double huber = RobustEstimator.HuberLocation(xs);
      double median = RobustEstimator.Median(xs);

      Assert.True(mean > 3.0, $"mean should be dragged far: {mean}");
      Assert.True(Math.Abs(huber - center) < 0.8, $"Huber should hold near centre: {huber}");
      Assert.True(Math.Abs(median - center) < 0.5, $"median should hold near centre: {median}");
      Assert.True(Math.Abs(huber - center) < Math.Abs(mean - center));
    }

    private static double Gauss(Random rng) {
      double u1 = 1.0 - rng.NextDouble();
      double u2 = 1.0 - rng.NextDouble();
      return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }
  }
}
