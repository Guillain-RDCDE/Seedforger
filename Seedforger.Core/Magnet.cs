using System;
using System.Collections.Generic;

namespace Seedforger {

  internal sealed class MagnetInfo {
    internal byte[] InfoHash;                         // 20 raw bytes
    internal string HashHex;                          // 40 hex chars
    internal string Name;
    internal readonly List<string> Trackers = new List<string>();
  }

  /// <summary>Parses magnet links (xt=urn:btih:…, dn, tr). Pure and testable.</summary>
  internal static class Magnet {

    internal static bool IsMagnet(string uri) =>
      uri != null && uri.TrimStart().StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns the parsed magnet, or null if it isn't a valid btih magnet.</summary>
    internal static MagnetInfo Parse(string uri) {
      if (!IsMagnet(uri)) return null;
      var query = uri.Trim().Substring("magnet:?".Length);
      var result = new MagnetInfo();

      foreach (var pair in query.Split('&')) {
        var eq = pair.IndexOf('=');
        if (eq <= 0) continue;
        var key = pair.Substring(0, eq).ToLowerInvariant();
        var value = Uri.UnescapeDataString(pair.Substring(eq + 1));

        if (key == "xt" && value.StartsWith("urn:btih:", StringComparison.OrdinalIgnoreCase)) {
          var h = value.Substring("urn:btih:".Length).Trim();
          result.InfoHash = HashToBytes(h);
          if (result.InfoHash != null) result.HashHex = ToHex(result.InfoHash);
        }
        else if (key == "dn") {
          result.Name = value;
        }
        else if (key == "tr" && value.Length > 0) {
          result.Trackers.Add(value);
        }
      }

      return result.InfoHash != null ? result : null;
    }

    /// <summary>Decodes a btih value (40-char hex or 32-char base32) to 20 bytes.</summary>
    internal static byte[] HashToBytes(string s) {
      if (string.IsNullOrEmpty(s)) return null;
      s = s.Trim();
      if (s.Length == 40 && IsHex(s)) return FromHex(s);
      if (s.Length == 32) return FromBase32(s);
      return null;
    }

    private static bool IsHex(string s) {
      foreach (var c in s)
        if (!Uri.IsHexDigit(c)) return false;
      return true;
    }

    private static byte[] FromHex(string s) {
      var b = new byte[s.Length / 2];
      for (var i = 0; i < b.Length; i++)
        b[i] = Convert.ToByte(s.Substring(i * 2, 2), 16);
      return b;
    }

    // RFC 4648 base32 (A-Z, 2-7), no padding, 32 chars -> 20 bytes.
    private static byte[] FromBase32(string s) {
      const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
      s = s.ToUpperInvariant();
      var output = new List<byte>(20);
      int buffer = 0, bitsLeft = 0;
      foreach (var c in s) {
        var v = alphabet.IndexOf(c);
        if (v < 0) return null; // invalid base32 char
        buffer = (buffer << 5) | v;
        bitsLeft += 5;
        if (bitsLeft >= 8) {
          bitsLeft -= 8;
          output.Add((byte) ((buffer >> bitsLeft) & 0xFF));
        }
      }
      return output.Count == 20 ? output.ToArray() : null;
    }

    private static string ToHex(byte[] bytes) {
      var c = new char[bytes.Length * 2];
      const string hex = "0123456789ABCDEF";
      for (var i = 0; i < bytes.Length; i++) {
        c[i * 2] = hex[bytes[i] >> 4];
        c[i * 2 + 1] = hex[bytes[i] & 0xF];
      }
      return new string(c);
    }
  }
}
