using System;

namespace Seedforger {

  /// <summary>
  /// Client-specific peer-id tail generation. Some clients don't use a plain
  /// random tail: Transmission makes the last character a checksum of the
  /// preceding random characters, so a validator can spot a forged id. We
  /// reproduce it exactly so our Transmission ids pass that check.
  /// </summary>
  internal static class PeerId {

    // Transmission's character pool (libtransmission tr_peerIdInit): 0-9 a-z A-Z.
    private const string Pool = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>
    /// A 12-char Transmission tail: 11 random pool chars + 1 checksum char, where
    /// checksum = Pool[(sum of the 11 random indices) mod 62].
    /// </summary>
    internal static string TransmissionTail(Random rand) {
      var chars = new char[12];
      var sum = 0;
      for (var i = 0; i < 11; i++) {
        var v = rand.Next(Pool.Length);
        sum += v;
        chars[i] = Pool[v];
      }
      chars[11] = Pool[sum % Pool.Length];
      return new string(chars);
    }

    /// <summary>True if <paramref name="tail"/> is a valid Transmission tail
    /// (12 pool chars whose last is the checksum of the first 11).</summary>
    internal static bool IsValidTransmissionTail(string tail) {
      if (tail == null || tail.Length != 12) return false;
      var sum = 0;
      for (var i = 0; i < 12; i++) {
        var idx = Pool.IndexOf(tail[i]);
        if (idx < 0) return false;
        if (i < 11) sum += idx;
      }
      return Pool.IndexOf(tail[11]) == sum % Pool.Length;
    }
  }
}
