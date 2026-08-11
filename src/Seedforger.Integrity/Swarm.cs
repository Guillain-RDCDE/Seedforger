using System.Collections.Generic;

namespace Seedforger.Integrity {

  /// <summary>
  /// One peer's reality over a single announce window. The fields split cleanly
  /// into two worlds: what actually happened on the wire (<see cref="TrueUp"/>,
  /// <see cref="TrueDown"/>) and what the peer *told the tracker* happened
  /// (<see cref="DeclaredUp"/>, <see cref="DeclaredDown"/>). A tracker only ever
  /// sees the declared numbers plus <see cref="CorroboratedUp"/> — the sliver of
  /// the claim that its monitoring peers actually received. The truth fields and
  /// <see cref="IsCheater"/> exist only to score a detector; no detector reads them.
  /// This is the whole thesis in a data structure: the reported number is not the
  /// measured number.
  /// </summary>
  public sealed class SwarmPeer {
    public int Id;
    public bool IsSeeder;

    /// <summary>Evaluation label only — never an input to any detector.</summary>
    public bool IsCheater;

    // Ground truth (private to the wire, invisible to the tracker).
    public long TrueUp;
    public long TrueDown;

    // Self-reported to the tracker (the only figures a naive tracker trusts).
    public long DeclaredUp;
    public long DeclaredDown;

    /// <summary>
    /// Bytes of this peer's <see cref="DeclaredUp"/> that the tracker's monitoring
    /// peers actually received. Observable by the tracker, noisy, and — crucially —
    /// something the peer can neither read nor fabricate. It is the hidden variable
    /// of the game.
    /// </summary>
    public long CorroboratedUp;

    public double WindowSeconds;

    /// <summary>Physical uplink ceiling implied by the client handshake / link.</summary>
    public double LinkCapacityBytesPerSec;
  }

  /// <summary>A swarm snapshot over one announce window.</summary>
  public sealed class Swarm {
    public List<SwarmPeer> Peers = new List<SwarmPeer>();

    /// <summary>
    /// Coverage c ∈ [0,1]: the expected fraction of a peer's genuine upload that
    /// lands on a monitoring peer. It is a property of the tracker's deployment,
    /// known to the tracker, unknown to the peer.
    /// </summary>
    public double Coverage;

    public int Count => Peers.Count;
  }
}
