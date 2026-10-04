using System;
using System.Globalization;

namespace Seedforger {

  /// <summary>Human-readable formatting shared by every front-end and the CLI.</summary>
  internal static class Format {

    private static readonly string[] Units = { "bytes", "KB", "MB", "GB", "TB", "PB" };

    /// <summary>"0 bytes", "512 bytes", "1.50 MB" — binary units, two decimals.</summary>
    public static string Bytes(long bytes) {
      if (bytes < 0) bytes = 0;
      double v = bytes;
      var i = 0;
      while (v >= 1024 && i < Units.Length - 1) { v /= 1024; i++; }
      return (i == 0 ? bytes.ToString(CultureInfo.InvariantCulture) : v.ToString("0.00", CultureInfo.InvariantCulture)) + " " + Units[i];
    }

    /// <summary>"1.50 MB/s".</summary>
    public static string Rate(long bytesPerSecond) => Bytes(bytesPerSecond) + "/s";

    /// <summary>uploaded ÷ downloaded with two decimals, or "—" when nothing was
    /// downloaded (a pure seeder's ratio is infinite, not zero).</summary>
    public static string Ratio(long uploaded, long downloaded) =>
      downloaded > 0 ? ((double) uploaded / downloaded).ToString("0.00", CultureInfo.InvariantCulture) : "—";

    /// <summary>"hh:mm:ss", with a day count in front past 24 hours.</summary>
    public static string Duration(TimeSpan t) {
      if (t < TimeSpan.Zero) t = TimeSpan.Zero;
      return t.TotalDays >= 1
        ? (int) t.TotalDays + "d " + t.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)
        : t.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
    }
  }
}
