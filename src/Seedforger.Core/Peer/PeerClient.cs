using System;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;

namespace Seedforger.Wire {

  /// <summary>
  /// Outbound peer: connect, handshake, and pull a whole piece in blocks, then
  /// verify it against its SHA-1. Used to relay pieces on demand (stage B) and to
  /// prove a verifiable node-to-node transfer (stage D).
  /// </summary>
  internal static class PeerClient {

    private const int BlockSize = 16 * 1024;

    /// <summary>Downloads a piece over an already-connected duplex stream. Returns
    /// the verified bytes, or null on failure/hash-mismatch.</summary>
    internal static byte[] DownloadPiece(Stream stream, byte[] infoHash, byte[] peerId,
      int index, int pieceLength, byte[] expectedHash, int timeoutMs = 8000) {

      stream.Write(PeerProtocol.BuildHandshake(infoHash, peerId), 0, PeerProtocol.HandshakeLength);
      stream.Flush();
      var raw = PeerProtocol.ReadExact(stream, PeerProtocol.HandshakeLength);
      if (raw == null || !PeerProtocol.TryParseHandshake(raw, out var hs)) return null;
      if (!Same(hs.InfoHash, infoHash)) return null;

      var interested = PeerProtocol.Interested();
      stream.Write(interested, 0, interested.Length);
      stream.Flush();

      var piece = new byte[pieceLength];
      var received = 0;
      var requestedUpTo = 0;
      var unchoked = false;

      // Prime a couple of requests once unchoked; keep a small pipeline.
      void MaybeRequest() {
        while (unchoked && requestedUpTo < pieceLength) {
          var len = Math.Min(BlockSize, pieceLength - requestedUpTo);
          var r = PeerProtocol.Request(index, requestedUpTo, len);
          stream.Write(r, 0, r.Length);
          requestedUpTo += len;
        }
        stream.Flush();
      }

      var deadline = Environment.TickCount64 + timeoutMs;
      while (received < pieceLength && Environment.TickCount64 < deadline) {
        if (!PeerProtocol.TryReadMessage(stream, out var f)) break;
        if (f.IsKeepAlive) continue;
        switch (f.Id) {
          case MsgId.Unchoke:
            unchoked = true; MaybeRequest(); break;
          case MsgId.Choke:
            unchoked = false; break;
          case MsgId.Bitfield:
          case MsgId.Have:
            break;
          case MsgId.Piece:
            if (f.Payload == null || f.Payload.Length < 8) break;
            var pi = PeerProtocol.ReadInt(f.Payload, 0);
            var begin = PeerProtocol.ReadInt(f.Payload, 4);
            var blockLen = f.Payload.Length - 8;
            if (pi == index && begin >= 0 && begin + blockLen <= pieceLength) {
              Buffer.BlockCopy(f.Payload, 8, piece, begin, blockLen);
              received += blockLen;
            }
            break;
        }
      }

      if (received < pieceLength) return null;
      using var sha = SHA1.Create();
      var h = sha.ComputeHash(piece);
      if (expectedHash != null) {
        if (h.Length != expectedHash.Length) return null;
        for (var i = 0; i < h.Length; i++) if (h[i] != expectedHash[i]) return null;
      }
      return piece;
    }

    /// <summary>Connects to host:port and downloads/verifies a piece.</summary>
    internal static byte[] DownloadPiece(string host, int port, byte[] infoHash, byte[] peerId,
      int index, int pieceLength, byte[] expectedHash, int timeoutMs = 8000) {
      using var tcp = new TcpClient();
      var ar = tcp.BeginConnect(host, port, null, null);
      if (!ar.AsyncWaitHandle.WaitOne(timeoutMs)) return null;
      tcp.EndConnect(ar);
      using var ns = tcp.GetStream();
      ns.ReadTimeout = timeoutMs;
      ns.WriteTimeout = timeoutMs;
      return DownloadPiece(ns, infoHash, peerId, index, pieceLength, expectedHash, timeoutMs);
    }

    private static bool Same(byte[] a, byte[] b) {
      if (a == null || b == null || a.Length != b.Length) return false;
      for (var i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
      return true;
    }
  }
}
