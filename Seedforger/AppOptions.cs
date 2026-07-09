namespace Seedforger {

  /// <summary>
  /// Process-wide runtime options, persisted via the settings store.
  /// Kept separate from per-torrent state so every tab shares the same choices.
  /// </summary>
  internal static class AppOptions {

    /// <summary>
    /// When true, upload/download progress is shaped with a ramp-up and smooth
    /// mean-reverting variation (see <see cref="SpeedShaper"/>) instead of a flat
    /// rate, which is far less detectable. When false, the classic behaviour
    /// (base rate + uniform random jitter) is used.
    /// </summary>
    internal static bool RealisticSpeed = true;

    /// <summary>Debug escape hatch: set SF_NOTHEME to disable the theme engine.</summary>
    internal static readonly bool ThemingEnabled =
      System.Environment.GetEnvironmentVariable("SF_NOTHEME") == null;
  }
}
