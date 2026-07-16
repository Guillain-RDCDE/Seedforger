using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace Seedforger {

  /// <summary>
  /// DNS resolution that survives ISP tracker-blocking. Many ISPs sinkhole tracker
  /// domains to 127.0.0.1 (or 0.0.0.0) via their DNS. When the system resolver hands
  /// back such a bogus address, we resolve the real IP over DNS-over-HTTPS (Cloudflare,
  /// reached by literal IP so it needs no DNS itself) and connect to that instead —
  /// the Host header and TLS SNI still carry the real hostname, so the tracker is happy.
  /// </summary>
  internal static class SecureDns {

    /// <summary>Optional sink for a one-line note when a block is bypassed.</summary>
    internal static Action<string> Log;

    private static readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
    private static readonly ConcurrentDictionary<string, IPAddress> cache = new ConcurrentDictionary<string, IPAddress>();

    /// <summary>Resolve a host to a real, routable IP (or null to let the caller try the OS resolver).</summary>
    internal static IPAddress Resolve(string host) {
      if (string.IsNullOrEmpty(host)) return null;
      if (IPAddress.TryParse(host, out var literal)) return literal;
      if (cache.TryGetValue(host, out var hit)) return hit;

      // 1) System resolver — accept only if it isn't a sinkhole.
      try {
        foreach (var a in Dns.GetHostAddresses(host))
          if (!IsBogus(a)) { cache[host] = a; return a; }
      }
      catch { /* fall through to DoH */ }

      // 2) The OS answer was missing or a 127.0.0.1-style block → go over DoH.
      var real = ResolveDoH(host);
      if (real != null) {
        cache[host] = real;
        try { Log?.Invoke($"DNS: '{host}' is blocked by your resolver — using {real} via Cloudflare DoH."); } catch { }
        return real;
      }
      return null;
    }

    private static bool IsBogus(IPAddress a) =>
      a == null || IPAddress.IsLoopback(a) || a.Equals(IPAddress.Any) || a.Equals(IPAddress.IPv6Any);

    private static IPAddress ResolveDoH(string host) {
      foreach (var server in new[] { "1.1.1.1", "1.0.0.1" }) {
        try {
          using (var req = new HttpRequestMessage(HttpMethod.Get,
                   $"https://{server}/dns-query?name={Uri.EscapeDataString(host)}&type=A")) {
            req.Headers.Accept.ParseAdd("application/dns-json");
            using (var resp = http.Send(req))
            using (var stream = resp.Content.ReadAsStream())
            using (var doc = JsonDocument.Parse(stream)) {
              if (!doc.RootElement.TryGetProperty("Answer", out var answers)) continue;
              foreach (var ans in answers.EnumerateArray()) {
                if (ans.TryGetProperty("type", out var t) && t.GetInt32() == 1 /* A */
                    && ans.TryGetProperty("data", out var d)
                    && IPAddress.TryParse(d.GetString(), out var ip) && !IsBogus(ip))
                  return ip;
              }
            }
          }
        }
        catch { /* try the next server, then give up */ }
      }
      return null;
    }
  }
}
