using System.Collections.Generic;
using System.Text;
using Seedforger.Wire;
using Xunit;

namespace Seedforger.Tests {

  public class BencodeTests {

    private static string Ascii(byte[] b) => Encoding.ASCII.GetString(b);

    [Fact]
    public void EncodesIntStringList() {
      Assert.Equal("i42e", Ascii(Bencode.Encode(42)));
      Assert.Equal("4:spam", Ascii(Bencode.Encode("spam")));
      Assert.Equal("li1ei2ee", Ascii(Bencode.Encode(new List<object> { 1, 2 })));
    }

    [Fact]
    public void EncodesDictionaryWithSortedKeys() {
      var d = new Dictionary<string, object> { ["foo"] = "bar", ["a"] = 1 };
      Assert.Equal("d1:ai1e3:foo3:bare", Ascii(Bencode.Encode(d)));
    }
  }

  public class ExtensionMessageTests {

    [Fact]
    public void Reserved_HasExtensionBit() {
      var r = ExtensionMessages.ReservedWithExtensions();
      Assert.Equal(8, r.Length);
      Assert.Equal(0x10, r[5]);
    }

    [Fact]
    public void ExtendedHandshake_IsExtendedMessageAdvertisingExtensions() {
      var m = ExtensionMessages.ExtendedHandshake("Seedforger");
      // <len(4)><id=20><sub=0><bencoded dict...>
      Assert.Equal((byte) MsgId.Extended, m[4]);
      Assert.Equal(0, m[5]);                 // extended handshake sub-id
      var payload = Encoding.ASCII.GetString(m, 6, m.Length - 6);
      Assert.Contains("ut_metadata", payload);
      Assert.Contains("ut_pex", payload);
      Assert.StartsWith("d", payload);       // bencoded dict
    }

    [Fact]
    public void Pex_OneFlagBytePerPeer() {
      var peers = new byte[12]; // 2 compact peers
      var m = ExtensionMessages.Pex(peers);
      Assert.Equal((byte) MsgId.Extended, m[4]);
      var payload = Encoding.ASCII.GetString(m, 6, m.Length - 6);
      Assert.Contains("7:added.f", payload); // the "added.f" key
      Assert.Contains("5:added", payload);   // the "added" key (12-byte compact peers)
    }
  }
}
