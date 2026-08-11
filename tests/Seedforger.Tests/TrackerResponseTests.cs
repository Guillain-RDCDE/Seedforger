using System.IO;
using System.Text;
using Seedforger;
using Seedforger.BitTorrent;
using Xunit;

namespace Seedforger.Tests {
  public class TrackerResponseTests {
    static TrackerResponseTests() {
      Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    [Fact]
    public void ParsesHeadersAndBencodedBody() {
      // Minimal, non-chunked, non-redirect tracker HTTP response.
      const string body = "d3:foo3:bar5:helloi42ee";
      var raw = "HTTP/1.1 200 OK\r\n\r\n" + body;
      using var stream = new MemoryStream(Encoding.ASCII.GetBytes(raw));

      var response = new TrackerResponse(stream);

      Assert.False(response.doRedirect);
      Assert.NotNull(response.Dict);
      Assert.Equal("bar", BEncode.String(response.Dict["foo"]));
      Assert.Equal(42, ((ValueNumber) response.Dict["hello"]).Integer);
      Assert.Equal(body, response.Body);
    }

    [Fact]
    public void DetectsRedirect() {
      var raw = "HTTP/1.1 302 Found\r\nLocation: http://tracker.example/announce\r\n\r\n";
      using var stream = new MemoryStream(Encoding.ASCII.GetBytes(raw));

      var response = new TrackerResponse(stream);

      Assert.True(response.doRedirect);
      Assert.Equal("http://tracker.example/announce", response.RedirectionURL);
    }
  }
}
