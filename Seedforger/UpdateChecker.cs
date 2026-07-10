using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Seedforger {

  /// <summary>
  /// Silent, best-effort "is there a newer release?" check against the GitHub
  /// releases API. Runs in the background at launch and only surfaces anything if
  /// a strictly newer version exists; offline / rate-limited / parse errors are
  /// swallowed so it never bothers the user.
  /// </summary>
  internal static class UpdateChecker {

    private const string LatestApi =
      "https://api.github.com/repos/Guillain-RDCDE/Seedforger/releases/latest";

    /// <summary>
    /// Fire-and-forget. If a newer release is found, <paramref name="onUpdate"/> is
    /// invoked with (tag, downloadPageUrl) — on a thread-pool thread, so callers
    /// must marshal back to the UI thread themselves.
    /// </summary>
    internal static void CheckInBackground(Action<string, string> onUpdate) {
      Task.Run(async () => {
        try {
          using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
          http.DefaultRequestHeaders.UserAgent.ParseAdd("Seedforger/" + AppInfo.Version);
          http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

          var json = await http.GetStringAsync(LatestApi).ConfigureAwait(false);
          using var doc = JsonDocument.Parse(json);
          var root = doc.RootElement;

          var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
          var url = root.TryGetProperty("html_url", out var u) ? u.GetString() : null;
          if (string.IsNullOrEmpty(url)) url = AppInfo.SiteUrl + "/releases/latest";

          if (!string.IsNullOrEmpty(tag) && IsNewer(tag, AppInfo.Version))
            onUpdate(tag, url);
        }
        catch { /* offline, rate-limited, or unparseable — stay silent */ }
      });
    }

    private static bool IsNewer(string tag, string current) {
      var latest = ParseVersion(tag);
      var have = ParseVersion(current);
      return latest != null && have != null && latest > have;
    }

    private static Version ParseVersion(string s) {
      if (string.IsNullOrWhiteSpace(s)) return null;
      s = s.Trim().TrimStart('v', 'V');
      var end = 0;
      while (end < s.Length && (char.IsDigit(s[end]) || s[end] == '.')) end++;
      s = s.Substring(0, end);
      return Version.TryParse(s, out var v) ? v : null;
    }
  }
}
