using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Seedforger;
using Seedforger.BitTorrent;
using Xunit;

namespace Seedforger.Tests {

  /// <summary>
  /// Covers the believability hardening: the tracker's "min interval" floor, and
  /// BEP-12 multi-tracker announcing (announce-list parsed, de-duplicated, ordered
  /// primary-first, and surfaced to the engine).
  /// </summary>
  public class RealismTests {
    static RealismTests() { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); }

    // ---- min interval ----

    [Fact]
    public void FromBody_ReadsMinIntervalWhenPresent() {
      var body = Encoding.ASCII.GetBytes("d8:completei5e10:incompletei3e8:intervali1800e12:min intervali900ee");
      var r = Announce.ParseBody(body);
      Assert.Equal(1800, r.Interval);
      Assert.Equal(900, r.MinInterval);
    }

    [Fact]
    public void FromBody_MinIntervalDefaultsToUnsetWhenAbsent() {
      var body = Encoding.ASCII.GetBytes("d8:completei1e10:incompletei0e8:intervali1800ee");
      var r = Announce.ParseBody(body);
      Assert.Equal(-1, r.MinInterval);
    }

    // ---- announce-list (BEP-12) ----

    [Fact]
    public void AnnounceList_SingleTrackerTorrent_ReturnsJustThePrimary() {
      var path = TorrentBuilder.Write("http://primary.test/announce", null);
      try {
        var t = new Torrent(path);
        Assert.Equal(new[] { "http://primary.test/announce" }, new List<string>(t.AnnounceList).ToArray());
      }
      finally { File.Delete(path); }
    }

    [Fact]
    public void AnnounceList_FlattensTiers_PrimaryFirst_AndDeduplicates() {
      var path = TorrentBuilder.Write(
        "http://primary.test/announce",
        new[] {
          new[] { "http://primary.test/announce" },              // dup of primary
          new[] { "http://backup-a.test/announce", "http://backup-b.test/announce" },
          new[] { "http://backup-a.test/announce" },             // dup of a
        });
      try {
        var t = new Torrent(path);
        Assert.Equal(
          new[] {
            "http://primary.test/announce",
            "http://backup-a.test/announce",
            "http://backup-b.test/announce",
          },
          new List<string>(t.AnnounceList).ToArray());
      }
      finally { File.Delete(path); }
    }

    [Fact]
    public void SeedEngine_AnnouncesToEveryTrackerTheTorrentLists() {
      var path = TorrentBuilder.Write(
        "http://primary.test/announce",
        new[] { new[] { "http://backup.test/announce" } });
      try {
        var t = new Torrent(path);
        var client = TorrentClientFactory.GetClient("qBittorrent 5.2.3");
        var engine = new SeedEngine(t, client, new ProxyInfo(), 100, 0, 100);
        Assert.Equal(2, engine.TrackerCount);
      }
      finally { File.Delete(path); }
    }

    [Fact]
    public void SeedEngine_SingleTracker_HasOneTracker() {
      var path = TorrentBuilder.Write("http://only.test/announce", null);
      try {
        var t = new Torrent(path);
        var client = TorrentClientFactory.GetClient("qBittorrent 5.2.3");
        var engine = new SeedEngine(t, client, new ProxyInfo(), 100, 0, 100);
        Assert.Equal(1, engine.TrackerCount);
      }
      finally { File.Delete(path); }
    }
  }

  /// <summary>Writes a minimal but valid single-file .torrent, optionally with an
  /// <c>announce-list</c> (a list of tiers, each a list of URLs).</summary>
  internal static class TorrentBuilder {
    public static string Write(string announce, string[][] announceListTiers) {
      var body = new MemoryStream();
      void Raw(string s) { var b = Encoding.ASCII.GetBytes(s); body.Write(b, 0, b.Length); }
      void Str(string s) { Raw(s.Length + ":" + s); }

      Raw("d");
      Str("announce"); Str(announce);
      if (announceListTiers != null) {
        Str("announce-list");
        Raw("l");
        foreach (var tier in announceListTiers) {
          Raw("l");
          foreach (var url in tier) Str(url);
          Raw("e");
        }
        Raw("e");
      }
      Str("info");
      Raw("d");
      Str("length"); Raw("i100e");
      Str("name"); Str("test.bin");
      Str("piece length"); Raw("i16384e");
      Str("pieces"); Raw("20:"); body.Write(new byte[20], 0, 20); // one all-zero piece hash
      Raw("e"); // info
      Raw("e"); // root

      var path = Path.Combine(Path.GetTempPath(), "sf_torrent_" + Guid.NewGuid().ToString("N") + ".torrent");
      File.WriteAllBytes(path, body.ToArray());
      return path;
    }
  }
}
