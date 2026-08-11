using System.Collections.Generic;
using Seedforger.Wire;
using Xunit;

namespace Seedforger.Tests {

  public class SeederChokeTests {

    [Fact]
    public void UnchokesUpToSlots_AndNothingWhenNobodyInterested() {
      var c = new SeederChoke(4);
      Assert.Empty(c.SelectUnchoked(0));
      Assert.Equal(3, c.SelectUnchoked(3).Count);      // fewer peers than slots
      Assert.Equal(4, c.SelectUnchoked(10).Count);     // capped to slots
    }

    [Fact]
    public void RotationCoversEveryPeerOverTime() {
      var c = new SeederChoke(2);
      const int peers = 5;
      var seen = new HashSet<int>();
      for (var round = 0; round < peers; round++)
        seen.UnionWith(c.SelectUnchoked(peers));
      Assert.Equal(peers, seen.Count); // no peer is starved forever
    }
  }

  public class RelayPieceSourceTests {

    [Fact]
    public void FetchesOnce_ThenServesFromCache() {
      var calls = 0;
      var piece = new byte[32768];
      for (var i = 0; i < piece.Length; i++) piece[i] = (byte) (i & 0xFF);
      var relay = new RelayPieceSource(10, 32768, 320000, idx => { calls++; return piece; });

      Assert.True(relay.HasPiece(3));
      Assert.True(relay.TryReadBlock(3, 0, 16384, out var b0));
      Assert.True(relay.TryReadBlock(3, 16384, 16384, out var b1)); // same piece, cached
      Assert.Equal(1, calls);                    // fetched only once
      Assert.Equal(1, relay.CachedPieces);
      Assert.Equal(piece[..16384], b0);
      Assert.Equal(piece[16384..], b1);
    }

    [Fact]
    public void FailedFetch_ReturnsFalse() {
      var relay = new RelayPieceSource(10, 32768, 320000, _ => null);
      Assert.False(relay.TryReadBlock(0, 0, 16384, out _));
      Assert.False(relay.TryReadBlock(99, 0, 16384, out _)); // out of range
    }
  }
}
