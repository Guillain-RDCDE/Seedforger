using Seedforger;
using Xunit;

namespace Seedforger.Tests {

  public class MagnetTests {

    private const string Hex40 = "0123456789ABCDEF0123456789ABCDEF01234567";

    [Fact]
    public void Parse_HexMagnet_ExtractsHashNameTrackers() {
      var uri = "magnet:?xt=urn:btih:" + Hex40 +
                "&dn=Ubuntu%2024.04&tr=http%3A%2F%2Ftracker.example%2Fannounce";
      var m = Magnet.Parse(uri);

      Assert.NotNull(m);
      Assert.Equal(20, m.InfoHash.Length);
      Assert.Equal(Hex40, m.HashHex);
      Assert.Equal("Ubuntu 24.04", m.Name);
      Assert.Single(m.Trackers);
      Assert.Equal("http://tracker.example/announce", m.Trackers[0]);
    }

    [Fact]
    public void Parse_Base32Magnet_DecodesTo20Bytes() {
      // 32 'A's in base32 decode to 20 zero bytes.
      var m = Magnet.Parse("magnet:?xt=urn:btih:" + new string('A', 32));
      Assert.NotNull(m);
      Assert.Equal(20, m.InfoHash.Length);
      Assert.All(m.InfoHash, b => Assert.Equal(0, b));
    }

    [Fact]
    public void Parse_NonMagnet_ReturnsNull() {
      Assert.Null(Magnet.Parse("http://example.com/file.torrent"));
      Assert.Null(Magnet.Parse("magnet:?dn=NoHashHere"));
      Assert.Null(Magnet.Parse(null));
    }

    [Fact]
    public void HashToBytes_RejectsWrongLength() {
      Assert.Null(Magnet.HashToBytes("ABCD"));
      Assert.NotNull(Magnet.HashToBytes(Hex40));
      Assert.Equal(20, Magnet.HashToBytes(Hex40).Length);
    }

    [Fact]
    public void IsMagnet_DetectsScheme() {
      Assert.True(Magnet.IsMagnet("magnet:?xt=urn:btih:" + Hex40));
      Assert.False(Magnet.IsMagnet("https://x/y"));
    }
  }
}
