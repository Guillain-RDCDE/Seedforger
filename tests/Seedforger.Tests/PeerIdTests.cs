using System;
using Seedforger;
using Xunit;

namespace Seedforger.Tests {

  public class PeerIdTests {

    [Fact]
    public void TransmissionTail_AlwaysHasValidChecksum() {
      var r = new Random(3);
      for (var i = 0; i < 500; i++) {
        var tail = PeerId.TransmissionTail(r);
        Assert.Equal(12, tail.Length);
        Assert.True(PeerId.IsValidTransmissionTail(tail), tail);
      }
    }

    [Fact]
    public void Validator_RejectsTamperedChecksum() {
      var tail = PeerId.TransmissionTail(new Random(1));
      var swapped = tail[11] == '0' ? '1' : '0';
      var bad = tail.Substring(0, 11) + swapped;
      Assert.False(PeerId.IsValidTransmissionTail(bad));
    }

    [Fact]
    public void GetClient_Transmission_ProducesValidChecksummedPeerId() {
      var c = TorrentClientFactory.GetClient("Transmission 4.1.3");
      Assert.StartsWith("-TR4130-", c.PeerID);
      var tail = c.PeerID.Substring(8);
      Assert.True(PeerId.IsValidTransmissionTail(tail), c.PeerID);
    }

    [Fact]
    public void GetClient_NonTransmission_TailUnchanged() {
      var c = TorrentClientFactory.GetClient("qBittorrent 5.2.3");
      Assert.StartsWith("-qB5230-", c.PeerID);
    }
  }
}
