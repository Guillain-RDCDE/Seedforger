using Seedforger;
using Seedforger.Wire;
using Xunit;

namespace Seedforger.Tests {

  public class SwarmModelTests {

    [Fact]
    public void UploadFactor_NoData_NoGating() {
      Assert.Equal(1.0, SwarmModel.UploadFactor(-1, -1));
    }

    [Fact]
    public void UploadFactor_ZeroLeechers_IsTrickle() {
      Assert.Equal(0.02, SwarmModel.UploadFactor(0, 10), 3);
    }

    [Fact]
    public void UploadFactor_ManyLeechersFewSeeders_Full() {
      Assert.Equal(1.0, SwarmModel.UploadFactor(50, 3), 3);
    }

    [Fact]
    public void UploadFactor_FewLeechersManySeeders_SmallShare() {
      var f = SwarmModel.UploadFactor(1, 40);
      Assert.InRange(f, 0.05, 0.10);
    }

    [Fact]
    public void DownloadFactor_ReflectsSeederSupply() {
      Assert.Equal(1.0, SwarmModel.DownloadFactor(-1));  // no data
      Assert.Equal(0.0, SwarmModel.DownloadFactor(0));   // nobody to pull from
      Assert.Equal(1.0, SwarmModel.DownloadFactor(20));  // plenty -> capped
      Assert.Equal(0.3, SwarmModel.DownloadFactor(3), 3);
    }
  }

  public class UpstreamBudgetTests {

    [Fact]
    public void Disabled_ReturnsRequestUnchanged() {
      var budget = new UpstreamBudget();
      Assert.False(budget.Enabled);
      Assert.Equal(1_000_000, budget.CapUpload(1_000_000));
    }

    [Fact]
    public void SharesTheLineAcrossActiveEngines() {
      var budget = new UpstreamBudget(1000); // 1000 kB/s = 1_024_000 B/s
      budget.Register();
      budget.Register(); // 2 active
      Assert.Equal(2, budget.ActiveCount);
      Assert.Equal(512_000, budget.CapUpload(5_000_000)); // capped to fair share
      Assert.Equal(100_000, budget.CapUpload(100_000));   // under share -> unchanged
      budget.Unregister();
      Assert.Equal(1_024_000, budget.CapUpload(5_000_000)); // alone on the line again
    }

    [Fact]
    public void InstancesAreIndependent_SoACampaignNeverTouchesTheSharedLine() {
      var shared = new UpstreamBudget(100);
      var campaign = new UpstreamBudget(5000);
      campaign.Register();
      Assert.Equal(100, shared.TotalKBps);
      Assert.Equal(0, shared.ActiveCount);
      Assert.Equal(1, campaign.ActiveCount);
    }
  }

  public class PeerProtocolTests {

    [Fact]
    public void FullBitfield_FullBytes() {
      var m = PeerProtocol.FullBitfield(16); // 2 full bytes, no spare
      Assert.Equal(4 + 1 + 2, m.Length);
      Assert.Equal(5, m[4]);       // bitfield id
      Assert.Equal(0xFF, m[5]);
      Assert.Equal(0xFF, m[6]);
    }

    [Fact]
    public void FullBitfield_ClearsSpareBits() {
      var m = PeerProtocol.FullBitfield(12); // 2 bytes, 4 spare low bits in last
      Assert.Equal(4 + 1 + 2, m.Length);
      Assert.Equal(0xFF, m[5]);
      Assert.Equal(0xF0, m[6]);    // 0xFF << 4
    }

    [Fact]
    public void FullBitfield_ZeroPieces_HasAnEmptyPayload() {
      var m = PeerProtocol.FullBitfield(0);
      Assert.Equal(5, m.Length);   // length prefix (1) + id, nothing else
      Assert.Equal(5, m[4]);
    }

    [Fact]
    public void Choke_IsFiveBytes() {
      Assert.Equal(new byte[] { 0, 0, 0, 1, 0 }, PeerProtocol.Choke());
    }
  }
}
