using System;

namespace Seedforger {

  /// <summary>
  /// Tiny bits of the BitTorrent peer wire protocol, so that when a tracker (or a
  /// monitoring peer) actually connects to our advertised port, we look like a
  /// real, complete seeder: we answer the handshake, send a full bitfield, then
  /// choke — connectable and complete, but handing out no data. Never transfers
  /// a byte of real content.
  /// </summary>
  internal static class PeerWire {

    /// <summary>A `bitfield` message (id 5) claiming all <paramref name="pieces"/>
    /// pieces. Spare bits in the final byte are zeroed as the spec requires.</summary>
    internal static byte[] FullBitfieldMessage(int pieces) {
      if (pieces <= 0) return Array.Empty<byte>();
      var bfLen = (pieces + 7) / 8;
      var bf = new byte[bfLen];
      for (var i = 0; i < bfLen; i++) bf[i] = 0xFF;
      var spare = bfLen * 8 - pieces;              // unused low bits of the last byte
      if (spare > 0) bf[bfLen - 1] = (byte) (0xFF << spare);

      var msgLen = 1 + bfLen;                       // id + payload
      var msg = new byte[4 + msgLen];
      msg[0] = (byte) (msgLen >> 24);
      msg[1] = (byte) (msgLen >> 16);
      msg[2] = (byte) (msgLen >> 8);
      msg[3] = (byte) msgLen;
      msg[4] = 5;                                   // bitfield
      Buffer.BlockCopy(bf, 0, msg, 5, bfLen);
      return msg;
    }

    /// <summary>A `choke` message (length 1, id 0).</summary>
    internal static byte[] ChokeMessage() => new byte[] { 0, 0, 0, 1, 0 };
  }
}
