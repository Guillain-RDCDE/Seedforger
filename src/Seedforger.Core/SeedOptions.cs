using System;
using Seedforger.BitTorrent;
using Seedforger.Net;

namespace Seedforger {

  /// <summary>
  /// Everything a <see cref="SeedEngine"/> needs, in one place. The front-ends
  /// fill this from their controls, the CLI from its flags, a campaign from its
  /// plan — the engine itself never reads settings or globals.
  /// </summary>
  internal sealed class SeedOptions {

    /// <summary>The torrent to announce (a real .torrent, or a virtual one from a magnet).</summary>
    public Torrent Torrent { get; set; }

    /// <summary>The client to impersonate (see <see cref="TorrentClientFactory"/>).</summary>
    public TorrentClient Client { get; set; }

    public ProxySettings Proxy { get; set; } = ProxySettings.None;

    /// <summary>Reported upload speed, kB/s. Adjustable at runtime through the engine.</summary>
    public int UploadKBps { get; set; }

    /// <summary>Reported download speed, kB/s (leecher mode only).</summary>
    public int DownloadKBps { get; set; }

    /// <summary>100 = complete seeder (left=0, no download); 0 = fresh leecher.</summary>
    public int FinishedPercent { get; set; } = 100;

    /// <summary>Base announce interval in seconds; 0 lets the tracker decide
    /// (1800 s until it answers). The tracker's interval always overrides it.</summary>
    public int AnnounceIntervalSeconds { get; set; }

    public StopRule StopWhen { get; set; } = StopRule.Never;

    /// <summary>Optional downloaded file matching the torrent: genuine hash-valid
    /// pieces are then served to peers that ask (defeats monitoring peers).</summary>
    public string RealFile { get; set; }

    /// <summary>Answer inbound peer connections on the announced port (a reachable,
    /// complete-but-choked seeder). Skipped automatically behind a proxy.</summary>
    public bool OpenPeerPort { get; set; } = true;

    /// <summary>Announced listening port; 0 picks a random one like a real client.</summary>
    public int Port { get; set; }

    /// <summary>Fingerprint overrides for the Advanced dialog; null/0 = the client's own.</summary>
    public string PeerIdOverride { get; set; }
    public string KeyOverride { get; set; }
    public int NumWant { get; set; }

    public StealthOptions Stealth { get; set; } = StealthOptions.Shared;
    public UpstreamBudget Budget { get; set; } = UpstreamBudget.Shared;

    /// <summary>Log sink. Called from background threads.</summary>
    public Action<string> Log { get; set; }

    internal void Validate() {
      if (Torrent == null) throw new ArgumentException("A torrent is required.");
      if (Client == null) throw new ArgumentException("A client to impersonate is required.");
      if (FinishedPercent < 0 || FinishedPercent > 100) throw new ArgumentOutOfRangeException(nameof(FinishedPercent));
      if (Port < 0 || Port > 65535) throw new ArgumentOutOfRangeException(nameof(Port));
      Proxy ??= ProxySettings.None;
      Stealth ??= StealthOptions.Shared;
      Budget ??= UpstreamBudget.Shared;
      StopWhen ??= StopRule.Never;
    }
  }
}
