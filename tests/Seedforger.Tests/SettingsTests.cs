using System.Text.Json;
using Seedforger;
using Seedforger.Net;
using Xunit;

namespace Seedforger.Tests {

  public class SettingsTests {

    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions {
      WriteIndented = true,
      PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public void Defaults_AreTheSafeOnes() {
      var s = new Settings();
      Assert.False(s.BalloonTip);
      Assert.True(s.MinimizeToTray);
      Assert.True(s.CloseToTray);
      Assert.True(s.RealisticSpeed);
      Assert.True(s.SwarmAware);
      Assert.False(s.RandomizeClientOnStart);
      Assert.Equal("qBittorrent", s.Client);
      Assert.Equal("5.2.4", s.ClientVersion);
      Assert.Equal(1024, s.UploadRate);
      Assert.Equal("Never", s.StopWhen);
      Assert.Equal("None", s.ProxyType);
      Assert.Equal(0, s.ProxyPort);
    }

    [Fact]
    public void RoundTrip_PreservesAllValues() {
      var original = new Settings {
        BalloonTip = true, MinimizeToTray = false, CloseToTray = false, RealisticSpeed = false, SwarmAware = false,
        RandomizeClientOnStart = true, ActiveHoursEnabled = true, ActiveHoursStart = 22, ActiveHoursEnd = 6,
        Language = "fr", GlobalUpstreamKBps = 1220,
        Client = "Transmission", ClientVersion = "9.9.9", UploadRate = 123, DownloadRate = 456, Interval = 42,
        StopWhen = "RatioAbove", StopAfter = 2.5,
        ProxyType = "Socks5", ProxyAddress = "127.0.0.1", ProxyUser = "user", ProxyPass = "pass", ProxyPort = 1080,
        CustomKey = "KEY123", CustomPeerID = "PEER123", CustomPeers = 5, CustomPort = 6881,
      };

      var json = JsonSerializer.Serialize(original, Options);
      var restored = JsonSerializer.Deserialize<Settings>(json, Options);

      Assert.NotNull(restored);
      Assert.Equal(original.BalloonTip, restored.BalloonTip);
      Assert.Equal(original.MinimizeToTray, restored.MinimizeToTray);
      Assert.Equal(original.CloseToTray, restored.CloseToTray);
      Assert.Equal(original.RealisticSpeed, restored.RealisticSpeed);
      Assert.Equal(original.SwarmAware, restored.SwarmAware);
      Assert.Equal(original.RandomizeClientOnStart, restored.RandomizeClientOnStart);
      Assert.Equal(original.ActiveHoursEnabled, restored.ActiveHoursEnabled);
      Assert.Equal(original.ActiveHoursStart, restored.ActiveHoursStart);
      Assert.Equal(original.ActiveHoursEnd, restored.ActiveHoursEnd);
      Assert.Equal(original.Language, restored.Language);
      Assert.Equal(original.GlobalUpstreamKBps, restored.GlobalUpstreamKBps);
      Assert.Equal(original.Client, restored.Client);
      Assert.Equal(original.ClientVersion, restored.ClientVersion);
      Assert.Equal(original.UploadRate, restored.UploadRate);
      Assert.Equal(original.DownloadRate, restored.DownloadRate);
      Assert.Equal(original.Interval, restored.Interval);
      Assert.Equal(original.StopWhen, restored.StopWhen);
      Assert.Equal(original.StopAfter, restored.StopAfter);
      Assert.Equal(original.ProxyType, restored.ProxyType);
      Assert.Equal(original.ProxyAddress, restored.ProxyAddress);
      Assert.Equal(original.ProxyUser, restored.ProxyUser);
      Assert.Equal(original.ProxyPass, restored.ProxyPass);
      Assert.Equal(original.ProxyPort, restored.ProxyPort);
      Assert.Equal(original.CustomKey, restored.CustomKey);
      Assert.Equal(original.CustomPeerID, restored.CustomPeerID);
      Assert.Equal(original.CustomPeers, restored.CustomPeers);
      Assert.Equal(original.CustomPort, restored.CustomPort);
    }

    [Fact]
    public void JsonNames_StayCompatibleWithOlderFiles() {
      // The historical (misspelt) names are what existing settings.json files contain.
      var json = JsonSerializer.Serialize(new Settings { BalloonTip = true, ProxyAddress = "p.example" }, Options);
      Assert.Contains("\"BallonTip\": true", json);
      Assert.Contains("\"ProxyAdress\": \"p.example\"", json);
    }

    [Fact]
    public void OlderFiles_WithNumbersStoredAsStrings_StillLoad() {
      const string json = "{ \"UploadRate\": \"10240\", \"DownloadRate\": \"30\", \"Interval\": \"300\", \"ProxyPort\": \"\", " +
                          "\"StopAfter\": \"0\", \"CustomPeers\": \"\", \"CustomPort\": \"6881\", \"TCPlistener\": true, \"MinRandUp\": \"1\" }";
      var s = JsonSerializer.Deserialize<Settings>(json, Options);
      Assert.NotNull(s);
      Assert.Equal(10240, s.UploadRate);
      Assert.Equal(30, s.DownloadRate);
      Assert.Equal(300, s.Interval);
      Assert.Equal(0, s.ProxyPort);     // "" → 0
      Assert.Equal(0, s.CustomPeers);
      Assert.Equal(6881, s.CustomPort);
      Assert.True(s.CloseToTray);        // unspecified fields keep their defaults; unknown ones are ignored
    }

    [Fact]
    public void Deserialize_IsCaseInsensitive() {
      const string json = "{ \"minimizetotray\": false, \"client\": \"lowercased\" }";
      var s = JsonSerializer.Deserialize<Settings>(json, Options);
      Assert.NotNull(s);
      Assert.False(s.MinimizeToTray);
      Assert.Equal("lowercased", s.Client);
      Assert.True(s.CloseToTray);
    }

    [Fact]
    public void RunPreferences_RoundTripThroughSettings() {
      var prefs = new RunPreferences {
        Proxy = ProxySettings.From(ProxyType.HttpConnect, "proxy.example", 3128, "u", "p"),
        StopWhen = StopRule.After(StopKind.UploadedMB, 500),
        AnnounceIntervalSeconds = 900, PeerIdOverride = "-qB5240-custom", KeyOverride = "ABCDEF01",
        Port = 51413, NumWant = 80, LeechDownloadKBps = 250,
      };
      var s = new Settings();
      prefs.SaveTo(s);
      var back = RunPreferences.FromSettings(s);
      Assert.Equal(prefs.Proxy, back.Proxy);
      Assert.Equal(StopKind.UploadedMB, back.StopWhen.Kind);
      Assert.Equal(500, back.StopWhen.Value);
      Assert.Equal(900, back.AnnounceIntervalSeconds);
      Assert.Equal("-qB5240-custom", back.PeerIdOverride);
      Assert.Equal("ABCDEF01", back.KeyOverride);
      Assert.Equal(51413, back.Port);
      Assert.Equal(80, back.NumWant);
      Assert.Equal(250, back.LeechDownloadKBps);

      var o = new SeedOptions();
      back.ApplyTo(o);
      Assert.Equal(51413, o.Port);
      Assert.Equal("-qB5240-custom", o.PeerIdOverride);
      Assert.Same(back.Proxy, o.Proxy);
    }

    [Fact]
    public void StealthOptions_RoundTripThroughSettings_AndCloneIsIndependent() {
      var s = new Settings { RealisticSpeed = false, SwarmAware = false, ActiveHoursEnabled = true, ActiveHoursStart = 22, ActiveHoursEnd = 6 };
      var st = new StealthOptions().LoadFrom(s);
      Assert.False(st.RealisticSpeed);
      Assert.True(st.ActiveHoursEnabled);
      var clone = st.Clone();
      clone.ActiveHoursEnabled = false;
      Assert.True(st.ActiveHoursEnabled);
      var back = new Settings();
      st.SaveTo(back);
      Assert.Equal(22, back.ActiveHoursStart);
      Assert.Equal(6, back.ActiveHoursEnd);
    }
  }
}
