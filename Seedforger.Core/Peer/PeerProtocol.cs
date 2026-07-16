using System;
using System.IO;

namespace Seedforger.Wire {

  internal enum MsgId : byte {
    Choke = 0, Unchoke = 1, Interested = 2, NotInterested = 3,
    Have = 4, Bitfield = 5, Request = 6, Piece = 7, Cancel = 8,
    Port = 9, Extended = 20, KeepAlive = 255, // KeepAlive is synthetic (len 0)
  }

  /// <summary>
  /// Pure framing/parsing of the BitTorrent peer wire protocol (BEP 3): the
  /// 68-byte handshake and length-prefixed messages. No sockets here, so it can
  /// be unit-tested against byte arrays and streams.
  /// </summary>
  internal static class PeerProtocol {

    internal const string Pstr = "BitTorrent protocol";
    internal const int HandshakeLength = 68; // 1 + 19 + 8 + 20 + 20

    // ---- Handshake ----

    internal static byte[] BuildHandshake(byte[] infoHash, byte[] peerId, byte[] reserved = null) {
      if (infoHash == null || infoHash.Length != 20) throw new ArgumentException("infoHash must be 20 bytes");
      if (peerId == null || peerId.Length != 20) throw new ArgumentException("peerId must be 20 bytes");
      var buf = new byte[HandshakeLength];
      var p = 0;
      buf[p++] = (byte) Pstr.Length;
      foreach (var c in Pstr) buf[p++] = (byte) c;
      // 8 reserved bytes (all zero by default; caller may advertise extensions)
      if (reserved != null && reserved.Length == 8) Buffer.BlockCopy(reserved, 0, buf, p, 8);
      p += 8;
      Buffer.BlockCopy(infoHash, 0, buf, p, 20); p += 20;
      Buffer.BlockCopy(peerId, 0, buf, p, 20);
      return buf;
    }

    internal struct Handshake {
      internal byte[] Reserved, InfoHash, PeerId;
    }

    internal static bool TryParseHandshake(byte[] b, out Handshake hs) {
      hs = default;
      if (b == null || b.Length < HandshakeLength) return false;
      if (b[0] != Pstr.Length) return false;
      for (var i = 0; i < Pstr.Length; i++)
        if (b[1 + i] != (byte) Pstr[i]) return false;
      hs.Reserved = Slice(b, 20, 8);
      hs.InfoHash = Slice(b, 28, 20);
      hs.PeerId = Slice(b, 48, 20);
      return true;
    }

    // ---- Messages: <length(4, big-endian)><id(1)><payload> ; keep-alive = <0000> ----

    internal static byte[] Message(MsgId id, byte[] payload = null) {
      var plen = payload?.Length ?? 0;
      var len = 1 + plen;
      var m = new byte[4 + len];
      WriteInt(m, 0, len);
      m[4] = (byte) id;
      if (plen > 0) Buffer.BlockCopy(payload, 0, m, 5, plen);
      return m;
    }

    internal static byte[] KeepAlive() => new byte[] { 0, 0, 0, 0 };
    internal static byte[] Choke() => Message(MsgId.Choke);
    internal static byte[] Unchoke() => Message(MsgId.Unchoke);
    internal static byte[] Interested() => Message(MsgId.Interested);
    internal static byte[] NotInterested() => Message(MsgId.NotInterested);

    internal static byte[] Have(int pieceIndex) {
      var p = new byte[4]; WriteInt(p, 0, pieceIndex);
      return Message(MsgId.Have, p);
    }

    internal static byte[] Request(int index, int begin, int length) {
      var p = new byte[12];
      WriteInt(p, 0, index); WriteInt(p, 4, begin); WriteInt(p, 8, length);
      return Message(MsgId.Request, p);
    }

    internal static byte[] PieceMsg(int index, int begin, byte[] block) {
      var p = new byte[8 + block.Length];
      WriteInt(p, 0, index); WriteInt(p, 4, begin);
      Buffer.BlockCopy(block, 0, p, 8, block.Length);
      return Message(MsgId.Piece, p);
    }

    /// <summary>Bitfield claiming all <paramref name="pieces"/> pieces (spare
    /// low bits of the final byte are zeroed as the spec requires).</summary>
    internal static byte[] FullBitfield(int pieces) {
      var bfLen = (pieces + 7) / 8;
      var bf = new byte[bfLen];
      for (var i = 0; i < bfLen; i++) bf[i] = 0xFF;
      var spare = bfLen * 8 - pieces;
      if (bfLen > 0 && spare > 0) bf[bfLen - 1] = (byte) (0xFF << spare);
      return Message(MsgId.Bitfield, bf);
    }

    // ---- Stream helpers ----

    /// <summary>Reads exactly n bytes or returns null on EOF/short read.</summary>
    internal static byte[] ReadExact(Stream s, int n) {
      var buf = new byte[n];
      var got = 0;
      while (got < n) {
        var r = s.Read(buf, got, n - got);
        if (r <= 0) return null;
        got += r;
      }
      return buf;
    }

    internal struct Frame { internal MsgId Id; internal byte[] Payload; internal bool IsKeepAlive; }

    /// <summary>Reads one length-prefixed message from the stream. Returns false
    /// on EOF. Keep-alives come back with <see cref="Frame.IsKeepAlive"/>.</summary>
    internal static bool TryReadMessage(Stream s, out Frame frame, int maxLen = 1 << 20) {
      frame = default;
      var lenBytes = ReadExact(s, 4);
      if (lenBytes == null) return false;
      var len = ReadInt(lenBytes, 0);
      if (len == 0) { frame.IsKeepAlive = true; return true; }
      if (len < 0 || len > maxLen) return false;
      var body = ReadExact(s, len);
      if (body == null) return false;
      frame.Id = (MsgId) body[0];
      frame.Payload = Slice(body, 1, len - 1);
      return true;
    }

    // ---- primitives ----

    internal static void WriteInt(byte[] b, int off, int v) {
      b[off] = (byte) (v >> 24); b[off + 1] = (byte) (v >> 16);
      b[off + 2] = (byte) (v >> 8); b[off + 3] = (byte) v;
    }

    internal static int ReadInt(byte[] b, int off) =>
      (b[off] << 24) | (b[off + 1] << 16) | (b[off + 2] << 8) | b[off + 3];

    private static byte[] Slice(byte[] src, int off, int len) {
      var d = new byte[len];
      Buffer.BlockCopy(src, off, d, 0, len);
      return d;
    }
  }
}
