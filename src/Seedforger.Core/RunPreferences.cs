using Seedforger.Net;

namespace Seedforger {

  /// <summary>
  /// The "advanced" run values both GUIs edit in their Advanced dialog and
  /// remember between launches: proxy, fingerprint overrides, announce interval,
  /// stop rule, leecher download speed. One mapping to and from
  /// <see cref="Settings"/>, one application onto <see cref="SeedOptions"/>.
  /// </summary>
  internal sealed class RunPreferences {

    public ProxySettings Proxy { get; set; } = ProxySettings.None;
    public StopRule StopWhen { get; set; } = StopRule.Never;
    /// <summary>Base announce interval in seconds; 0 = the tracker's.</summary>
    public int AnnounceIntervalSeconds { get; set; }
    public string PeerIdOverride { get; set; } = "";
    public string KeyOverride { get; set; } = "";
    /// <summary>Announced port; 0 = random.</summary>
    public int Port { get; set; }
    /// <summary>numwant; 0 = the client's own default.</summary>
    public int NumWant { get; set; }
    /// <summary>Reported download speed when seeding as a leecher, kB/s.</summary>
    public int LeechDownloadKBps { get; set; } = 30;

    public static RunPreferences FromSettings(Settings s) => new RunPreferences {
      Proxy = ProxySettings.FromSettings(s),
      StopWhen = StopRule.Parse(s.StopWhen, s.StopAfter.ToString(System.Globalization.CultureInfo.InvariantCulture)),
      AnnounceIntervalSeconds = s.Interval,
      PeerIdOverride = s.CustomPeerID ?? "",
      KeyOverride = s.CustomKey ?? "",
      Port = s.CustomPort,
      NumWant = s.CustomPeers,
      LeechDownloadKBps = s.DownloadRate,
    };

    public void SaveTo(Settings s) {
      Proxy.SaveTo(s);
      s.StopWhen = StopWhen.Kind.ToString();
      s.StopAfter = StopWhen.Value;
      s.Interval = AnnounceIntervalSeconds;
      s.CustomPeerID = PeerIdOverride ?? "";
      s.CustomKey = KeyOverride ?? "";
      s.CustomPort = Port;
      s.CustomPeers = NumWant;
      s.DownloadRate = LeechDownloadKBps;
    }

    public void ApplyTo(SeedOptions o) {
      o.Proxy = Proxy;
      o.StopWhen = StopWhen;
      o.AnnounceIntervalSeconds = AnnounceIntervalSeconds;
      o.PeerIdOverride = string.IsNullOrWhiteSpace(PeerIdOverride) ? null : PeerIdOverride.Trim();
      o.KeyOverride = string.IsNullOrWhiteSpace(KeyOverride) ? null : KeyOverride.Trim();
      o.Port = Port;
      o.NumWant = NumWant;
    }
  }
}
