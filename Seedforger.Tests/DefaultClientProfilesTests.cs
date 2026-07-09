using System.Linq;
using Seedforger;
using Xunit;

namespace Seedforger.Tests {
  public class DefaultClientProfilesTests {

    [Fact]
    public void All_ContainsExactly47Profiles() {
      Assert.Equal(47, DefaultClientProfiles.All.Count);
    }

    [Fact]
    public void All_HasNoDuplicateFullNames() {
      var names = DefaultClientProfiles.All.Select(p => p.FullName).ToList();
      Assert.Equal(names.Distinct().Count(), names.Count);
    }

    [Fact]
    public void All_EveryProfileHasRequiredFields() {
      foreach (var p in DefaultClientProfiles.All) {
        Assert.False(string.IsNullOrWhiteSpace(p.Family), $"Family missing on {p.FullName}");
        Assert.False(string.IsNullOrWhiteSpace(p.Version), $"Version missing on {p.FullName}");
        Assert.False(string.IsNullOrWhiteSpace(p.Headers), $"Headers missing on {p.FullName}");
        Assert.False(string.IsNullOrWhiteSpace(p.Query), $"Query missing on {p.FullName}");
        Assert.NotNull(p.Key);
      }
    }

    [Fact]
    public void FullName_CombinesFamilyAndVersion() {
      var profile = DefaultClientProfiles.All.First(p => p.Family == "qBittorrent" && p.Version == "5.2.3");
      Assert.Equal("qBittorrent 5.2.3", profile.FullName);
    }
  }
}
