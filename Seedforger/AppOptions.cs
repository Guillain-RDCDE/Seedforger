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

    /// <summary>Dark theme when true, light when false. Persisted in settings.json.</summary>
    internal static bool DarkMode = false;

    /// <summary>When true, a random modern client fingerprint is picked on each
    /// start, so the emulated client isn't the same every session.</summary>
    internal static bool RandomizeClientOnStart = false;

    /// <summary>When enabled, seeding only uploads during [Start, End) hours
    /// (outside the window the speed drops to 0, like the machine is idle).</summary>
    internal static bool ActiveHoursEnabled = false;
    internal static int ActiveHoursStart = 8;
    internal static int ActiveHoursEnd = 24;

    /// <summary>UI language. Persisted in settings.json.</summary>
    internal static Language Language = Language.English;

    /// <summary>Debug escape hatch: set SF_NOTHEME to disable the theme engine.</summary>
    internal static readonly bool ThemingEnabled =
      System.Environment.GetEnvironmentVariable("SF_NOTHEME") == null;
  }
}
