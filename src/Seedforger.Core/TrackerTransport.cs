using System;
using System.IO;
using System.Net.Security;
using System.Text;
using Seedforger.Net;

namespace Seedforger {

  /// <summary>
  /// Sends one announce (or scrape) request to a tracker and returns the parsed
  /// response. HTTP and HTTPS, directly or through any proxy
  /// <see cref="ProxyConnector"/> speaks — HTTPS through a proxy is a CONNECT
  /// tunnel with TLS inside, so the proxy never sees the announce.
  ///
  /// The request is a hand-built raw HTTP/1.x message: header order and the
  /// User-Agent are part of a client's fingerprint, so nothing here goes through
  /// HttpClient, which would normalise them. Logging is a callback and the whole
  /// class is static and WinForms-free, so it runs on every platform.
  /// </summary>
  internal static class TrackerTransport {

    private const int MaxRedirects = 3;
    private const int ConnectAttempts = 3;
    internal const int TimeoutMs = 30_000;

    /// <summary>Windows-1252: the encoding real clients use for raw announce bytes.</summary>
    internal static Encoding WireEncoding => Encodings.Win1252;

    /// <summary>Announces to <paramref name="url"/> as <paramref name="client"/> and
    /// returns the parsed tracker response, or null (after logging why).</summary>
    internal static TrackerResponse Fetch(Uri url, TorrentClient client, ProxySettings proxy, Action<string> log) =>
      Fetch(url, client, proxy, log, MaxRedirects);

    private static TrackerResponse Fetch(Uri url, TorrentClient client, ProxySettings proxy, Action<string> log, int redirectsLeft) {
      var tls = string.Equals(url.Scheme, "https", StringComparison.OrdinalIgnoreCase);
      var request = BuildRequest(url, client);
      log?.Invoke($"Connecting to tracker {url.Host}:{url.Port}{(tls ? " over TLS" : "")} ({proxy ?? ProxySettings.None})");
      log?.Invoke("======== Sending Command to Tracker ========");
      log?.Invoke(request);

      byte[] raw;
      try { raw = FetchRaw(url.Host, url.Port, tls, request, proxy, TimeoutMs, log); }
      catch (Exception ex) {
        log?.Invoke("Tracker request failed: " + ex.Message);
        return null;
      }
      if (raw.Length == 0) { log?.Invoke("Error: the tracker response is empty."); return null; }

      TrackerResponse response;
      using (var ms = new MemoryStream(raw)) response = new TrackerResponse(ms);

      if (response.doRedirect) {
        if (redirectsLeft <= 0 || string.IsNullOrEmpty(response.RedirectionURL)) {
          log?.Invoke("Too many redirects — giving up.");
          return null;
        }
        log?.Invoke("Redirected to " + response.RedirectionURL);
        return Fetch(new Uri(response.RedirectionURL), client, proxy, log, redirectsLeft - 1);
      }

      log?.Invoke("======== Tracker Response ========");
      log?.Invoke(response.Headers);
      if (response.Dict == null) {
        log?.Invoke("*** Failed to decode tracker response:");
        log?.Invoke(response.Body);
      }
      return response;
    }

    /// <summary>The exact bytes a client would send: request line, the client's
    /// own header block (with {host} filled in), and the blank line.</summary>
    internal static string BuildRequest(Uri url, TorrentClient client) =>
      "GET " + url.PathAndQuery + " " + client.HttpProtocol + "\r\n" +
      client.Headers.Replace("{host}", url.Host) + "\r\n";

    /// <summary>
    /// Opens the connection (direct or via proxy, TLS or not), writes
    /// <paramref name="rawRequest"/> and returns everything the server sends until
    /// it closes the connection (clients send "Connection: close"). Connection
    /// attempts are retried a few times; a failure is thrown with its reason.
    /// </summary>
    internal static byte[] FetchRaw(string host, int port, bool tls, string rawRequest, ProxySettings proxy,
                                    int timeoutMs = TimeoutMs, Action<string> log = null,
                                    Func<string, System.Net.IPAddress> resolve = null) {
      proxy ??= ProxySettings.None;
      resolve ??= SecureDns.Resolve;
      Exception last = null;
      for (var attempt = 1; attempt <= ConnectAttempts; attempt++) {
        Stream stream = null;
        try {
          // Direct connections resolve through SecureDns (bypasses ISP sinkholes);
          // SOCKS4a/5 and HTTP CONNECT let the proxy resolve the name instead.
          stream = ProxyConnector.Connect(host, port, proxy, timeoutMs, resolve);
          if (tls) {
            // Trackers often run self-signed certificates; be lenient like a torrent client.
            var ssl = new SslStream(stream, false, (sender, cert, chain, errors) => true);
            ssl.AuthenticateAsClient(host);
            stream = ssl;
          }
          var bytes = WireEncoding.GetBytes(rawRequest);
          stream.Write(bytes, 0, bytes.Length);
          stream.Flush();
          return ReadToEnd(stream);
        }
        catch (Exception ex) {
          last = ex;
          log?.Invoke($"Connection attempt {attempt}/{ConnectAttempts} failed: {ex.Message}");
        }
        finally {
          stream?.Dispose();
        }
      }
      throw new IOException($"Could not reach {host}:{port} after {ConnectAttempts} attempts.", last);
    }

    private static byte[] ReadToEnd(Stream stream) {
      var buffer = new byte[32 * 1024];
      using var ms = new MemoryStream();
      int n;
      while ((n = stream.Read(buffer, 0, buffer.Length)) > 0) ms.Write(buffer, 0, n);
      return ms.ToArray();
    }
  }
}
