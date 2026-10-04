using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Seedforger.BitTorrent;
using Seedforger.Net;
using Seedforger.Wire;

namespace Seedforger {

  /// <summary>Where a run is in its life.</summary>
  internal enum SeedState { Idle, Running, Stopped }

  /// <summary>
  /// The seeding engine: announces to the tracker on a believable schedule and
  /// advances the reported byte counts with stealth shaping — a gentle ramp-up,
  /// swarm-aware scaling, a day/night rhythm, active-hours windows, a shared
  /// upstream budget and announce-interval jitter. Optionally answers peers on
  /// the announced port, serving genuine pieces of a real file.
  ///
  /// One engine is one run of one torrent: build it from <see cref="SeedOptions"/>,
  /// <see cref="Start"/> it, read the live properties, <see cref="Stop"/> it. It
  /// reads no settings and no globals, so the Windows GUI, the cross-platform
  /// GUI, the CLI, the daemon and campaigns all drive exactly the same code.
  /// <see cref="Start"/> and <see cref="Stop"/> block on the network; the Async
  /// variants run them on the thread pool. <see cref="SeedOptions.Log"/> is called
  /// from background threads.
  /// </summary>
  internal sealed class SeedEngine : IDisposable {

    private const int DefaultIntervalSeconds = 1800;
    private const int HistoryLength = 3600;      // one sample per second, one hour
    private const int MinPort = 1025, MaxPort = 65535;

    private readonly SeedOptions options;
    private readonly Torrent torrent;
    private readonly TorrentClient client;
    private readonly ProxySettings proxy;
    private readonly StealthOptions stealth;
    private readonly UpstreamBudget budget;
    private readonly Action<string> log;
    private readonly Random rand = new Random();
    private readonly SpeedShaper upShaper;
    private readonly SpeedShaper downShaper;
    private readonly List<string> trackers;
    private readonly string hashHex;
    private readonly string peerId;
    private readonly string key;
    private readonly string numWant;
    private readonly int port;
    private readonly byte[] wirePeerId;
    private readonly int finishedPercent;
    private readonly int downloadKBps;
    private readonly long totalSize;
    private readonly object announceGate = new object();
    private readonly List<long> history = new List<long>(HistoryLength);

    private int uploadKBps;
    private long uploaded;
    private long downloaded;
    private long left;
    private int seeders = -1;
    private int leechers = -1;
    private int interval;
    private int minInterval = -1;
    private volatile bool completedSent;
    private volatile bool running;
    private volatile string lastFailure;
    private int stopOnce;
    private Timer announceTimer;
    private Timer counterTimer;
    private DateTime startedUtc;
    private DateTime stoppedUtc;
    private DateTime nextAnnounceUtc;

    // Optional real peer-wire serving (opens the announced port).
    private IPieceSource pieceSource;
    private Governor governor;
    private PeerListener peerListener;

    /// <summary>Raised (on a background thread) when a stop rule ended the run,
    /// with the reason.</summary>
    public event Action<SeedEngine, string> AutoStopped;

    internal SeedEngine(SeedOptions options) {
      if (options == null) throw new ArgumentNullException(nameof(options));
      options.Validate();
      this.options = options;
      torrent = options.Torrent;
      client = options.Client;
      proxy = options.Proxy;
      stealth = options.Stealth;
      budget = options.Budget;
      log = options.Log;
      uploadKBps = Math.Max(0, options.UploadKBps);
      downloadKBps = Math.Max(0, options.DownloadKBps);
      finishedPercent = options.FinishedPercent;
      interval = options.AnnounceIntervalSeconds > 0 ? options.AnnounceIntervalSeconds : DefaultIntervalSeconds;

      hashHex = Hex.Lower(torrent.InfoHash);
      peerId = string.IsNullOrEmpty(options.PeerIdOverride) ? client.PeerID : options.PeerIdOverride;
      key = string.IsNullOrEmpty(options.KeyOverride) ? client.Key : options.KeyOverride;
      port = options.Port > 0 ? options.Port : rand.Next(MinPort, MaxPort);
      var want = options.NumWant > 0 ? options.NumWant : client.DefNumWant > 0 ? client.DefNumWant : 200;
      numWant = want.ToString();
      upShaper = new SpeedShaper(rand);
      downShaper = new SpeedShaper(rand);
      wirePeerId = MakeWirePeerId(peerId, rand);

      // A 100% seeder reports left=0; a partial leecher reports the remainder.
      var total = (long) torrent.totalLength;
      totalSize = finishedPercent == 0 ? total
                : finishedPercent == 100 ? 0
                : total * (100 - finishedPercent) / 100;
      left = totalSize;
      // A seeder never needs to announce "completed"; a leecher sends it once it
      // reaches left=0, exactly like a real client.
      completedSent = finishedPercent >= 100;

      // Every tracker this torrent lists (BEP-12): the primary drives the reported
      // swarm/interval, the rest are announced to best-effort.
      trackers = new List<string>();
      var list = torrent.AnnounceList;
      if (list != null) trackers.AddRange(list);
      if (trackers.Count == 0 && !string.IsNullOrEmpty(torrent.Announce)) trackers.Add(torrent.Announce);

      if (!string.IsNullOrEmpty(options.RealFile)) EnableRealSeed(options.RealFile);
    }

    // ---- live read-outs (safe to read from any thread) ----

    public long UploadedBytes => Interlocked.Read(ref uploaded);
    public long DownloadedBytes => Interlocked.Read(ref downloaded);
    public long LeftBytes => Interlocked.Read(ref left);
    public int SeederCount => seeders;
    public int LeecherCount => leechers;
    public int IntervalSeconds => interval;
    public bool IsRunning => running;
    public SeedState State => running ? SeedState.Running : stoppedUtc != default ? SeedState.Stopped : SeedState.Idle;
    public double Ratio { get { var d = DownloadedBytes; return d > 0 ? (double) UploadedBytes / d : 0; } }
    public string TorrentName => torrent?.Name ?? "";
    /// <summary>The impersonated client's display name (e.g. "qBittorrent 5.2.4").</summary>
    public string ClientName => client?.Name ?? "";
    public int Port => port;
    public int UploadKBps => uploadKBps;
    /// <summary>True once a real, hash-verified file is being served over the wire.</summary>
    public bool RealSeedEnabled => pieceSource != null;
    /// <summary>How many trackers this engine announces to (primary + announce-list).</summary>
    public int TrackerCount => trackers.Count;
    /// <summary>The tracker's last "failure reason", or null while it accepts us.</summary>
    public string LastFailure => lastFailure;
    public bool Rejected => lastFailure != null;
    public TimeSpan Elapsed => startedUtc == default ? TimeSpan.Zero : (running ? DateTime.UtcNow : stoppedUtc) - startedUtc;
    public TimeSpan UntilNextAnnounce => running && nextAnnounceUtc > DateTime.UtcNow ? nextAnnounceUtc - DateTime.UtcNow : TimeSpan.Zero;
    /// <summary>Reported upload, sampled once a second (newest last, at most an hour).</summary>
    public long[] UploadHistory { get { lock (history) return history.ToArray(); } }

    /// <summary>Adjust the reported upload rate at runtime (campaign allocation).</summary>
    public void SetUploadKBps(int kbps) => uploadKBps = Math.Max(0, kbps);

    // ---- pure rules, unit-tested without a tracker ----

    /// <summary>A leecher (finished &lt; 100%) that has just reached zero bytes
    /// left fires a single <c>event=completed</c>, once only.</summary>
    internal static bool ShouldAnnounceCompleted(int finishedPercent, long left, bool alreadySent)
      => finishedPercent < 100 && left <= 0 && !alreadySent;

    /// <summary>The re-announce base interval: never below the tracker's interval,
    /// and never below its <c>min interval</c> when it sent one (a floor of 1s).</summary>
    internal static int AnnounceBaseInterval(int interval, int minInterval) {
      var b = Math.Max(1, interval);
      if (minInterval > 0) b = Math.Max(b, minInterval);
      return b;
    }

    // ---- real seed ----

    /// <summary>Serve genuine, hash-verified pieces of a real downloaded file
    /// (defeats a tracker's monitoring peers). Must match this torrent and be set
    /// before <see cref="Start"/>. Returns false, after logging why, if it doesn't verify.</summary>
    public bool EnableRealSeed(string filePath) {
      try {
        if (torrent.PieceCount <= 0 || torrent.PieceHashesRaw == null) {
          log?.Invoke("Real seed needs a .torrent with piece hashes (magnets have none).");
          return false;
        }
        var hashes = FilePieceSource.SplitHashes(torrent.PieceHashesRaw);
        var src = new FilePieceSource(filePath, torrent.PieceLength, (long) torrent.totalLength, hashes);
        if (!src.HasPiece(0)) { src.Dispose(); log?.Invoke("That file doesn't match this torrent (piece 0 failed)."); return false; }
        (pieceSource as IDisposable)?.Dispose();
        pieceSource = src;
        governor = new Governor();
        log?.Invoke("REAL SEED enabled: serving genuine hash-valid pieces from " + filePath);
        return true;
      }
      catch (Exception ex) { log?.Invoke("Real seed error: " + ex.Message); return false; }
    }

    // ---- lifecycle ----

    public void Start() {
      if (running) return;
      running = true;
      Interlocked.Exchange(ref stopOnce, 0);
      startedUtc = DateTime.UtcNow;
      stoppedUtc = default;
      lastFailure = null;
      budget.Register();

      // Open the announced port so a real, reachable peer sits behind the announce
      // (real pieces if a file was verified, else a complete-but-choked seeder).
      if (options.OpenPeerPort && !proxy.Enabled) {
        try {
          peerListener = new PeerListener(torrent, wirePeerId, pieceSource, governor, () => uploadKBps * 1024, port, log);
          peerListener.Start();
        }
        catch (Exception ex) { log?.Invoke("Could not open the peer port " + port + ": " + ex.Message); peerListener = null; }
      }

      SendAnnounce("&event=started");
      if (!running) return; // stopped while the first announce was in flight

      // A 1-second counter tick advances the reported bytes with stealth shaping,
      // while the announce timer re-announces the accumulated totals on schedule.
      counterTimer = new Timer(_ => CounterTick(), null, 1000, 1000);
      ScheduleNextAnnounce();
    }

    public Task StartAsync() => Task.Run(Start);

    public void Stop() => Stop("stopped", notify: false);

    public Task StopAsync() => Task.Run(Stop);

    /// <summary>Re-announce right now (a manual update); the regular schedule restarts from now.</summary>
    public void AnnounceNow() {
      if (!running) return;
      ThreadPool.QueueUserWorkItem(_ => { SendAnnounce(""); ScheduleNextAnnounce(); });
    }

    public void Dispose() {
      Stop();
      (pieceSource as IDisposable)?.Dispose();
    }

    private void Stop(string reason, bool notify) {
      if (!running) return;
      if (Interlocked.Exchange(ref stopOnce, 1) == 1) return;
      running = false;
      stoppedUtc = DateTime.UtcNow;
      try { counterTimer?.Dispose(); } catch (ObjectDisposedException) { }
      try { announceTimer?.Dispose(); } catch (ObjectDisposedException) { }
      counterTimer = null;
      announceTimer = null;
      try { peerListener?.Stop(); } catch (Exception ex) { log?.Invoke("peer port close: " + ex.Message); }
      peerListener = null;
      budget.Unregister();
      SendAnnounce("&event=stopped");
      log?.Invoke("Run " + reason + ".");
      if (notify) {
        try { AutoStopped?.Invoke(this, reason); }
        catch (Exception ex) { log?.Invoke("stop handler error: " + ex.Message); }
      }
    }

    // ---- the per-second clock ----

    private void CounterTick() {
      if (!running) return;
      try {
        var now = DateTime.Now;

        // Upload target for this second, shaped like a real client.
        var upTarget = uploadKBps * 1024L;
        if (stealth.SwarmAware && leechers >= 0)
          upTarget = (long) (upTarget * SwarmModel.UploadFactor(leechers, Math.Max(0, seeders)));
        upTarget = (long) (upTarget * Stealth.DiurnalFactor(now));
        if (!stealth.IsActive(now)) upTarget = 0;
        upTarget = budget.CapUpload(upTarget);
        var add = stealth.RealisticSpeed ? upShaper.NextSecondBytes(upTarget) : upTarget;

        var newUp = Interlocked.Read(ref uploaded) + add;
        // Real seed: never claim more than a plausible multiple of what was really served.
        if (pieceSource != null && governor != null)
          newUp = Math.Max(Interlocked.Read(ref uploaded), governor.CapAnnounced(newUp, Math.Max(1, leechers)));
        Interlocked.Exchange(ref uploaded, newUp);

        // Keep leeching only while there is something left to fetch; once left hits
        // zero a real client stops downloading and fires a single "completed".
        if (finishedPercent < 100 && Interlocked.Read(ref left) > 0) {
          var downTarget = downloadKBps * 1024L;
          if (stealth.SwarmAware && seeders >= 0)
            downTarget = (long) (downTarget * SwarmModel.DownloadFactor(seeders));
          var got = stealth.RealisticSpeed ? downShaper.NextSecondBytes(downTarget) : downTarget;
          Interlocked.Add(ref downloaded, got);
          var newLeft = Math.Max(0, Interlocked.Read(ref left) - got);
          Interlocked.Exchange(ref left, newLeft);
          if (ShouldAnnounceCompleted(finishedPercent, newLeft, completedSent)) {
            completedSent = true;
            log?.Invoke("Download complete — announcing event=completed and switching to seeder.");
            ThreadPool.QueueUserWorkItem(_ => SendAnnounce("&event=completed"));
          }
        }

        lock (history) {
          history.Add(newUp);
          if (history.Count > HistoryLength) history.RemoveAt(0);
        }

        var reason = options.StopWhen.Evaluate(Elapsed, newUp, Interlocked.Read(ref downloaded), seeders, leechers);
        if (reason != null)
          ThreadPool.QueueUserWorkItem(_ => Stop("ended by its stop rule: " + reason, notify: true));
      }
      catch (Exception ex) { log?.Invoke("counter error: " + ex.Message); }
    }

    // ---- announcing ----

    private void ScheduleNextAnnounce() {
      if (!running) return;
      // Never re-announce sooner than the tracker's own interval — and honour a
      // "min interval" floor when it sends one — then only ever drift *later*.
      var next = Stealth.JitterInterval(AnnounceBaseInterval(interval, minInterval), rand);
      nextAnnounceUtc = DateTime.UtcNow.AddSeconds(next);
      try { announceTimer?.Dispose(); } catch (ObjectDisposedException) { }
      announceTimer = new Timer(_ => {
        if (!running) return;
        SendAnnounce("");
        ScheduleNextAnnounce();
      }, null, next * 1000L, Timeout.Infinite);
    }

    /// <summary>Announce to every tracker the torrent lists (BEP-12). Serialised:
    /// a scheduled announce never overlaps a manual one or the final "stopped".</summary>
    private void SendAnnounce(string ev) {
      lock (announceGate) {
        for (var i = 0; i < trackers.Count; i++) {
          try { AnnounceOne(trackers[i], ev, isPrimary: i == 0); }
          catch (Exception ex) { log?.Invoke("announce error (" + trackers[i] + "): " + ex.Message); }
        }
      }
    }

    private void AnnounceOne(string tracker, string ev, bool isPrimary) {
      var p = Announce.ParamsFor(tracker, client, hashHex, peerId, key, port.ToString(), numWant,
        Interlocked.Read(ref uploaded), Interlocked.Read(ref downloaded), Interlocked.Read(ref left), totalSize, ev);
      var resp = TrackerTransport.Fetch(new Uri(Announce.BuildUrl(p)), client, proxy, log);
      if (resp?.Dict == null) return;

      var r = Announce.FromDict(resp.Dict);
      if (!r.Accepted) {
        if (isPrimary) lastFailure = r.Failure;
        log?.Invoke("Tracker rejected the announce: " + r.Failure);
        return;
      }
      // Only the primary tracker drives our reported state, so multiple trackers
      // don't fight over the swarm figures we display and pace against.
      if (!isPrimary) return;
      lastFailure = null;
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
  }
}
