using System;
using Seedforger.BitTorrent;
using Seedforger.Net;

namespace Seedforger {

  /// <summary>
  /// The structured result of a dry-run announce: one <c>started</c> as a complete
  /// seeder, read the answer, then a <c>stopped</c> so the tracker isn't left with
  /// a phantom peer. Tells a rejection (failure reason) apart from a transport
  /// error, and reports the swarm — what the guided setup and "Test announce" need.
  /// </summary>
  internal sealed class AnnounceProbe {

    public bool GotResponse { get; private set; }
    public bool Accepted { get; private set; }
    /// <summary>Set when the tracker answered with a "failure reason".</summary>
    public string FailureReason { get; private set; }
    /// <summary>Set when no usable answer came back (network, decode).</summary>
    public string Error { get; private set; }
    public int Seeders { get; private set; } = -1;
    public int Leechers { get; private set; } = -1;
    public int Interval { get; private set; } = -1;

    /// <summary>True when the tracker accepted and there is somebody to upload to.</summary>
    public bool Worthwhile => Accepted && Leechers > 0;

    /// <summary>Sends the probe synchronously (a few seconds at most) and returns
    /// what happened. Never throws.</summary>
    internal static AnnounceProbe Run(Torrent torrent, TorrentClient client, ProxySettings proxy, Action<string> log) {
      var probe = new AnnounceProbe();
      try {
        var tracker = PrimaryTracker(torrent);
        if (string.IsNullOrEmpty(tracker)) { probe.Error = "This torrent lists no tracker."; return probe; }

        var hashHex = Hex.Lower(torrent.InfoHash);
        var port = new Random().Next(1025, 65535).ToString();
        var numWant = client.DefNumWant > 0 ? client.DefNumWant.ToString() : "200";

        Announce.Params P(string ev) => Announce.ParamsFor(tracker, client, hashHex, client.PeerID, client.Key, port, numWant,
                                                           uploaded: 0, downloaded: 0, left: 0, totalSize: 0, ev);

        var resp = TrackerTransport.Fetch(new Uri(Announce.BuildUrl(P("&event=started"))), client, proxy, log);
        if (resp == null) { probe.Error = "No usable answer from the tracker (see the log)."; return probe; }
        probe.GotResponse = true;
        if (resp.Dict == null) { probe.Error = "The tracker's answer could not be decoded."; return probe; }

        var r = Announce.FromDict(resp.Dict);
        probe.Seeders = r.Seeders;
        probe.Leechers = r.Leechers;
        probe.Interval = r.Interval;
        if (!r.Accepted) { probe.FailureReason = r.Failure; return probe; }
        probe.Accepted = true;

        // Leave no phantom seeder behind.
        try { TrackerTransport.Fetch(new Uri(Announce.BuildUrl(P("&event=stopped"))), client, proxy, null); } catch { }
        return probe;
      }
      catch (Exception ex) {
        probe.Error = ex.Message;
        return probe;
      }
    }

    internal static string PrimaryTracker(Torrent torrent) {
      if (torrent == null) return null;
      var list = torrent.AnnounceList;
      if (list != null && list.Count > 0) return list[0];
      return torrent.Announce;
    }
  }

  /// <summary>Hex helpers for info-hashes.</summary>
  internal static class Hex {
    public static string Lower(byte[] bytes) => ToHex(bytes, "0123456789abcdef");
    public static string Upper(byte[] bytes) => ToHex(bytes, "0123456789ABCDEF");

    private static string ToHex(byte[] bytes, string digits) {
      if (bytes == null) return "";
      var c = new char[bytes.Length * 2];
      for (var i = 0; i < bytes.Length; i++) {
        c[i * 2] = digits[bytes[i] >> 4];
        c[i * 2 + 1] = digits[bytes[i] & 0xF];
      }
      return new string(c);
    }
  }
}
