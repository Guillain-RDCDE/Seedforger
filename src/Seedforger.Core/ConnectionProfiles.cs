namespace Seedforger {

  /// <summary>A believable upload/download speed preset (values in kB/s).</summary>
  internal sealed class ConnectionProfile {
    internal readonly string Name;
    internal readonly int UpKBps;
    internal readonly int DownKBps;
    internal ConnectionProfile(string name, int upKBps, int downKBps) {
      Name = name; UpKBps = upKBps; DownKBps = downKBps;
    }
  }

  /// <summary>
  /// Ready-made connection profiles so the announced speeds look like a real
  /// line instead of a made-up number. Only the upload rate really matters for
  /// ratio, but download is set too for consistency. Values are typical
  /// residential up/down speeds converted to kB/s (1 Mbps ~= 122 kB/s).
  /// </summary>
  internal static class ConnectionProfiles {
    internal static readonly ConnectionProfile[] All = {
      new ConnectionProfile("ADSL  (1 up / 10 down Mbps)",        120,   1220),
      new ConnectionProfile("ADSL2+  (1 / 20 Mbps)",              120,   2440),
      new ConnectionProfile("VDSL2  (10 / 50 Mbps)",             1220,   6100),
      new ConnectionProfile("Cable  (10 / 100 Mbps)",            1220,  12200),
      new ConnectionProfile("Fibre  100 / 100 Mbps",            12200,  12200),
      new ConnectionProfile("Fibre  300 / 300 Mbps",            36600,  36600),
      new ConnectionProfile("Fibre  300 up / 1 Gbps down",      36600, 122000),
      new ConnectionProfile("4G / LTE  (20 / 50 Mbps)",          2440,   6100),
      new ConnectionProfile("5G  (60 / 300 Mbps)",               7300,  36600),
    };
  }
}
