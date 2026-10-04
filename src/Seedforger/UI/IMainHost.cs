using Seedforger.BitTorrent;

namespace Seedforger.UI {

  /// <summary>What the guided setup needs from the main window: the current
  /// torrent, a way to load one, a dry-run probe, and the start button.</summary>
  internal interface IMainHost {
    Torrent CurrentTorrent { get; }
    string TorrentDisplayName { get; }
    bool LoadTorrent(string path);
    /// <summary>One dry-run seeder announce with the current client and proxy (blocking).</summary>
    AnnounceProbe Probe();
    void ApplyConnectionProfile(string name);
    /// <summary>Starts seeding as a complete seeder with the believable defaults on.</summary>
    void StartSeedingSafely();
  }
}
