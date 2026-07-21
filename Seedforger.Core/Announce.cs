using System;
using System.IO;
using System.Text;
using Seedforger.BitTorrent;

namespace Seedforger {

  /// <summary>
  /// The tracker-announce core, with **no WinForms dependency** — the part that
  /// actually matters (building the exact announce URL, percent-encoding the
  /// info_hash/peer_id, and reading the tracker's bencoded answer). Extracted from
  /// the RM UserControl so it can be unit-tested on its own. The wire behaviour is
  /// byte-for-byte identical to the legacy code it replaces.
  /// </summary>
  internal static class Announce {

    /// <summary>Everything needed to build one announce URL — plain data, no UI.</summary>
    internal sealed class Params {
      public string Tracker;         // the announce URL (may already carry a query)
      public string QueryTemplate;   // the client's query with {infohash}/{peerid}/… placeholders
      public string InfoHashHex;     // 40-hex-char SHA-1
      public string PeerId;          // already percent-ready peer_id string
      public string Port;
      public long Uploaded;
      public long Downloaded;
      public long Left;
      public long TotalSize;
      public string Key;
      public string NumWant;         // "0" means "let the tracker default to 200"
      public string Event;           // "&event=started" etc. (as the legacy caller passes it)
      public string LocalIp;
      public bool HashUpperCase;
    }

    private static long RoundByDenominator(long value, long denominator) => denominator * (value / denominator);

    /// <summary>Builds the announce URL exactly as the legacy RM path did.</summary>
    internal static string BuildUrl(Params p) {
      var uploaded = "0";
      var up = p.Uploaded;
      if (up > 0) { up = RoundByDenominator(up, 0x4000); uploaded = up.ToString(); }

      var downloaded = "0";
      var down = p.Downloaded;
      if (down > 0) { down = RoundByDenominator(down, 0x10); downloaded = down.ToString(); }

      var leftVal = p.Left;
      if (leftVal > 0) leftVal = p.TotalSize - down;
      var left = leftVal.ToString();

      var url = p.Tracker;
      url += url.Contains("?") ? "&" : "?";

      if (p.Event.Contains("started")) url = url.Replace("&natmapped=1&localip={localip}", "");
      if (!p.Event.Contains("stopped")) url = url.Replace("&trackerid=48", "");
      url += p.QueryTemplate;
      url = url.Replace("{infohash}", EncodeHash(p.InfoHashHex, p.HashUpperCase));
      url = url.Replace("{peerid}", p.PeerId);
      url = url.Replace("{port}", p.Port);
      url = url.Replace("{uploaded}", uploaded);
      url = url.Replace("{downloaded}", downloaded);
      url = url.Replace("{left}", left);
      url = url.Replace("{event}", p.Event);
      var numWant = p.NumWant == "0" && !p.Event.ToLower().Contains("stopped") ? "200" : p.NumWant;
      url = url.Replace("{numwant}", numWant);
      url = url.Replace("{key}", p.Key);
      url = url.Replace("{localip}", p.LocalIp ?? "");
      return url;
    }

    /// <summary>
    /// Percent-encodes a 40-hex-char info_hash (or any hex byte string): each hex
    /// pair becomes one byte, kept verbatim when it's an ASCII letter/digit, else
    /// %XX. Deterministic — identical to the legacy RandomStringGenerator path.
    /// </summary>
    internal static string EncodeHash(string hex, bool upperCase) {
      var raw = new StringBuilder();
      for (var i = 0; i + 1 < hex.Length; i += 2)
        raw.Append((char) Convert.ToUInt16(hex.Substring(i, 2), 16));
      return PercentEncode(raw.ToString(), upperCase);
    }

    /// <summary>The legacy escape rule: keep ASCII alphanumerics, %XX everything else.</summary>
    internal static string PercentEncode(string s, bool upperCase) {
      var result = new StringBuilder(s.Length * 3);
      foreach (var ch in s) {
        if (char.IsLetterOrDigit(ch) && ch < 127) {
          result.Append(ch);
        }
        else {
          var temp = Convert.ToString(ch, 16);
          if (upperCase) temp = temp.ToUpperInvariant();
          result.Append('%').Append(temp.Length == 1 ? "0" + temp : temp);
        }
      }
      return result.ToString();
    }

    /// <summary>Structured reading of a tracker's announce answer.</summary>
    internal sealed class Result {
      public string Failure;         // set when the tracker rejected the announce
      public int Seeders = -1;       // "complete"
      public int Leechers = -1;      // "incomplete"
      public int Interval = -1;
      public int MinInterval = -1;   // "min interval" — the floor a client must not re-announce sooner than
      public int Downloaded = -1;    // "downloaded" (times completed), when present
      public bool Accepted => string.IsNullOrEmpty(Failure);
    }

    /// <summary>Reads complete/incomplete/interval/failure from a parsed dict.</summary>
    internal static Result FromDict(ValueDictionary d) {
      var r = new Result();
      if (d == null) return r;
      if (d.Contains("failure reason")) r.Failure = BEncode.String(d["failure reason"]);
      if (d.Contains("complete")) r.Seeders = BEncode.String(d["complete"]).ParseValidInt(-1);
      if (d.Contains("incomplete")) r.Leechers = BEncode.String(d["incomplete"]).ParseValidInt(-1);
      if (d.Contains("interval")) r.Interval = BEncode.String(d["interval"]).ParseValidInt(-1);
      if (d.Contains("min interval")) r.MinInterval = BEncode.String(d["min interval"]).ParseValidInt(-1);
      if (d.Contains("downloaded")) r.Downloaded = BEncode.String(d["downloaded"]).ParseValidInt(-1);
      return r;
    }

    /// <summary>Parses a raw bencoded announce body (no HTTP framing).</summary>
    internal static Result ParseBody(byte[] bencoded) {
      using var ms = new MemoryStream(bencoded);
      var dict = BEncode.Parse(ms) as ValueDictionary;
      return FromDict(dict);
    }

    /// <summary>Parses a full raw HTTP announce response (headers + body, gzip/chunked aware).</summary>
    internal static Result ParseHttpResponse(byte[] rawResponse) {
      using var ms = new MemoryStream(rawResponse);
      return FromDict(new TrackerResponse(ms).Dict);
    }
  }
}
