using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Seedforger.Wire;
using Xunit;

namespace Seedforger.Tests {

  public class PeerProtocolTests {

    [Fact]
    public void Handshake_RoundTrips() {
      var ih = New(20, 1); var pid = New(20, 2);
      var hs = PeerProtocol.BuildHandshake(ih, pid);
      Assert.Equal(68, hs.Length);
      Assert.True(PeerProtocol.TryParseHandshake(hs, out var p));
      Assert.Equal(ih, p.InfoHash);
      Assert.Equal(pid, p.PeerId);
    }

    [Fact]
    public void Message_RoundTripsThroughStream() {
      using var ms = new MemoryStream();
      var req = PeerProtocol.Request(3, 16384, 16384);
      ms.Write(req, 0, req.Length);
      var ka = PeerProtocol.KeepAlive();
      ms.Write(ka, 0, ka.Length);
      var piece = PeerProtocol.PieceMsg(3, 0, new byte[] { 9, 8, 7 });
      ms.Write(piece, 0, piece.Length);
      ms.Position = 0;

      Assert.True(PeerProtocol.TryReadMessage(ms, out var f1));
      Assert.Equal(MsgId.Request, f1.Id);
      Assert.Equal(3, PeerProtocol.ReadInt(f1.Payload, 0));
      Assert.Equal(16384, PeerProtocol.ReadInt(f1.Payload, 8));

      Assert.True(PeerProtocol.TryReadMessage(ms, out var f2));
      Assert.True(f2.IsKeepAlive);

      Assert.True(PeerProtocol.TryReadMessage(ms, out var f3));
      Assert.Equal(MsgId.Piece, f3.Id);
      Assert.Equal(new byte[] { 9, 8, 7 }, f3.Payload[8..]);
    }

    [Fact]
    public void FullBitfield_ClearsSpareBits() {
      var m = PeerProtocol.FullBitfield(12); // <len><id=5><2 bytes>
      Assert.Equal(5, m[4]);
      Assert.Equal(0xFF, m[5]);
      Assert.Equal(0xF0, m[6]);
    }

    private static byte[] New(int n, byte fill) { var b = new byte[n]; Array.Fill(b, fill); return b; }
  }

  public class GovernorTests {
    [Fact]
    public void CapsAnnouncedToRealServedTimesPeers() {
      var g = new Governor();
      g.AddServed(1000);
      Assert.Equal(5000, g.CapAnnounced(1_000_000, 5)); // 1000 * 5
      Assert.Equal(400, g.CapAnnounced(400, 5));        // proposal below ceiling
      Assert.Equal(1000, g.CapAnnounced(999_999, 0));   // peers<1 treated as 1
    }
  }

  public class FilePieceSourceTests {

    [Fact]
    public void VerifiesAndServesRealBlocks() {
      var (path, hashes, content, pieceLen, total) = MakeFile(40000, 16384);
      try {
        using var src = new FilePieceSource(path, pieceLen, total, hashes);
        Assert.Equal(3, src.PieceCount);
        Assert.True(src.HasPiece(0));
        Assert.True(src.HasPiece(2)); // short last piece

        Assert.True(src.TryReadBlock(0, 0, 16384, out var b0));
        Assert.Equal(content[..16384], b0);
        Assert.True(src.TryReadBlock(2, 0, 40000 - 2 * 16384, out var b2));
        Assert.Equal(content[(2 * 16384)..], b2);

        Assert.False(src.TryReadBlock(0, 16000, 16384, out _)); // past piece end
      }
      finally { File.Delete(path); }
    }

    [Fact]
    public void RejectsPieceWithWrongHash() {
      var (path, hashes, _, pieceLen, total) = MakeFile(20000, 16384);
      hashes[1][0] ^= 0xFF; // corrupt the expected hash of piece 1
      try {
        using var src = new FilePieceSource(path, pieceLen, total, hashes);
        Assert.True(src.HasPiece(0));
        Assert.False(src.HasPiece(1)); // won't advertise/serve what doesn't verify
      }
      finally { File.Delete(path); }
    }

    internal static (string path, byte[][] hashes, byte[] content, long pieceLen, long total) MakeFile(int size, int pieceLen) {
      var content = new byte[size];
      for (var i = 0; i < size; i++) content[i] = (byte) ((i * 31 + 7) & 0xFF);
      var path = Path.Combine(Path.GetTempPath(), "sf_test_" + Guid.NewGuid().ToString("N") + ".bin");
      File.WriteAllBytes(path, content);
      var n = (size + pieceLen - 1) / pieceLen;
      var hashes = new byte[n][];
      using var sha = SHA1.Create();
      for (var i = 0; i < n; i++) {
        var start = i * pieceLen;
        var len = Math.Min(pieceLen, size - start);
        hashes[i] = sha.ComputeHash(content, start, len);
      }
      return (path, hashes, content, pieceLen, size);
    }
  }

  /// <summary>End-to-end: a real seeder (FilePieceSource) serves a real, hash-valid
  /// piece to a peer over a loopback TCP connection (proves stages A and D).</summary>
  public class PeerTransferIntegrationTests {

    [Fact]
    public void ClientDownloadsVerifiedPieceFromRealSeeder() {
      var (path, hashes, content, pieceLen, total) = FilePieceSourceTests.MakeFile(40000, 16384);
      var infoHash = Fill(20, 0xAB);
      var governor = new Governor();

      var listener = new TcpListener(IPAddress.Loopback, 0);
      listener.Start();
      var port = ((IPEndPoint) listener.LocalEndpoint).Port;

      var serverTask = Task.Run(() => {
        using var src = new FilePieceSource(path, pieceLen, total, hashes);
        using var client = listener.AcceptTcpClient();
        using var ns = client.GetStream();
        new PeerSession(infoHash, Fill(20, 0x01), src, governor).Serve(ns);
      });

      try {
        var got = PeerClient.DownloadPiece("127.0.0.1", port, infoHash, Fill(20, 0x02),
          0, 16384, hashes[0]);
        Assert.NotNull(got);
        Assert.Equal(content[..16384], got);
        serverTask.Wait(5000);
        Assert.True(governor.RealServed >= 16384, $"served {governor.RealServed}");
      }
      finally {
        listener.Stop();
        File.Delete(path);
      }
    }

    private static byte[] Fill(int n, byte v) { var b = new byte[n]; Array.Fill(b, v); return b; }
  }
}
