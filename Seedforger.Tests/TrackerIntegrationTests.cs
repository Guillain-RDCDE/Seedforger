using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Seedforger;
using Seedforger.BitTorrent;
using Xunit;

namespace Seedforger.Tests {

  /// <summary>
  /// True end-to-end tests: a real in-process BitTorrent tracker (HttpListener,
  /// bencoded answers) and a real <see cref="SeedEngine"/> talking to it over the
  /// loopback socket — the full announce path on the wire, no mocking of the core.
  /// Verifies acceptance/swarm parsing, that reported bytes advance, the started/
  /// stopped lifecycle, and the leecher→seeder "completed" transition.
  /// </summary>
  public class TrackerIntegrationTests {
    static TrackerIntegrationTests() { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); }

    [Fact]
    public void Seeder_IsAcceptedAndReportsSwarm_AndSendsStartedThenStopped() {
      using var tracker = new MockTracker(complete: 7, incomplete: 4, interval: 5, minInterval: 3);
      var path = TorrentBuilder.Write(tracker.AnnounceUrl, null);
      try {
        AppOptions.RealisticSpeed = false; // deterministic byte accrual for the test
        AppOptions.SwarmAware = false;
        var client = TorrentClientFactory.GetClient("qBittorrent 5.2.3");
        var engine = new SeedEngine(new Torrent(path), client, new ProxyInfo(), uploadKBps: 50, downloadKBps: 0, finishedPercent: 100);

        engine.Start();
        Assert.True(WaitUntil(() => tracker.SawEvent("started"), 5000), "tracker never saw event=started");
        Assert.True(WaitUntil(() => engine.SeederCount == 7 && engine.LeecherCount == 4, 5000), "swarm counts not parsed");
        Assert.True(WaitUntil(() => engine.UploadedBytes > 0, 5000), "reported upload never advanced");

        engine.Stop();
        Assert.True(WaitUntil(() => tracker.SawEvent("stopped"), 5000), "tracker never saw event=stopped");
      }
      finally {
        AppOptions.RealisticSpeed = true; AppOptions.SwarmAware = true;
        File.Delete(path);
      }
    }

    [Fact]
    public void Leecher_ReachingZeroLeft_SendsExactlyOneCompleted() {
      using var tracker = new MockTracker(complete: 20, incomplete: 1, interval: 5, minInterval: 1);
      var path = TorrentBuilder.Write(tracker.AnnounceUrl, null);
      try {
        AppOptions.RealisticSpeed = false; // so the tiny 100-byte torrent completes on the first tick
        AppOptions.SwarmAware = false;
        var client = TorrentClientFactory.GetClient("qBittorrent 5.2.3");
        // downloadKBps=1 -> 1024 bytes/tick, far exceeding the 100-byte torrent: left hits 0 immediately.
        var engine = new SeedEngine(new Torrent(path), client, new ProxyInfo(), uploadKBps: 10, downloadKBps: 1, finishedPercent: 0);

        engine.Start();
        Assert.True(WaitUntil(() => tracker.EventCount("completed") == 1, 8000),
          $"expected exactly one completed, saw {tracker.EventCount("completed")}");

        // It must not keep firing "completed" on later announces.
        Thread.Sleep(1500);
        Assert.Equal(1, tracker.EventCount("completed"));
        engine.Stop();
      }
      finally {
        AppOptions.RealisticSpeed = true; AppOptions.SwarmAware = true;
        File.Delete(path);
      }
    }

    [Fact]
    public void MultiTracker_Torrent_AnnouncesToEveryTracker() {
      using var primary = new MockTracker(complete: 3, incomplete: 2, interval: 30, minInterval: 10);
      using var backup = new MockTracker(complete: 0, incomplete: 0, interval: 30, minInterval: 10);
      var path = TorrentBuilder.Write(primary.AnnounceUrl, new[] { new[] { backup.AnnounceUrl } });
      try {
        var client = TorrentClientFactory.GetClient("qBittorrent 5.2.3");
        var engine = new SeedEngine(new Torrent(path), client, new ProxyInfo(), 10, 0, 100);
        Assert.Equal(2, engine.TrackerCount);

        engine.Start();
        try {
          Assert.True(WaitUntil(() => primary.SawEvent("started"), 5000), "primary tracker not announced to");
          Assert.True(WaitUntil(() => backup.SawEvent("started"), 5000), "backup tracker not announced to");
          // Only the primary drives the reported swarm.
          Assert.True(WaitUntil(() => engine.SeederCount == 3, 5000), "primary swarm not adopted");
        }
        finally { engine.Stop(); }
      }
      finally { File.Delete(path); }
    }

    private static bool WaitUntil(Func<bool> cond, int timeoutMs) {
      var sw = System.Diagnostics.Stopwatch.StartNew();
      while (sw.ElapsedMilliseconds < timeoutMs) {
        if (cond()) return true;
        Thread.Sleep(50);
      }
      return cond();
    }

    /// <summary>A minimal but real BitTorrent HTTP tracker: answers every announce
    /// with a bencoded swarm and records the events it was sent.</summary>
    private sealed class MockTracker : IDisposable {
      private readonly HttpListener listener = new HttpListener();
      private readonly ConcurrentBag<string> events = new ConcurrentBag<string>();
      private readonly string body;
      private volatile bool running = true;

      public string AnnounceUrl { get; }

      public MockTracker(int complete, int incomplete, int interval, int minInterval) {
        var port = FreePort();
        AnnounceUrl = $"http://127.0.0.1:{port}/announce";
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        body = $"d8:completei{complete}e10:incompletei{incomplete}e8:intervali{interval}e12:min intervali{minInterval}ee";
        listener.Start();
        var t = new Thread(Loop) { IsBackground = true };
        t.Start();
      }

      private void Loop() {
        while (running) {
          HttpListenerContext ctx;
          try { ctx = listener.GetContext(); }
          catch { return; }
          try {
            var q = ctx.Request.Url?.Query ?? "";
            var m = System.Text.RegularExpressions.Regex.Match(q, "event=([a-z]+)");
            if (m.Success) events.Add(m.Groups[1].Value);
            var bytes = Encoding.ASCII.GetBytes(body);
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            ctx.Response.OutputStream.Close();
          }
          catch { /* ignore */ }
        }
      }

      public bool SawEvent(string ev) { foreach (var e in events) if (e == ev) return true; return false; }
      public int EventCount(string ev) { var n = 0; foreach (var e in events) if (e == ev) n++; return n; }

      public void Dispose() {
        running = false;
        try { listener.Stop(); } catch { }
        try { listener.Close(); } catch { }
      }

      private static int FreePort() {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var p = ((IPEndPoint) l.LocalEndpoint).Port;
        l.Stop();
        return p;
      }
    }
  }
}
