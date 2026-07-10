using System;
using System.Collections.Generic;

namespace Seedforger.Wire {

  /// <summary>
  /// Serves pieces we don't hold by fetching them on demand from a real seeder in
  /// the swarm (the <c>fetch</c> delegate — typically <see cref="PeerClient"/>),
  /// verifying them there, then relaying and caching. We become a caching proxy
  /// for the swarm: never storing the whole file, paying bandwidth only for what
  /// is actually requested, yet handing genuine hash-valid data to whoever asks.
  /// </summary>
  internal sealed class RelayPieceSource : IPieceSource {

    private readonly Func<int, byte[]> fetch;               // returns a verified piece, or null
    private readonly Dictionary<int, byte[]> cache = new Dictionary<int, byte[]>();
    private readonly object gate = new object();

    internal RelayPieceSource(int pieceCount, long pieceLength, long totalLength, Func<int, byte[]> fetch) {
      PieceCount = pieceCount;
      PieceLength = pieceLength;
      TotalLength = totalLength;
      this.fetch = fetch;
    }

    public int PieceCount { get; }
    public long PieceLength { get; }
    public long TotalLength { get; }

    /// <summary>A relay advertises the whole torrent and fetches on demand.</summary>
    public bool HasPiece(int index) => index >= 0 && index < PieceCount;

    public bool TryReadBlock(int index, int begin, int length, out byte[] data) {
      data = null;
      if (index < 0 || index >= PieceCount || begin < 0 || length <= 0) return false;

      byte[] piece;
      lock (gate) cache.TryGetValue(index, out piece);

      if (piece == null) {
        piece = fetch?.Invoke(index);          // network fetch outside the lock
        if (piece == null) return false;
        lock (gate) cache[index] = piece;
      }

      if (begin + length > piece.Length) return false;
      var block = new byte[length];
      Buffer.BlockCopy(piece, begin, block, 0, length);
      data = block;
      return true;
    }

    internal int CachedPieces { get { lock (gate) return cache.Count; } }
  }
}
