using System.Net;
using System.Net.Sockets;

namespace Seedforger {

  /// <summary>The machine's own IPv4 address, for clients whose announce carries a
  /// <c>localip</c> parameter (BitComet). Falls back to loopback.</summary>
  internal static class LocalAddress {
    internal static string Ipv4() {
      try {
        foreach (var address in Dns.GetHostEntry(string.Empty).AddressList)
          if (address.AddressFamily == AddressFamily.InterNetwork) return address.ToString();
      }
      catch { /* no network stack: loopback below */ }
      return "127.0.0.1";
    }
  }
}
