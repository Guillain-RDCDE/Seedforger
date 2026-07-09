using System.Text.Json;
using Seedforger;
using Xunit;

namespace Seedforger.Tests {

  public class SettingsTests {

    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions {
      WriteIndented = true,
      PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public void Defaults_MatchLegacyRegistryDefaults() {
      var s = new Settings();

      // App-level
      Assert.False(s.BallonTip);
      Assert.True(s.MinimizeToTray);
      Assert.True(s.CloseToTray);
      Assert.True(s.RealisticSpeed);

      // A few representative per-tab defaults
      Assert.True(s.NewValues);
      Assert.Equal("qBittorrent", s.Client);
      Assert.Equal("5.2.3", s.ClientVersion);
      Assert.True(s.TCPlistener);
      Assert.True(s.ScrapeInfo);
      Assert.Equal("Never", s.StopWhen);
      Assert.Equal("None", s.ProxyType);
    }

    [Fact]
    public void RoundTrip_PreservesAllValues() {
      var original = new Settings {
        // bools flipped away from their defaults
        BallonTip = true,
        MinimizeToTray = false,
        CloseToTray = false,
        RealisticSpeed = false,
        NewValues = false,
        TCPlistener = false,
        ScrapeInfo = false,
        GetRandUp = false,
        GetRandDown = false,
        GetRandUpNext = true,
        GetRandDownNext = true,
        IgnoreFailureReason = true,

        // strings set to distinctive values
        Client = "Transmission",
        ClientVersion = "9.9.9",
        UploadRate = "123",
        DownloadRate = "456",
        Interval = "42",
        FileSize = "77",
        Directory = @"C:\some\path",
        MinRandUp = "2",
        MaxRandUp = "20",
        MinRandDown = "3",
        MaxRandDown = "30",
        CustomKey = "KEY123",
        CustomPeerID = "PEER123",
        CustomPeers = "5",
        CustomPort = "6881",
        StopWhen = "Time",
        StopAfter = "99",
        ProxyType = "SOCKS5",
        ProxyAdress = "127.0.0.1",
        ProxyUser = "user",
        ProxyPass = "pass",
        ProxyPort = "1080",
        MinRandUpNext = "11",
        MaxRandUpNext = "111",
        MinRandDownNext = "22",
        MaxRandDownNext = "222",
      };

      var json = JsonSerializer.Serialize(original, Options);
      var restored = JsonSerializer.Deserialize<Settings>(json, Options);

      Assert.NotNull(restored);

      // Bools
      Assert.Equal(original.BallonTip, restored.BallonTip);
      Assert.Equal(original.MinimizeToTray, restored.MinimizeToTray);
      Assert.Equal(original.CloseToTray, restored.CloseToTray);
      Assert.Equal(original.RealisticSpeed, restored.RealisticSpeed);
      Assert.Equal(original.NewValues, restored.NewValues);
      Assert.Equal(original.TCPlistener, restored.TCPlistener);
      Assert.Equal(original.ScrapeInfo, restored.ScrapeInfo);
      Assert.Equal(original.GetRandUp, restored.GetRandUp);
      Assert.Equal(original.GetRandDown, restored.GetRandDown);
      Assert.Equal(original.GetRandUpNext, restored.GetRandUpNext);
      Assert.Equal(original.GetRandDownNext, restored.GetRandDownNext);
      Assert.Equal(original.IgnoreFailureReason, restored.IgnoreFailureReason);

      // Strings
      Assert.Equal(original.Client, restored.Client);
      Assert.Equal(original.ClientVersion, restored.ClientVersion);
      Assert.Equal(original.UploadRate, restored.UploadRate);
      Assert.Equal(original.DownloadRate, restored.DownloadRate);
      Assert.Equal(original.Interval, restored.Interval);
      Assert.Equal(original.FileSize, restored.FileSize);
      Assert.Equal(original.Directory, restored.Directory);
      Assert.Equal(original.MinRandUp, restored.MinRandUp);
      Assert.Equal(original.MaxRandUp, restored.MaxRandUp);
      Assert.Equal(original.MinRandDown, restored.MinRandDown);
      Assert.Equal(original.MaxRandDown, restored.MaxRandDown);
      Assert.Equal(original.CustomKey, restored.CustomKey);
      Assert.Equal(original.CustomPeerID, restored.CustomPeerID);
      Assert.Equal(original.CustomPeers, restored.CustomPeers);
      Assert.Equal(original.CustomPort, restored.CustomPort);
      Assert.Equal(original.StopWhen, restored.StopWhen);
      Assert.Equal(original.StopAfter, restored.StopAfter);
      Assert.Equal(original.ProxyType, restored.ProxyType);
      Assert.Equal(original.ProxyAdress, restored.ProxyAdress);
      Assert.Equal(original.ProxyUser, restored.ProxyUser);
      Assert.Equal(original.ProxyPass, restored.ProxyPass);
      Assert.Equal(original.ProxyPort, restored.ProxyPort);
      Assert.Equal(original.MinRandUpNext, restored.MinRandUpNext);
      Assert.Equal(original.MaxRandUpNext, restored.MaxRandUpNext);
      Assert.Equal(original.MinRandDownNext, restored.MinRandDownNext);
      Assert.Equal(original.MaxRandDownNext, restored.MaxRandDownNext);
    }

    [Fact]
    public void Deserialize_IsCaseInsensitive() {
      const string json = "{ \"minimizetotray\": false, \"client\": \"lowercased\" }";
      var s = JsonSerializer.Deserialize<Settings>(json, Options);

      Assert.NotNull(s);
      Assert.False(s.MinimizeToTray);
      Assert.Equal("lowercased", s.Client);
      // Unspecified fields keep their defaults.
      Assert.True(s.CloseToTray);
    }
  }
}
