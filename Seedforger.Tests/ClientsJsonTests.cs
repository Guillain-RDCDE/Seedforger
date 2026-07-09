using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Seedforger;
using Xunit;

namespace Seedforger.Tests {

  /// <summary>
  /// Guards the external clients.json contract: the built-in profiles must
  /// serialize and deserialize back losslessly (this is what ExportSampleIfMissing
  /// writes and MergeExternalOverrides reads).
  /// </summary>
  public class ClientsJsonTests {

    [Fact]
    public void Profiles_RoundTripThroughJson() {
      var json = JsonSerializer.Serialize(DefaultClientProfiles.All,
        new JsonSerializerOptions { WriteIndented = true });

      var back = JsonSerializer.Deserialize<List<ClientProfile>>(json,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

      Assert.NotNull(back);
      Assert.Equal(DefaultClientProfiles.All.Count, back.Count);

      foreach (var original in DefaultClientProfiles.All) {
        var copy = back.Single(p => p.FullName == original.FullName);
        Assert.Equal(original.Family, copy.Family);
        Assert.Equal(original.Version, copy.Version);
        Assert.Equal(original.PeerIdPrefix, copy.PeerIdPrefix);
        Assert.Equal(original.Headers, copy.Headers);
        Assert.Equal(original.Query, copy.Query);
        Assert.Equal(original.DefNumWant, copy.DefNumWant);
        Assert.Equal(original.HashUpperCase, copy.HashUpperCase);
      }
    }

    [Fact]
    public void ModernClient_SurvivesJsonRoundTrip() {
      var json = JsonSerializer.Serialize(DefaultClientProfiles.All);
      var back = JsonSerializer.Deserialize<List<ClientProfile>>(json,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

      var qbt = back.Single(p => p.FullName == "qBittorrent 5.2.3");
      Assert.Equal("-qB5230-", qbt.PeerIdPrefix);
      Assert.Contains("qBittorrent/5.2.3", qbt.Headers);
    }
  }
}
