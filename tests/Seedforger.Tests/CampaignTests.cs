using System.Text.Json;
using Seedforger;
using Xunit;

namespace Seedforger.Tests {

  public class CampaignPlannerTests {

    [Fact]
    public void PaceCeiling_TracksDeadlineLinearly() {
      long goal = 1_000_000;
      Assert.Equal(0, CampaignPlanner.PaceCeiling(goal, 0, 100));
      Assert.Equal(500_000, CampaignPlanner.PaceCeiling(goal, 50, 100));
      Assert.Equal(goal, CampaignPlanner.PaceCeiling(goal, 200, 100)); // clamped
      Assert.Equal(goal, CampaignPlanner.PaceCeiling(goal, 10, 0));    // no deadline
    }

    [Fact]
    public void AllocateByDemand_IsProportionalAndBounded() {
      var a = CampaignPlanner.AllocateByDemand(4000, new[] { 10, 0, 30 });
      Assert.Equal(1000, a[0]);
      Assert.Equal(0, a[1]);   // no leechers -> no upload
      Assert.Equal(3000, a[2]);
      Assert.True(a[0] + a[1] + a[2] <= 4000);

      Assert.All(CampaignPlanner.AllocateByDemand(4000, new[] { 0, 0 }), x => Assert.Equal(0, x));
      Assert.All(CampaignPlanner.AllocateByDemand(0, new[] { 5, 5 }), x => Assert.Equal(0, x));
    }

    [Fact]
    public void StaggerOffsets_AreMonotonicWithinBoundsAndDeterministic() {
      var o1 = CampaignPlanner.StaggerOffsets(6, 3, 40, 42);
      var o2 = CampaignPlanner.StaggerOffsets(6, 3, 40, 42);
      Assert.Equal(o1, o2);          // deterministic
      Assert.Equal(0, o1[0]);        // first starts immediately
      for (var i = 1; i < o1.Length; i++) {
        var gap = o1[i] - o1[i - 1];
        Assert.InRange(gap, 3, 40);
      }
    }

    [Fact]
    public void GoalReached_Helpers() {
      Assert.True(CampaignPlanner.UploadGoalReached(1000, 1000));
      Assert.False(CampaignPlanner.UploadGoalReached(999, 1000));
      Assert.True(CampaignPlanner.RatioGoalReached(2000, 1000, 2.0));
      Assert.False(CampaignPlanner.RatioGoalReached(2000, 1000, 3.0));
      Assert.False(CampaignPlanner.RatioGoalReached(2000, 0, 1.0)); // no downloaded
    }
  }

  public class CampaignModelTests {
    [Fact]
    public void RoundTripsThroughJson() {
      var c = new Campaign {
        Goal = "ratio", TargetRatio = 3.5, UploadGoalGB = 42, DeadlineHours = 168,
        Connection = "VDSL2  (10 / 50 Mbps)", RotateClient = false,
        TorrentFolder = @"D:\t", StaggerMinMinutes = 5, StaggerMaxMinutes = 60, MaxConcurrent = 3,
      };
      var json = JsonSerializer.Serialize(c);
      var back = JsonSerializer.Deserialize<Campaign>(json,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

      Assert.Equal("ratio", back.Goal);
      Assert.Equal(3.5, back.TargetRatio);
      Assert.Equal("VDSL2  (10 / 50 Mbps)", back.Connection);
      Assert.False(back.RotateClient);
      Assert.Equal(3, back.MaxConcurrent);
    }
  }
}
