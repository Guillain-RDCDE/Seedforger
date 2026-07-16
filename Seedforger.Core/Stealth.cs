using System;
using System.Collections.Generic;

namespace Seedforger {

  /// <summary>
  /// Pure, testable helpers that make the faked activity look human: announce
  /// timing drift, a day/night speed rhythm, and believability checks.
  /// </summary>
  internal static class Stealth {

    /// <summary>
    /// Adds a small upward drift (0..12%) to the tracker's announce interval.
    /// Real clients rarely announce at the exact same second, and announcing
    /// EARLIER than the tracker's interval can get you flagged — so we only ever
    /// drift later, never sooner.
    /// </summary>
    internal static int JitterInterval(int baseInterval, Random rand) {
      if (baseInterval <= 0) return baseInterval;
      return baseInterval + (int) (rand.NextDouble() * baseInterval * 0.12);
    }

    /// <summary>
    /// A day/night multiplier for the target speed. Peaks in the evening
    /// (~20:00) and bottoms out in the morning (~08:00), so seeding "breathes"
    /// over the day instead of running dead-flat 24/7. Range ~[0.56, 1.0].
    /// </summary>
    internal static double DiurnalFactor(DateTime now) {
      var h = now.Hour + now.Minute / 60.0;
      var phase = (h - 20.0) / 24.0 * 2.0 * Math.PI; // peak at 20:00
      var c = Math.Cos(phase);                       // 1 at 20:00, -1 at 08:00
      return 0.78 + 0.22 * c;
    }

    /// <summary>
    /// True if <paramref name="now"/> falls inside the [startHour, endHour) active
    /// window. Handles windows that wrap past midnight (e.g. 22 → 6). When start
    /// equals end the window is treated as the whole day.
    /// </summary>
    internal static bool InActiveHours(DateTime now, int startHour, int endHour) {
      if (startHour == endHour) return true;
      var h = now.Hour;
      return startHour < endHour ? h >= startHour && h < endHour : h >= startHour || h < endHour;
    }

    /// <summary>
    /// Sanity-checks the chosen speeds and returns human-readable warnings when
    /// they look implausible for a home connection (empty list = looks fine).
    /// </summary>
    internal static List<string> BelievabilityWarnings(long upKBps, long downKBps) {
      var warnings = new List<string>();
      var upMbps = upKBps * 8.0 / 1000.0;

      if (upMbps > 500)
        warnings.Add($"Upload ~{upMbps:0} Mbps is higher than almost any home line - trackers may flag it.");
      else if (upMbps > 200)
        warnings.Add($"Upload ~{upMbps:0} Mbps is high for a home connection; make sure it fits your story.");

      if (downKBps > 0 && upKBps > downKBps * 3)
        warnings.Add("Upload far exceeds download - unusual for a residential connection.");

      return warnings;
    }
  }
}
