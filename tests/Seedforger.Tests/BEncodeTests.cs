using System.IO;
using System.Text;
using Seedforger.BitTorrent;
using Xunit;

namespace Seedforger.Tests {
  public class BEncodeTests {
    static BEncodeTests() {
      // The BEncode types use Windows-1252, whose provider is only registered in
      // the app's Main(). Tests must register it themselves.
      Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private static Stream StreamOf(string bencoded) {
      return new MemoryStream(Encoding.ASCII.GetBytes(bencoded));
    }

    [Fact]
    public void Parse_Integer() {
      var value = BEncode.Parse(StreamOf("i42e"));
      var number = Assert.IsType<ValueNumber>(value);
      Assert.Equal(42, number.Integer);
    }

    [Fact]
    public void Parse_NegativeInteger() {
      var number = Assert.IsType<ValueNumber>(BEncode.Parse(StreamOf("i-13e")));
      Assert.Equal(-13, number.Integer);
    }

    [Fact]
    public void Parse_String() {
      var value = BEncode.Parse(StreamOf("4:spam"));
      var str = Assert.IsType<ValueString>(value);
      Assert.Equal("spam", str.String);
      Assert.Equal(4, str.Length);
    }

    [Fact]
    public void Parse_List() {
      var value = BEncode.Parse(StreamOf("li1ei2ee"));
      var list = Assert.IsType<ValueList>(value);
      Assert.Equal(2, list.values.Count);
      Assert.Equal("1", BEncode.String(list[0]));
      Assert.Equal("2", BEncode.String(list[1]));
    }

    [Fact]
    public void Parse_Dictionary() {
      var value = BEncode.Parse(StreamOf("d3:foo3:bar5:helloi42ee"));
      var dict = Assert.IsType<ValueDictionary>(value);
      Assert.True(dict.Contains("foo"));
      Assert.True(dict.Contains("hello"));
      Assert.Equal("bar", BEncode.String(dict["foo"]));
      Assert.Equal(42, ((ValueNumber) dict["hello"]).Integer);
    }

    [Fact]
    public void RoundTrip_Dictionary_PreservesValues() {
      var original = new ValueDictionary();
      original.Add("num", new ValueNumber(12345));
      original.Add("str", new ValueString("hello world"));
      var list = new ValueList();
      list.Add(new ValueNumber(1));
      list.Add(new ValueString("x"));
      original.Add("lst", list);

      var encoded = original.Encode();
      var reparsed = Assert.IsType<ValueDictionary>(BEncode.Parse(new MemoryStream(encoded)));

      Assert.Equal(12345, ((ValueNumber) reparsed["num"]).Integer);
      Assert.Equal("hello world", BEncode.String(reparsed["str"]));
      var reList = Assert.IsType<ValueList>(reparsed["lst"]);
      Assert.Equal(2, reList.values.Count);
      Assert.Equal(1, ((ValueNumber) reList[0]).Integer);
      Assert.Equal("x", BEncode.String(reList[1]));
    }

    [Fact]
    public void Encode_String_MatchesBencodeFormat() {
      var encoded = new ValueString("spam").Encode();
      Assert.Equal("4:spam", Encoding.ASCII.GetString(encoded));
    }

    [Fact]
    public void Encode_Number_MatchesBencodeFormat() {
      var encoded = new ValueNumber(42).Encode();
      Assert.Equal("i42e", Encoding.ASCII.GetString(encoded));
    }

    [Fact]
    public void String_ReturnsNullForListValue() {
      Assert.Null(BEncode.String(new ValueList()));
    }
  }
}
