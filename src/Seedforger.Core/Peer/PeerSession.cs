using System;
using System.IO;
using System.Threading;

namespace Seedforger.Wire {

  /// <summary>
  /// One inbound peer connection, served for real: handshake, a bitfield of the
  /// pieces we actually hold, then answer `request`s with genuine block data from
  /// the <see cref="IPieceSource"/> — while choking by default and rate-limiting,
  /// so a monitoring peer receives valid, hash-checkable data at a modest rate.
  /// Runs over any duplex <see cref="Stream"/> so it can be tested over loopback.
  /// </summary>
  internal sealed class PeerSession {

    private readonly byte[] infoHash;
    private readonly byte[] peerId;
    private readonly IPieceSource source;
    private readonly Governor governor;
    private readonly int maxServeBytesPerSec;

    internal PeerSession(byte[] infoHash, byte[] peerId, IPieceSource source,
      Governor governor, int maxServeBytesPerSec = 0) {
      this.infoHash = infoHash;
      this.peerId = peerId;
      this.source = source;
      this.governor = governor;
      this.maxServeBytesPerSec = maxServeBytesPerSec;
    }

    internal void Serve(Stream stream, CancellationToken ct = default) {
      // Their handshake first (we're the receiving side).
      var raw = PeerProtocol.ReadExact(stream, PeerProtocol.HandshakeLength);
      if (raw == null || !PeerProtocol.TryParseHandshake(raw, out var hs)) return;
      if (!SameHash(hs.InfoHash, infoHash)) return;

      stream.Write(PeerProtocol.BuildHandshake(infoHash, peerId, ExtensionMessages.ReservedWithExtensions()),
        0, PeerProtocol.HandshakeLength);
      var bf = BuildBitfield();
      stream.Write(bf, 0, bf.Length);
      // If the peer advertises the extension protocol, reply with our extended
      // handshake (advertising ut_pex / ut_metadata) like a modern client.
      if (hs.Reserved != null && hs.Reserved.Length == 8 && (hs.Reserved[5] & 0x10) != 0) {
        var eh = ExtensionMessages.ExtendedHandshake();
        stream.Write(eh, 0, eh.Length);
      }
      stream.Flush();

      var choked = true;
      var windowStart = 0L;         // monotonic ms (from Environment.TickCount64)
      var servedInWindow = 0;

      while (!ct.IsCancellationRequested) {
        if (!PeerProtocol.TryReadMessage(stream, out var f)) break;
        if (f.IsKeepAlive) continue;

        switch (f.Id) {
          case MsgId.Interested:
            if (choked) { choked = false; var u = PeerProtocol.Unchoke(); stream.Write(u, 0, u.Length); stream.Flush(); }
            break;
          case MsgId.NotInterested:
            break;
          case MsgId.Request:
            if (choked || f.Payload == null || f.Payload.Length < 12) break;
            var index = PeerProtocol.ReadInt(f.Payload, 0);
            var begin = PeerProtocol.ReadInt(f.Payload, 4);
            var length = PeerProtocol.ReadInt(f.Payload, 8);
            if (length <= 0 || length > 1 << 17) break; // sane block size
            if (!source.TryReadBlock(index, begin, length, out var block)) break;
            RateLimit(ref windowStart, ref servedInWindow, length);
            var msg = PeerProtocol.PieceMsg(index, begin, block);
            stream.Write(msg, 0, msg.Length); stream.Flush();
            governor?.AddServed(length);
            break;
          default:
            break; // choke/unchoke/have/cancel/port/extended: ignore for a pure seeder
        }
      }
    }

    private byte[] BuildBitfield() {
      var n = source.PieceCount;
      var bfLen = (n + 7) / 8;
      var bf = new byte[bfLen];
      for (var i = 0; i < n; i++)
        if (source.HasPiece(i)) bf[i / 8] |= (byte) (0x80 >> (i % 8));
      return PeerProtocol.Message(MsgId.Bitfield, bf);
    }

    private void RateLimit(ref long windowStart, ref int served, int bytes) {
      if (maxServeBytesPerSec <= 0) return;
      var now = Environment.TickCount64;
      if (now - windowStart >= 1000) { windowStart = now; served = 0; }
      served += bytes;
      if (served > maxServeBytesPerSec) {
        var wait = (int) (1000 - (now - windowStart));
        if (wait > 0) Thread.Sleep(wait);
        windowStart = Environment.TickCount64;
        served = 0;
      }
    }

    private static bool SameHash(byte[] a, byte[] b) {
      if (a == null || b == null || a.Length != b.Length) return false;
      for (var i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
      return true;
    }
  }
}
