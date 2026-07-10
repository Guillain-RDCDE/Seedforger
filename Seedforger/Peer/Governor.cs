using System;
using System.Threading;

namespace Seedforger.Wire {

  /// <summary>
  /// Keeps the *announced* upload within a believable multiple of the bytes we
  /// actually served on the wire. A monitoring peer only ever sees its own
  /// connection, so a total of (real served) × (plausible peer count) can't be
  /// refuted by any single observer — but claiming far beyond what you really
  /// served can. This enforces that ceiling.
  /// </summary>
  internal sealed class Governor {

    private long realServed;

    /// <summary>Record real block bytes actually written to a peer.</summary>
    internal void AddServed(long bytes) {
      if (bytes > 0) Interlocked.Add(ref realServed, bytes);
    }

    internal long RealServed => Interlocked.Read(ref realServed);

    /// <summary>
    /// Cap a proposed announced-upload total to real-served × plausiblePeers.
    /// plausiblePeers is bounded by the swarm (≈ leecher count) so it stays
    /// credible. Never returns more than <paramref name="proposed"/>.
    /// </summary>
    internal long CapAnnounced(long proposed, int plausiblePeers) {
      var k = plausiblePeers < 1 ? 1 : plausiblePeers;
      var ceiling = RealServed * k;
      return proposed < ceiling ? proposed : ceiling;
    }
  }
}
