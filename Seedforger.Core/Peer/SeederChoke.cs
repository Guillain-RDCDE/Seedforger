using System;
using System.Collections.Generic;

namespace Seedforger.Wire {

  /// <summary>
  /// A seeder's choking policy. A seeder has nothing to download, so it can't use
  /// tit-for-tat; real clients instead **round-robin** a few unchoke slots across
  /// the interested peers and rotate an optimistic slot periodically. This picks,
  /// each round, which interested peers are unchoked — so no single peer is
  /// unchoked forever and coverage rotates like a real client.
  /// </summary>
  internal sealed class SeederChoke {

    private readonly int slots;
    private int rotation;

    internal SeederChoke(int slots = 4) {
      this.slots = Math.Max(1, slots);
    }

    /// <summary>Indices (into the ordered interested-peer list) that are unchoked
    /// this round. Advances the rotation so the next call shifts the window.</summary>
    internal HashSet<int> SelectUnchoked(int interestedCount) {
      var result = new HashSet<int>();
      if (interestedCount <= 0) return result;
      var take = Math.Min(slots, interestedCount);
      for (var i = 0; i < take; i++)
        result.Add((rotation + i) % interestedCount);
      rotation = (rotation + 1) % interestedCount;
      return result;
    }
  }
}
