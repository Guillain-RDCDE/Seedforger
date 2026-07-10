using System;
using System.IO;
using System.Security.Cryptography;

namespace Seedforger.Wire {

  /// <summary>Supplies real, hash-valid block data to serve to peers.</summary>
  internal interface IPieceSource {
    int PieceCount { get; }
    long PieceLength { get; }
    long TotalLength { get; }
    /// <summary>Do we have a verified copy of this piece to serve?</summary>
    bool HasPiece(int index);
    /// <summary>Reads a block; returns false if out of range or unavailable.</summary>
    bool TryReadBlock(int index, int begin, int length, out byte[] data);
  }

  /// <summary>
  /// Serves real content from a local file, verifying each piece against its
  /// SHA-1 from the .torrent before advertising/serving it. This is what makes a
  /// monitoring peer's `request` return genuine, hash-valid data.
  /// </summary>
  internal sealed class FilePieceSource : IPieceSource, IDisposable {

    private readonly FileStream file;
    private readonly byte[][] hashes;      // 20-byte SHA-1 per piece
    private readonly bool?[] verified;     // lazy verification cache
    private readonly object gate = new object();

    internal FilePieceSource(string path, long pieceLength, long totalLength, byte[][] pieceHashes) {
      if (pieceLength <= 0) throw new ArgumentException("pieceLength");
      PieceLength = pieceLength;
      TotalLength = totalLength;
      hashes = pieceHashes ?? throw new ArgumentNullException(nameof(pieceHashes));
      verified = new bool?[hashes.Length];
      file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    /// <summary>Splits a concatenated `pieces` blob (20 bytes each) into an array.</summary>
    internal static byte[][] SplitHashes(byte[] concatenated) {
      var n = concatenated.Length / 20;
      var arr = new byte[n][];
      for (var i = 0; i < n; i++) {
        arr[i] = new byte[20];
        Buffer.BlockCopy(concatenated, i * 20, arr[i], 0, 20);
      }
      return arr;
    }

    public int PieceCount => hashes.Length;
    public long PieceLength { get; }
    public long TotalLength { get; }

    private long LengthOfPiece(int index) {
      var start = (long) index * PieceLength;
      var remaining = TotalLength - start;
      return remaining < PieceLength ? remaining : PieceLength;
    }

    public bool HasPiece(int index) {
      if (index < 0 || index >= hashes.Length) return false;
      lock (gate) {
        if (verified[index].HasValue) return verified[index].Value;
        var ok = VerifyPiece(index);
        verified[index] = ok;
        return ok;
      }
    }

    private bool VerifyPiece(int index) {
      try {
        var len = LengthOfPiece(index);
        if (len <= 0) return false;
        var buf = new byte[len];
        file.Seek((long) index * PieceLength, SeekOrigin.Begin);
        var got = 0;
        while (got < len) {
          var r = file.Read(buf, got, (int) (len - got));
          if (r <= 0) return false;
          got += r;
        }
        using var sha = SHA1.Create();
        var h = sha.ComputeHash(buf);
        for (var i = 0; i < 20; i++) if (h[i] != hashes[index][i]) return false;
        return true;
      }
      catch { return false; }
    }

    public bool TryReadBlock(int index, int begin, int length, out byte[] data) {
      data = null;
      if (index < 0 || index >= hashes.Length || begin < 0 || length <= 0) return false;
      var pieceLen = LengthOfPiece(index);
      if (begin + length > pieceLen) return false;
      if (!HasPiece(index)) return false;
      try {
        var buf = new byte[length];
        lock (gate) {
          file.Seek((long) index * PieceLength + begin, SeekOrigin.Begin);
          var got = 0;
          while (got < length) {
            var r = file.Read(buf, got, length - got);
            if (r <= 0) return false;
            got += r;
          }
        }
        data = buf;
        return true;
      }
      catch { return false; }
    }

    public void Dispose() => file?.Dispose();
  }
}
