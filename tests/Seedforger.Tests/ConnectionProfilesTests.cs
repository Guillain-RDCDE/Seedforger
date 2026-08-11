using System.Linq;
using Seedforger;
using Xunit;

namespace Seedforger.Tests {

  public class ConnectionProfilesTests {

    [Fact]
    public void All_HaveSaneValues() {
      Assert.NotEmpty(ConnectionProfiles.All);
      foreach (var p in ConnectionProfiles.All) {
        Assert.False(string.IsNullOrWhiteSpace(p.Name));
        Assert.InRange(p.UpKBps, 1, 2_000_000);
        Assert.InRange(p.DownKBps, 1, 2_000_000);
      }
    }

    [Fact]
    public void Covers_TheCommonConnectionTypes() {
      var names = ConnectionProfiles.All.Select(p => p.Name).ToList();
      Assert.Contains(names, n => n.Contains("ADSL"));
      Assert.Contains(names, n => n.Contains("Fibre"));
      Assert.Contains(names, n => n.Contains("4G") || n.Contains("5G"));
    }
  }
}
