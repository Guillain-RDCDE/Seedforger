using System;
using System.Threading;
using Seedforger.BitTorrent;
using Seedforger.BytesRoads;
using Seedforger.Wire;

namespace Seedforger {

  /// <summary>
  /// A headless, WinForms-free seeding engine: announces to the tracker on the
  /// believable schedule and advances the reported byte counts with the same
  /// stealth shaping as the GUI — a gentle ramp-up, swarm-aware scaling, a
  /// day/night rhythm, active-hours windows, a shared upstream budget and
  /// announce-interval jitter. Reuses the exact same core as the CLI and the GUI
  /// (Announce URL/parse + TrackerTransport). Runs on Windows, Linux and macOS.
  /// </summary>
  internal sealed class SeedEngine {

    /// <summary>Optional log sink (raw tracker exchange + notes).</summary>
    public Action<string> Log;

    private readonly Torrent torrent;
    private readonly TorrentClient client;
    private readonly ProxyInfo proxy;
    private int uploadKBps;
    private readonly int downloadKBps;
    private readonly int finishedPercent;

    private readonly string hashHex;
    private readonly string peerId;
    private readonly string key;
    private readonly string port;
    private readonly string numWant = "200";

    private readonly Random rand = new Random();
    private readonly SpeedShaper upShaper;
    private readonly SpeedShaper downShaper;
    private readonly byte[] wirePeerId;

    // Optional real peer-wire serving (opens the announced port).
    private IPieceSource pieceSource;
    private Governor governor;
    private PeerListener peerListener;

    private readonly System.Collections.Generic.List<string> trackers;

    private long uploaded;
    private long downloaded;
    private long totalSize;
    private long left;
    private int seeders = -1;
    private int leechers = -1;
    private int interval = 1800;
    private int minInterval = -1;
    private readonly bool startedAsLeecher;
    private volatile bool completedSent;
    private volatile bool running;
    private Timer announceTimer;
    private Timer counterTimer;

    public long UploadedBytes => uploaded;
    public long DownloadedBytes => downloaded;
    public int SeederCount => seeders;
    public int LeecherCount => leechers;
    public int IntervalSeconds => interval;
    public bool IsRunning => running;
    public double Ratio => downloaded > 0 ? (double) uploaded / downloaded : 0;
    public string TorrentName => torrent?.Name ?? "";
    /// <summary>The impersonated client's display name (e.g. "qBittorrent 5.2.3").</summary>
    public string ClientName => client?.Name ?? "";
    /// <summary>True once a real, hash-verified file is being served over the wire.</summary>
    public bool RealSeedEnabled => pieceSource != null;
    /// <summary>Adjust the reported upload rate at runtime (campaign allocation).</summary>
    public void SetUploadKBps(int kbps) => uploadKBps = Math.Max(0, kbps);

    internal SeedEngine(Torrent torrent, TorrentClient client, ProxyInfo proxy,
                        int uploadKBps, int downloadKBps, int finishedPercent) {
      this.torrent = torrent;
      this.client = client;
      this.proxy = proxy;
      this.uploadKBps = Math.Max(0, uploadKBps);
      this.downloadKBps = Math.Max(0, downloadKBps);
      this.finishedPercent = Math.Min(100, Math.Max(0, finishedPercent));

      hashHex = ToHex(torrent.InfoHash);
      peerId = client.PeerID;
      key = client.Key;
      port = new Random().Next(1025, 65535).ToString();
      upShaper = new SpeedShaper(rand);
      downShaper = new SpeedShaper(rand);
      wirePeerId = MakeWirePeerId(client.PeerID, rand);

      // Mirror the GUI's finished→size mapping: 100% seeder reports left=0.
      var total = (long) torrent.totalLength;
      totalSize = this.finishedPercent == 0 ? total
                : this.finishedPercent == 100 ? 0
                : total * (100 - this.finishedPercent) / 100;
      left = totalSize;
      startedAsLeecher = this.finishedPercent < 100;
      // A seeder never needs to announce "completed"; a leecher sends it once it
      // reaches left=0, exactly like a real client.
      completedSent = !startedAsLeecher;

      // Every tracker this torrent lists (BEP-12): the primary drives the reported
      // swarm/interval, the rest are announced to best-effort — real multi-tracker
      // behaviour. Falls back to the single Announce URL when there's no list.
      trackers = new System.Collections.Generic.List<string>(torrent.AnnounceList);
      if (trackers.Count == 0 && !string.IsNullOrEmpty(torrent.Announce)) trackers.Add(torrent.Announce);
    }

    /// <summary>How many trackers this engine announces to (primary + announce-list).</summary>
    public int TrackerCount => trackers.Count;

    /// <summary>Serve genuine, hash-verified pieces of a real downloaded file
    /// (defeats a tracker's monitoring peers). Must be a file that matches this
    /// torrent. Returns false if it doesn't verify.</summary>
    public bool EnableRealSeed(string filePath) {
      try {
        if (torrent.PieceCount <= 0 || torrent.PieceHashesRaw == null) {
          Log?.Invoke("Real seed needs a .torrent with piece hashes (magnets have none).");
          return false;
        }
        var hashes = FilePieceSource.SplitHashes(torrent.PieceHashesRaw);
        var src = new FilePieceSource(filePath, torrent.PieceLength, (long) torrent.totalLength, hashes);
        if (!src.HasPiece(0)) { src.Dispose(); Log?.Invoke("That file doesn't match this torrent (piece 0 failed)."); return false; }
        (pieceSource as IDisposable)?.Dispose();
        pieceSource = src;
        governor = new Governor();
        Log?.Invoke("REAL SEED enabled: serving genuine hash-valid pieces from " + filePath);
        return true;
      }
      catch (Exception ex) { Log?.Invoke("Real seed error: " + ex.Message); return false; }
    }

    public void Start() {
      if (running) return;
      running = true;
      Bandwidth.RegisterActive();
      // Open the announced port so a real, reachable peer sits behind the announce
      // (real pieces if a file was verified, else a complete-but-choked seeder).
      if (proxy.ProxyType == ProxyType.None && int.TryParse(port, out var p)) {
        peerListener = new PeerListener(torrent, wirePeerId, pieceSource, governor,
          () => uploadKBps * 1024, p, Log);
        peerListener.Start();
      }
      SendAnnounce("&event=started");
      // A 1-second counter tick advances the reported bytes with stealth shaping,
      // while the announce timer re-announces the accumulated totals on schedule.
      counterTimer = new Timer(_ => CounterTick(), null, 1000, 1000);
      ScheduleNextAnnounce();
    }

    public void Stop() {
      if (!running) return;
      running = false;
      try { peerListener?.Stop(); } catch { }
      try { counterTimer?.Dispose(); } catch { }
      try { announceTimer?.Dispose(); } catch { }
      counterTimer = null; announceTimer = null;
      Bandwidth.UnregisterActive();
      SendAnnounce("&event=stopped");
    }

    private void CounterTick() {
      if (!running) return;
      try {
        // Upload target for this second, shaped like a real client.
        long upTarget = uploadKBps * 1024L;
        if (AppOptions.SwarmAware && leechers >= 0)
          upTarget = (long) (upTarget * SwarmModel.UploadFactor(leechers, seeders < 0 ? 0 : seeders));
        upTarget = (long) (upTarget * Stealth.DiurnalFactor(DateTime.Now));
        if (AppOptions.ActiveHoursEnabled && !Stealth.InActiveHours(DateTime.Now, AppOptions.ActiveHoursStart, AppOptions.ActiveHoursEnd))
          upTarget = 0;
        upTarget = Bandwidth.CapUpload(upTarget);
        uploaded += AppOptions.RealisticSpeed ? upShaper.NextSecondBytes(upTarget) : upTarget;

        // Keep leeching only while there is something left to fetch; once left hits
        // zero a real client stops downloading and fires a single "completed".
        if (finishedPercent < 100 && left > 0) {
          long downTarget = downloadKBps * 1024L;
          if (AppOptions.SwarmAware && seeders >= 0)
            downTarget = (long) (downTarget * SwarmModel.DownloadFactor(seeders));
          var add = AppOptions.RealisticSpeed ? downShaper.NextSecondBytes(downTarget) : downTarget;
          downloaded += add;
          left = Math.Max(0, left - add);
          if (left == 0 && !completedSent) {
            completedSent = true;
            Log?.Invoke("download complete — announcing event=completed and switching to seeder.");
            ThreadPool.QueueUserWorkItem(_ => { try { SendAnnounce("&event=completed"); } catch { } });
          }
        }
      }
      catch (Exception ex) { Log?.Invoke("counter error: " + ex.Message); }
    }

    private void ScheduleNextAnnounce() {
      if (!running) return;
      // Never re-announce sooner than the tracker's own interval — and honour a
      // "min interval" floor when it sends one — then only ever drift *later*.
      var baseInterval = Math.Max(1, interval);
      if (minInterval > 0) baseInterval = Math.Max(baseInterval, minInterval);
      var next = Stealth.JitterInterval(baseInterval, rand);
      try { announceTimer?.Dispose(); } catch { }
      announceTimer = new Timer(_ => { SendAnnounce(""); ScheduleNextAnnounce(); }, null, next * 1000, Timeout.Infinite);
    }

    private void SendAnnounce(string ev) {
      // Announce to every tracker the torrent lists (BEP-12). The first (primary)
      // response drives the reported swarm counts and the announce interval; the
      // rest are best-effort, so one dead tracker can't stall the others.
      for (var i = 0; i < trackers.Count; i++)
        AnnounceOne(trackers[i], ev, isPrimary: i == 0);
    }

    private void AnnounceOne(string tracker, string ev, bool isPrimary) {
      var p = new Announce.Params {
        Tracker = tracker,
        QueryTemplate = client.Query,
        InfoHashHex = hashHex,
        PeerId = peerId,
        Port = port,
        Uploaded = uploaded,
        Downloaded = downloaded,
        Left = left,
        TotalSize = totalSize,
        Key = key,
        NumWant = numWant,
        Event = ev,
        LocalIp = Functions.GetIp(),
        HashUpperCase = client.HashUpperCase,
      };

      var url = Announce.BuildUrl(p);
      TrackerResponse resp;
      try { resp = TrackerTransport.Fetch(new Uri(url), client, proxy, Log); }
      catch (Exception ex) { Log?.Invoke("announce error: " + ex.Message); return; }

      if (resp?.Dict == null) return;
      var r = Announce.FromDict(resp.Dict);
      if (!string.IsNullOrEmpty(r.Failure)) { Log?.Invoke("Tracker rejected the announce: " + r.Failure); return; }
      // Only the primary tracker drives our reported state, so multiple trackers
      // don't fight over the swarm figures we display and pace against.
      if (!isPrimary) return;
      if (r.Seeders >= 0) seeders = r.Seeders;
      if (r.Leechers >= 0) leechers = r.Leechers;
      if (r.Interval > 0) interval = r.Interval;
      if (r.MinInterval > 0) minInterval = r.MinInterval;
    }

    // A 20-byte wire peer_id: keep the client-identifying ASCII prefix (what a
    // whitelist checks) then random bytes.
    private static byte[] MakeWirePeerId(string announced, Random rand) {
      var id = new byte[20];
      announced ??= "-SF0001-";
      var p = 0;
      for (; p < announced.Length && p < 20 && announced[p] != '%'; p++) id[p] = (byte) announced[p];
      for (; p < 20; p++) id[p] = (byte) rand.Next(48, 122);
      return id;
    }

    private static string ToHex(byte[] bytes) {
      if (bytes == null) return "";
      var c = new char[bytes.Length * 2];
      const string hex = "0123456789abcdef";
      for (var i = 0; i < bytes.Length; i++) {
        c[i * 2] = hex[bytes[i] >> 4];
        c[i * 2 + 1] = hex[bytes[i] & 0xF];
      }
      return new string(c);
    }
  }
}
