// The dashboard has no async test surface of its own, so we drive HttpClient
// synchronously here; the blocking-call analyzer warning is expected.
#pragma warning disable xUnit1031
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using Seedforger.Web;
using Xunit;

namespace Seedforger.Tests {

  /// <summary>The daemon's web layer: the pure status→JSON serialisation and the
  /// live HttpListener dashboard, exercised end-to-end over loopback.</summary>
  public class DaemonWebTests {

    [Fact]
    public void StatusJson_ComputesTotalsAndSerialisesRows() {
      var report = new StatusReport {
        Version = "9.9.9",
        UptimeSeconds = 3661,
        Torrents = new List<StatusSnapshot> {
          new StatusSnapshot { Name = "Alpha", Client = "qBittorrent 5.2.3", Uploaded = 3000, Downloaded = 1000, Ratio = 3.0, Seeders = 10, Leechers = 5, Interval = 1800, Running = true, Trackers = 2, RealSeed = true },
          new StatusSnapshot { Name = "Beta",  Client = "Transmission 4.0", Uploaded = 1000, Downloaded = 1000, Ratio = 1.0, Seeders = 1,  Leechers = 0, Interval = 900,  Running = false, Trackers = 1, RealSeed = false },
        },
      };

      var json = StatusJson.Serialize(report);
      using var doc = JsonDocument.Parse(json);
      var root = doc.RootElement;

      Assert.Equal("Seedforger", root.GetProperty("app").GetString());
      Assert.Equal("9.9.9", root.GetProperty("version").GetString());
      Assert.Equal(3661, root.GetProperty("uptimeSeconds").GetInt64());

      var totals = root.GetProperty("totals");
      Assert.Equal(4000, totals.GetProperty("uploaded").GetInt64());
      Assert.Equal(2000, totals.GetProperty("downloaded").GetInt64());
      Assert.Equal(2.0, totals.GetProperty("ratio").GetDouble(), 3);
      Assert.Equal(1, totals.GetProperty("running").GetInt32());
      Assert.Equal(2, totals.GetProperty("count").GetInt32());

      var rows = root.GetProperty("torrents");
      Assert.Equal(2, rows.GetArrayLength());
      Assert.Equal("Alpha", rows[0].GetProperty("name").GetString());
      Assert.True(rows[0].GetProperty("realSeed").GetBoolean());
      Assert.Equal(2, rows[0].GetProperty("trackers").GetInt32());
    }

    [Fact]
    public void StatusReport_ZeroDownload_RatioIsZeroNotInfinity() {
      var report = new StatusReport {
        Torrents = new List<StatusSnapshot> { new StatusSnapshot { Uploaded = 5000, Downloaded = 0 } },
      };
      Assert.Equal(0, report.TotalRatio);
      var json = StatusJson.Serialize(report);
      Assert.Contains("\"ratio\":0", json);
    }

    [Fact]
    public void WebDashboard_ServesPageStatusAndStop() {
      var port = FreePort();
      var stopped = false;
      var status = "{\"ok\":true,\"app\":\"Seedforger\"}";
      using var web = new WebDashboard("127.0.0.1", port, () => status, () => stopped = true, null);
      web.Start();

      using var http = new HttpClient { Timeout = System.TimeSpan.FromSeconds(5) };
      var baseUrl = $"http://127.0.0.1:{port}";

      // The dashboard page.
      var page = http.GetStringAsync(baseUrl + "/").GetAwaiter().GetResult();
      Assert.Contains("Seedforger", page);
      Assert.Contains("/api/status", page); // the page polls this endpoint

      // The JSON endpoint returns exactly what the provider hands back.
      var json = http.GetStringAsync(baseUrl + "/api/status").GetAwaiter().GetResult();
      Assert.Equal(status, json);

      // The stop endpoint invokes the callback.
      var stopResp = http.PostAsync(baseUrl + "/api/stop", null).GetAwaiter().GetResult();
      Assert.True(stopResp.IsSuccessStatusCode);
      Assert.True(stopped);

      // Unknown paths 404.
      var missing = http.GetAsync(baseUrl + "/nope").GetAwaiter().GetResult();
      Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
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
