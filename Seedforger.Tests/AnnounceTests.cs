using System.IO;
using System.Text;
using Seedforger;
using Xunit;

namespace Seedforger.Tests {

  /// <summary>
  /// Covers the WinForms-free announce core — the part that actually goes on the
  /// wire: the exact query string, the info_hash percent-encoding, and reading the
  /// tracker's bencoded answer (including a full raw HTTP response).
  /// </summary>
  public class AnnounceTests {
    static AnnounceTests() {
      Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private const string QBittorrentQuery =
      "info_hash={infohash}&peer_id={peerid}&port={port}&uploaded={uploaded}&downloaded={downloaded}" +
      "&left={left}&corrupt=0&key={key}{event}&numwant={numwant}&compact=1&no_peer_id=1&supportcrypto=1&redundant=0";

    // ---- percent-encoding / info_hash ----

    [Fact]
    public void PercentEncode_KeepsAsciiAlnum_EscapesTheRest_Lowercase() {
      // bytes: 0x00 (ctrl) 'A' 0x7e('~') 0xff
      Assert.Equal("%00A%7e%ff", Announce.EncodeHash("00417eff", upperCase: false));
    }

    [Fact]
    public void PercentEncode_HonoursUpperCase() {
      Assert.Equal("%00A%7E%FF", Announce.EncodeHash("00417eff", upperCase: true));
    }

    [Fact]
    public void EncodeHash_RoundTripsBackToTheOriginalBytes() {
      const string hex = "bc5e27a7f1be319c29ff2764781445567ec4748b"; // a real 20-byte SHA-1
      var encoded = Announce.EncodeHash(hex, upperCase: false);
      var decoded = PercentDecodeToBytes(encoded);

      var expected = new byte[hex.Length / 2];
      for (var i = 0; i < expected.Length; i++)
        expected[i] = System.Convert.ToByte(hex.Substring(i * 2, 2), 16);

      Assert.Equal(expected, decoded);
    }

    // ---- URL building ----

    [Fact]
    public void BuildUrl_ProducesTheExactAnnounceQuery() {
      const string hex = "0102030405060708090a0b0c0d0e0f1011121314";
      var p = new Announce.Params {
        Tracker = "https://tracker.test/announce",
        QueryTemplate = QBittorrentQuery,
        InfoHashHex = hex,
        PeerId = "-qB5230-abcdef012345",
        Port = "6881",
        Uploaded = 100000,   // -> rounded down to a 0x4000 boundary (98304)
        Downloaded = 0,
        Left = 0,
        TotalSize = 1000000,
        Key = "ABCD1234",
        NumWant = "0",        // -> defaults to 200 on a non-stopped event
        Event = "&event=started",
        LocalIp = "192.168.0.2",
        HashUpperCase = false,
      };

      var expected =
        "https://tracker.test/announce?info_hash=" + Announce.EncodeHash(hex, false) +
        "&peer_id=-qB5230-abcdef012345&port=6881&uploaded=98304&downloaded=0&left=0" +
        "&corrupt=0&key=ABCD1234&event=started&numwant=200&compact=1&no_peer_id=1&supportcrypto=1&redundant=0";

      Assert.Equal(expected, Announce.BuildUrl(p));
    }

    [Fact]
    public void BuildUrl_RoundsUploadedAndDownloadedToBelievableBoundaries() {
      var p = BaseParams();
      p.Uploaded = 0x4000 * 3 + 5000; // not on a boundary
      p.Downloaded = 0x10 * 7 + 9;    // not on a boundary
      p.Left = 1;                     // >0 so left = totalsize - rounded downloaded

      var url = Announce.BuildUrl(p);

      Assert.Contains("&uploaded=" + (0x4000 * 3), url);
      Assert.Contains("&downloaded=" + (0x10 * 7), url);
      Assert.Contains("&left=" + (p.TotalSize - 0x10 * 7), url);
    }

    [Fact]
    public void BuildUrl_KeepsNumwantZeroOnStopped() {
      var p = BaseParams();
      p.NumWant = "0";
      p.Event = "&event=stopped";
      Assert.Contains("&numwant=0", Announce.BuildUrl(p));
    }

    [Fact]
    public void BuildUrl_AppendsAmpersandWhenTrackerAlreadyHasAQuery() {
      var p = BaseParams();
      p.Tracker = "https://tracker.test/announce?passkey=xyz";
      Assert.Contains("/announce?passkey=xyz&info_hash=", Announce.BuildUrl(p));
    }

    // ---- response parsing ----

    [Fact]
    public void FromBody_ReadsAcceptedSwarm() {
      var body = Encoding.ASCII.GetBytes("d8:completei5e10:incompletei3e8:intervali1800ee");
      var r = Announce.ParseBody(body);

      Assert.True(r.Accepted);
      Assert.Null(r.Failure);
      Assert.Equal(5, r.Seeders);
      Assert.Equal(3, r.Leechers);
      Assert.Equal(1800, r.Interval);
    }

    [Fact]
    public void FromBody_ReadsFailureReason() {
      var body = Encoding.ASCII.GetBytes("d14:failure reason30:your ratio is too low to leeche");
      var r = Announce.ParseBody(body);

      Assert.False(r.Accepted);
      Assert.Equal("your ratio is too low to leech", r.Failure);
    }

    [Fact]
    public void ParseHttpResponse_ReadsSwarmFromAFullRawResponse() {
      const string body = "d8:completei12e10:incompletei4e8:intervali900ee";
      var raw = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\n\r\n" + body);

      var r = Announce.ParseHttpResponse(raw);

      Assert.True(r.Accepted);
      Assert.Equal(12, r.Seeders);
      Assert.Equal(4, r.Leechers);
      Assert.Equal(900, r.Interval);
    }

    // ---- helpers ----

    private static Announce.Params BaseParams() => new Announce.Params {
      Tracker = "https://tracker.test/announce",
      QueryTemplate = QBittorrentQuery,
      InfoHashHex = "0102030405060708090a0b0c0d0e0f1011121314",
      PeerId = "-qB5230-abcdef012345",
      Port = "6881",
      Uploaded = 0,
      Downloaded = 0,
      Left = 0,
      TotalSize = 1000000,
      Key = "ABCD1234",
      NumWant = "200",
      Event = "&event=started",
      LocalIp = "192.168.0.2",
      HashUpperCase = false,
    };

    private static byte[] PercentDecodeToBytes(string s) {
      using var ms = new MemoryStream();
      for (var i = 0; i < s.Length; i++) {
        if (s[i] == '%') {
          ms.WriteByte(System.Convert.ToByte(s.Substring(i + 1, 2), 16));
          i += 2;
        }
        else {
          ms.WriteByte((byte) s[i]);
        }
      }
      return ms.ToArray();
    }
  }
}
