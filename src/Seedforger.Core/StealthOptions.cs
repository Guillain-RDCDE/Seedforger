using System;

namespace Seedforger {

  /// <summary>
  /// The believability knobs an engine runs with: realistic ramp-up, swarm-aware
  /// scaling and an active-hours window. An instance, not global state, so a
  /// campaign can run with its own profile without touching what the user set in
  /// the window. <see cref="Shared"/> is the one the GUIs edit and persist.
  /// </summary>
  internal sealed class StealthOptions {

    /// <summary>The process-wide defaults the interface edits and persists.</summary>
    public static StealthOptions Shared { get; } = new StealthOptions();

    /// <summary>Shape upload/download with a ramp-up and smooth mean-reverting
    /// variation (see <see cref="SpeedShaper"/>) instead of a flat rate.</summary>
    public bool RealisticSpeed { get; set; } = true;

    /// <summary>Scale the reported speeds by the real swarm demand the tracker
    /// reports (leechers/seeders), so the numbers stay physically plausible.</summary>
    public bool SwarmAware { get; set; } = true;

    /// <summary>Only report upload inside [<see cref="ActiveHoursStart"/>,
    /// <see cref="ActiveHoursEnd"/>) — outside, the machine looks idle.</summary>
    public bool ActiveHoursEnabled { get; set; }
    public int ActiveHoursStart { get; set; } = 8;
    public int ActiveHoursEnd { get; set; } = 24;

    public StealthOptions Clone() => (StealthOptions) MemberwiseClone();

    /// <summary>True when upload may be reported right now under this profile.</summary>
    internal bool IsActive(DateTime now) =>
      !ActiveHoursEnabled || Stealth.InActiveHours(now, ActiveHoursStart, ActiveHoursEnd);

    /// <summary>Copies the persisted values into this instance.</summary>
    internal StealthOptions LoadFrom(Settings s) {
      if (s == null) return this;
      RealisticSpeed = s.RealisticSpeed;
      SwarmAware = s.SwarmAware;
      ActiveHoursEnabled = s.ActiveHoursEnabled;
      ActiveHoursStart = s.ActiveHoursStart;
      ActiveHoursEnd = s.ActiveHoursEnd;
      return this;
    }

    internal void SaveTo(Settings s) {
      if (s == null) return;
      s.RealisticSpeed = RealisticSpeed;
      s.SwarmAware = SwarmAware;
      s.ActiveHoursEnabled = ActiveHoursEnabled;
      s.ActiveHoursStart = ActiveHoursStart;
      s.ActiveHoursEnd = ActiveHoursEnd;
    }
  }
}
