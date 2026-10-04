using System;
using System.IO;
using System.Text;
using Seedforger;
using Seedforger.BitTorrent;
using Seedforger.Cli;
using Seedforger.Net;
using Xunit;

namespace Seedforger.Tests {

  /// <summary>The pure pieces the refactored engine is built from: stop rules,
  /// formatting, the command-line reader, and the dry-run probe against a fake tracker.</summary>
  public class StopRuleTests {

    [Fact]
    public void Never_NeverStops() {
      Assert.Null(StopRule.Never.Evaluate(TimeSpan.FromDays(10), long.MaxValue, 1, 0, 0));
      Assert.False(StopRule.Never.IsSet);
    }

    [Theory]
    [InlineData(StopKind.AfterMinutes, 30, 29, false)]
    [InlineData(StopKind.AfterMinutes, 30, 30, true)]
    public void AfterMinutes_UsesElapsedTime(StopKind kind, double value, int elapsedMinutes, bool stops) {
      var r = StopRule.After(kind, value).Evaluate(TimeSpan.FromMinutes(elapsedMinutes), 0, 0, -1, -1);
      Assert.Equal(stops, r != null);
    }

    [Fact]
    public void UploadedMB_ComparesInBinaryMegabytes() {
      var r = StopRule.After(StopKind.UploadedMB, 100);
      Assert.Null(r.Evaluate(TimeSpan.Zero, 100L * 1024 * 1024 - 1, 0, -1, -1));
      Assert.NotNull(r.Evaluate(TimeSpan.Zero, 100L * 1024 * 1024, 0, -1, -1));
    }

    [Fact]
    public void RatioAbove_NeedsSomethingDownloaded() {
      var r = StopRule.After(StopKind.RatioAbove, 2.0);
      Assert.Null(r.Evaluate(TimeSpan.Zero, 1_000_000, 0, -1, -1));         // infinite ratio is not "reached"
      Assert.Null(r.Evaluate(TimeSpan.Zero, 1_999, 1_000, -1, -1));
      Assert.NotNull(r.Evaluate(TimeSpan.Zero, 2_000, 1_000, -1, -1));
    }

    [Fact]
    public void SwarmRules_IgnoreUnknownCounts() {
      Assert.Null(StopRule.After(StopKind.SeedersBelow, 5).Evaluate(TimeSpan.Zero, 0, 0, -1, -1));
      Assert.NotNull(StopRule.After(StopKind.SeedersBelow, 5).Evaluate(TimeSpan.Zero, 0, 0, 4, 10));
      Assert.Null(StopRule.After(StopKind.LeechersBelow, 1).Evaluate(TimeSpan.Zero, 0, 0, 4, 1));
      Assert.NotNull(StopRule.After(StopKind.LeechersBelow, 1).Evaluate(TimeSpan.Zero, 0, 0, 4, 0));
    }

    [Theory]
    [InlineData("Never", "0", StopKind.Never)]
    [InlineData("time", "30", StopKind.AfterMinutes)]
    [InlineData("AfterMinutes", "30", StopKind.AfterMinutes)]
    [InlineData("uploaded", "500", StopKind.UploadedMB)]
    [InlineData("RatioAbove", "1,5", StopKind.RatioAbove)]
    [InlineData("garbage", "1", StopKind.Never)]
    public void Parse_AcceptsSettingsNames(string kind, string value, StopKind expected) {
      var r = StopRule.Parse(kind, value);
      Assert.Equal(expected, r.Kind);
      if (expected == StopKind.RatioAbove) Assert.Equal(1.5, r.Value);
    }
  }

  public class FormatTests {

    [Theory]
    [InlineData(0, "0 bytes")]
    [InlineData(512, "512 bytes")]
    [InlineData(1024, "1.00 KB")]
    [InlineData(1_572_864, "1.50 MB")]
    [InlineData(-5, "0 bytes")]
    public void Bytes_UsesBinaryUnitsAndTwoDecimals(long bytes, string expected) {
      Assert.Equal(expected, Format.Bytes(bytes));
    }

    [Fact]
    public void Ratio_IsDashForAPureSeeder() {
      Assert.Equal("—", Format.Ratio(123, 0));
      Assert.Equal("2.50", Format.Ratio(250, 100));
    }

    [Fact]
    public void Duration_ShowsDaysPastTwentyFourHours() {
      Assert.Equal("00:01:05", Format.Duration(TimeSpan.FromSeconds(65)));
      Assert.Equal("1d 02:00:00", Format.Duration(TimeSpan.FromHours(26)));
      Assert.Equal("00:00:00", Format.Duration(TimeSpan.FromSeconds(-3)));
    }
  }

  public class CommandLineTests {

    [Fact]
    public void Value_NeverReturnsAnotherOption() {
      var c = new CommandLine(new[] { "-t", "--quiet", "-u", "800" });
      Assert.Null(c.Value("--torrent", "-t"));
      Assert.Equal("800", c.Value("--upload", "-u"));
      Assert.True(c.Has("--quiet", "-q"));
    }

    [Fact]
    public void NegativeNumbers_AreValuesNotOptions() {
      Assert.False(CommandLine.IsOption("-5"));
      Assert.True(CommandLine.IsOption("-t"));
      Assert.True(CommandLine.IsOption("--torrent"));
      Assert.False(CommandLine.IsOption("-"));
    }

    [Fact]
    public void Bool_UnderstandsOnOffAndKeepsTheFallbackOtherwise() {
      var c = new CommandLine(new[] { "--realistic", "off", "--swarm-aware", "maybe" });
      Assert.False(c.Bool(true, "--realistic"));
      Assert.True(c.Bool(true, "--swarm-aware"));
      Assert.True(c.Bool(true, "--missing"));
    }

    [Fact]
    public void Unknown_ListsOptionsNotInTheKnownSet() {
      var c = new CommandLine(new[] { "--torrent", "a.torrent", "--turbo", "-x", "3" });
      var unknown = c.Unknown(new[] { "--torrent", "-t" });
      Assert.Equal(new[] { "--turbo", "-x" }, unknown);
    }

    [Fact]
    public void IsCliInvocation_SpotsHeadlessFlags() {
      Assert.True(CliApp.IsCliInvocation(new[] { "--test-announce", "-t", "x" }));
      Assert.True(CliApp.IsCliInvocation(new[] { "--help" }));
      Assert.False(CliApp.IsCliInvocation(Array.Empty<string>()));
      Assert.False(CliApp.IsCliInvocation(new[] { "--torrent", "x.torrent" }));
    }

    [Fact]
    public void Run_UnknownOption_IsAUsageError() {
      var o = new StringWriter(); var e = new StringWriter();
      Assert.Equal(CliApp.Usage, CliApp.Run(new[] { "--torrent", "x.torrent", "--turbo" }, o, e));
      Assert.Contains("--turbo", e.ToString());
    }

    [Fact]
    public void Run_Help_ListsEveryDocumentedOption() {
      var o = new StringWriter();
      Assert.Equal(CliApp.Ok, CliApp.Run(new[] { "--help" }, o, new StringWriter()));
      var help = o.ToString();
      foreach (var opt in new[] { "--torrent", "--folder", "--test-announce", "--daemon", "--client", "--connection", "--stop-after", "--proxy-type", "--duration" })
        Assert.Contains(opt, help);
    }

    [Fact]
    public void BuildProxy_ReadsTheProxyFlags() {
      var p = CliApp.BuildProxy(new CommandLine(new[] { "--proxy-type", "socks5", "--proxy-host", "p.example", "--proxy-port", "1080", "--proxy-user", "u" }));
      Assert.Equal(ProxyType.Socks5, p.Type);
      Assert.Equal("p.example", p.Host);
      Assert.Equal(1080, p.Port);
      Assert.True(p.Enabled);
    }
  }

  public class AnnounceProbeTests {
    static AnnounceProbeTests() { Encodings.Register(); }

    private static Torrent TorrentFor(int port) {
      var path = TorrentBuilder.Write($"http://127.0.0.1:{port}/announce", null);
      try { return new Torrent(path); } finally { File.Delete(path); }
    }

    [Fact]
    public void Accepted_ReadsTheSwarmAndSendsAStopped() {
      using var tracker = FakeHttpServer.Start("d8:completei9e10:incompletei6e8:intervali1800ee");
      var p = AnnounceProbe.Run(TorrentFor(tracker.Port), TorrentClientFactory.GetClient("qBittorrent 5.2.4"), ProxySettings.None, null);
      Assert.True(p.GotResponse);
      Assert.True(p.Accepted);
      Assert.True(p.Worthwhile);
      Assert.Equal(9, p.Seeders);
      Assert.Equal(6, p.Leechers);
      Assert.Equal(1800, p.Interval);
      Assert.Contains("event=stopped", tracker.LastRequest); // no phantom seeder left behind
    }

    [Fact]
    public void Rejected_CarriesTheFailureReason() {
      using var tracker = FakeHttpServer.Start("d14:failure reason17:unregistered infoe");
      var p = AnnounceProbe.Run(TorrentFor(tracker.Port), TorrentClientFactory.GetClient("qBittorrent 5.2.4"), ProxySettings.None, null);
      Assert.True(p.GotResponse);
      Assert.False(p.Accepted);
      Assert.Equal("unregistered info", p.FailureReason);
      Assert.Null(p.Error);
    }

    [Fact]
    public void AcceptedButEmptySwarm_IsNotWorthwhile() {
      using var tracker = FakeHttpServer.Start("d8:completei3e10:incompletei0e8:intervali1800ee");
      var p = AnnounceProbe.Run(TorrentFor(tracker.Port), TorrentClientFactory.GetClient("qBittorrent 5.2.4"), ProxySettings.None, null);
      Assert.True(p.Accepted);
      Assert.False(p.Worthwhile);
    }

    [Fact]
    public void Unreachable_IsAnErrorNotAnException() {
      var p = AnnounceProbe.Run(TorrentFor(1), TorrentClientFactory.GetClient("qBittorrent 5.2.4"), ProxySettings.None, null);
      Assert.False(p.GotResponse);
      Assert.False(p.Accepted);
      Assert.NotNull(p.Error);
    }
  }

  public class SeedEngineOptionsTests {
    static SeedEngineOptionsTests() { Encodings.Register(); }

    [Fact]
    public void Options_AreValidated() {
      Assert.Throws<ArgumentException>(() => new SeedEngine(new SeedOptions()));
      Assert.Throws<ArgumentNullException>(() => new SeedEngine(null));
    }

    [Fact]
    public void Overrides_ReachTheEngine_AndDefaultsComeFromTheClient() {
      var path = TorrentBuilder.Write("http://only.test/announce", null);
      try {
        var t = new Torrent(path);
        var engine = new SeedEngine(new SeedOptions {
          Torrent = t, Client = TorrentClientFactory.GetClient("Azureus 3.1.1.0"), Port = 51413, AnnounceIntervalSeconds = 600,
        });
        Assert.Equal(51413, engine.Port);
        Assert.Equal(600, engine.IntervalSeconds);
        Assert.Equal(SeedState.Idle, engine.State);
        Assert.False(engine.IsRunning);
        Assert.Equal(TimeSpan.Zero, engine.Elapsed);
        Assert.Empty(engine.UploadHistory);
        Assert.Equal("Azureus 3.1.1.0", engine.ClientName);
      }
      finally { File.Delete(path); }
    }

    [Fact]
    public void CampaignEngine_RunsWithItsOwnStealthProfile() {
      var shared = new StealthOptions { ActiveHoursEnabled = false, RealisticSpeed = false };
      var c = new Campaign { UseActiveHours = true, ActiveHoursStart = 1, ActiveHoursEnd = 2, TorrentFolder = Path.GetTempPath() };
      using var engine = new CampaignEngine(c, null, null, shared);
      // The campaign applied its own window to a clone; the caller's profile is untouched.
      Assert.False(shared.ActiveHoursEnabled);
      Assert.False(engine.Running);
      Assert.Equal(0, engine.TorrentCount);
    }

    [Fact]
    public void FromMagnet_MakesAnAnnounceableVirtualTorrent() {
      var m = Magnet.Parse("magnet:?xt=urn:btih:0123456789abcdef0123456789abcdef01234567&dn=Sample&tr=udp%3A%2F%2Fx.test%3A80&tr=http%3A%2F%2Ft.test%2Fannounce");
      var t = Torrent.FromMagnet(m, 700UL * 1024 * 1024);
      Assert.Equal("Sample", t.Name);
      Assert.Equal(700UL * 1024 * 1024, t.totalLength);
      Assert.Equal("http://t.test/announce", t.Announce);
      Assert.Equal(40, Hex.Lower(t.InfoHash).Length);
      Assert.Equal(0, t.PieceCount);
    }
  }
}
