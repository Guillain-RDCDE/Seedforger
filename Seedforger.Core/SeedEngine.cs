using System;
using System.Threading;
using Seedforger.BitTorrent;

namespace Seedforger {

  /// <summary>
  /// A headless, WinForms-free seeding engine: announces to the tracker on the
  /// believable schedule and keeps the reported byte counts moving, reusing the
  /// exact same core as the GUI (Announce URL/parse + TrackerTransport). Runs on
  /// Windows, Linux and macOS — this is what the command line drives.
  /// </summary>
  internal sealed class SeedEngine {

    /// <summary>Optional log sink (raw tracker exchange + notes).</summary>
    public Action<string> Log;

    private readonly Torrent torrent;
    private readonly TorrentClient client;
    private readonly ProxyInfo proxy;
    private readonly int uploadKBps;
    private readonly int downloadKBps;
    private readonly int finishedPercent;

    private readonly string hashHex;
    private readonly string peerId;
    private readonly string key;
    private readonly string port;
    private readonly string numWant = "200";

    private long uploaded;
    private long downloaded;
    private long totalSize;
    private long left;
    private int seeders = -1;
    private int leechers = -1;
    private int interval = 1800;
    private volatile bool running;
    private Timer timer;
    private DateTime lastTick;

    public long UploadedBytes => uploaded;
    public long DownloadedBytes => downloaded;
    public int SeederCount => seeders;
    public int LeecherCount => leechers;
    public int IntervalSeconds => interval;
    public bool IsRunning => running;
    public double Ratio => downloaded > 0 ? (double) uploaded / downloaded : 0;
    public string TorrentName => torrent?.Name ?? "";

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

      // Mirror the GUI's finished→size mapping: 100% seeder reports left=0.
      var total = (long) torrent.totalLength;
      totalSize = this.finishedPercent == 0 ? total
                : this.finishedPercent == 100 ? 0
                : total * (100 - this.finishedPercent) / 100;
      left = totalSize;
    }

    public void Start() {
      if (running) return;
      running = true;
      SendAnnounce("&event=started");
      lastTick = DateTime.UtcNow;
      timer = new Timer(_ => Tick(), null, Math.Max(1, interval) * 1000, Timeout.Infinite);
    }

    public void Stop() {
      if (!running) return;
      running = false;
      try { timer?.Dispose(); } catch { }
      timer = null;
      SendAnnounce("&event=stopped");
    }

    private void Tick() {
      if (!running) return;
      try {
        var now = DateTime.UtcNow;
        var elapsed = (now - lastTick).TotalSeconds;
        lastTick = now;

        // Advance the reported counters at the configured rate.
        uploaded += (long) (uploadKBps * 1024L * elapsed);
        if (finishedPercent < 100) {
          var add = (long) (downloadKBps * 1024L * elapsed);
          downloaded += add;
          left = Math.Max(0, left - add);
        }

        SendAnnounce("");
      }
      catch (Exception ex) { Log?.Invoke("tick error: " + ex.Message); }
      finally {
        try { timer?.Change(Math.Max(1, interval) * 1000, Timeout.Infinite); } catch { }
      }
    }

    private void SendAnnounce(string ev) {
      var p = new Announce.Params {
        Tracker = torrent.Announce,
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
      if (r.Seeders >= 0) seeders = r.Seeders;
      if (r.Leechers >= 0) leechers = r.Leechers;
      if (r.Interval > 0) interval = r.Interval;
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
