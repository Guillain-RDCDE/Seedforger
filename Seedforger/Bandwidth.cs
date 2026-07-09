namespace Seedforger {

  /// <summary>
  /// A shared upstream budget across all running tabs. One real connection has
  /// one uplink: ten torrents can't each upload at the full line speed. When a
  /// connection profile sets <see cref="GlobalUpKBps"/>, each active tab is
  /// capped to its fair share of that total.
  /// </summary>
  internal static class Bandwidth {

    /// <summary>Total upstream in kB/s (0 = unlimited / disabled). Set from the
    /// chosen connection profile.</summary>
    internal static int GlobalUpKBps;

    private static int active;
    private static readonly object gate = new object();

    internal static void RegisterActive() { lock (gate) active++; }
    internal static void UnregisterActive() { lock (gate) { if (active > 0) active--; } }

    internal static int ActiveCount { get { lock (gate) return active; } }

    /// <summary>Caps a per-second upload (bytes) to this tab's fair share of the
    /// global upstream. Returns the request unchanged when the budget is off.</summary>
    internal static long CapUpload(long requestedBytesPerSec) {
      var capKBps = GlobalUpKBps;
      if (capKBps <= 0) return requestedBytesPerSec;
      int n;
      lock (gate) n = active > 0 ? active : 1;
      var shareBytes = (long) capKBps * 1024L / n;
      return requestedBytesPerSec < shareBytes ? requestedBytesPerSec : shareBytes;
    }
  }
}
