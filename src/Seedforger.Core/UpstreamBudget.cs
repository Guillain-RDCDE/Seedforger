using System;

namespace Seedforger {

  /// <summary>
  /// A shared upstream budget across every engine that registers with it. One
  /// real connection has one uplink: ten torrents can't each upload at the full
  /// line speed, so each active engine is capped to its fair share of the total.
  /// <see cref="Shared"/> is the process-wide line the interface configures from a
  /// connection profile; a campaign gets its own instance.
  /// </summary>
  internal sealed class UpstreamBudget {

    public static UpstreamBudget Shared { get; } = new UpstreamBudget();

    private readonly object gate = new object();
    private int totalKBps;
    private int active;

    public UpstreamBudget(int totalKBps = 0) { TotalKBps = totalKBps; }

    /// <summary>Total upstream in kB/s; 0 means unlimited (the budget is off).</summary>
    public int TotalKBps {
      get => totalKBps;
      set => totalKBps = Math.Max(0, value);
    }

    public bool Enabled => totalKBps > 0;

    /// <summary>How many engines currently share the line.</summary>
    public int ActiveCount { get { lock (gate) return active; } }

    internal void Register() { lock (gate) active++; }

    internal void Unregister() { lock (gate) { if (active > 0) active--; } }

    /// <summary>Caps a per-second upload (bytes) to one engine's fair share of the
    /// total. Returns the request unchanged when the budget is off.</summary>
    internal long CapUpload(long requestedBytesPerSec) {
      var capKBps = totalKBps;
      if (capKBps <= 0) return requestedBytesPerSec;
      int n;
      lock (gate) n = active > 0 ? active : 1;
      var shareBytes = capKBps * 1024L / n;
      return Math.Min(requestedBytesPerSec, shareBytes);
    }
  }
}
