using System.Linq;
using Seedforger;
using Xunit;

namespace Seedforger.Tests {
  public class TorrentClientFactoryTests {

    [Theory]
    [InlineData("qBittorrent 5.2.3", "-qB5230-", "qBittorrent/5.2.3")]
    [InlineData("qBittorrent 4.6.7", "-qB4670-", "qBittorrent/4.6.7")]
    [InlineData("Transmission 4.1.3", "-TR4130-", "Transmission/4.1.3")]
    [InlineData("Deluge 2.1.1", "-DE2110-", "Deluge/2.1.1")]
    [InlineData("libtorrent 2.1.0", "-LT2100-", "libtorrent/2.1.0")]
    public void GetClient_ModernFingerprints_HavePrefixAndUserAgent(
      string name, string expectedPrefix, string expectedUserAgent) {
      var client = TorrentClientFactory.GetClient(name);

      Assert.NotNull(client);
      Assert.StartsWith(expectedPrefix, client.PeerID);
      Assert.Contains(expectedUserAgent, client.Headers);
    }

    [Fact]
    public void GetClient_AzStylePeerId_IsAtLeastTwentyChars() {
      // Az-style ids are 8-char prefix + 12-char tail. Transmission's tail is
      // alphanumeric with UrlEncode=false, so no expansion => exactly 20 chars.
      var client = TorrentClientFactory.GetClient("Transmission 4.1.3");
      Assert.Equal(20, client.PeerID.Length);
      Assert.StartsWith("-TR4130-", client.PeerID);
    }

    [Fact]
    public void GetClient_UnknownName_FallsBackToUtorrent332() {
      var client = TorrentClientFactory.GetClient("client-inexistant-xyz");
      Assert.NotNull(client);
      Assert.StartsWith("-UT3320-", client.PeerID);
    }

    [Fact]
    public void GetFamilies_ContainsExpectedFamilies() {
      var families = TorrentClientFactory.GetFamilies();
      Assert.Contains("qBittorrent", families);
      Assert.Contains("Transmission", families);
      Assert.Contains("Deluge", families);
      Assert.Contains("libtorrent", families);
      Assert.Contains("uTorrent", families);
      Assert.Contains("BitComet", families);
    }

    [Fact]
    public void GetFamilies_HasNoDuplicates() {
      var families = TorrentClientFactory.GetFamilies();
      Assert.Equal(families.Distinct().Count(), families.Count);
    }

    [Fact]
    public void GetVersions_QBittorrent_ContainsKnownVersions() {
      var versions = TorrentClientFactory.GetVersions("qBittorrent");
      Assert.Contains("5.2.3", versions);
      Assert.Contains("4.6.7", versions);
    }

    [Fact]
    public void GetVersions_Transmission_ContainsKnownVersion() {
      var versions = TorrentClientFactory.GetVersions("Transmission");
      Assert.Contains("4.1.3", versions);
    }

    [Fact]
    public void GetVersions_UnknownFamily_ReturnsEmpty() {
      var versions = TorrentClientFactory.GetVersions("does-not-exist");
      Assert.Empty(versions);
    }

    [Fact]
    public void GetDefaultNumWant_Azureus_Is50() {
      Assert.Equal(50, TorrentClientFactory.GetDefaultNumWant("Azureus"));
    }

    [Fact]
    public void GetDefaultNumWant_UnknownFamily_DefaultsTo200() {
      Assert.Equal(200, TorrentClientFactory.GetDefaultNumWant("does-not-exist"));
    }
  }
}
