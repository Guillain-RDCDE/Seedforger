using System;
using System.IO;
using System.Text;
using Seedforger.BytesRoads;

namespace Seedforger {

  /// <summary>
  /// Sends one announce/scrape request to a tracker and returns the parsed
  /// response — HTTP (optionally through a SOCKS/HTTP-CONNECT proxy) or HTTPS over
  /// TLS. WinForms-free (logging is a callback), so it runs on any platform.
  /// Extracted verbatim from the legacy RM path; wire behaviour is unchanged.
  /// </summary>
  internal static class TrackerTransport {

    private static Encoding Enc => Encoding.GetEncoding(0x4e4); // Windows-1252

    internal static TrackerResponse Fetch(Uri reqUri, TorrentClient client, ProxyInfo proxy, Action<string> log) {
      if (reqUri.Scheme == "https") return FetchHttps(reqUri, client, proxy, log);

      SocketEx sock = null;
      try {
        var host = reqUri.Host;
        var port = reqUri.Port;
        var path = reqUri.PathAndQuery;
        log?.Invoke("Connecting to tracker (" + host + ") in port " + port);
        sock = CreateSocket(proxy, log);
        sock.PreAuthenticate = false;

        // Bypass ISP DNS sinkholes when connecting directly (a proxy does its own DNS).
        var connectHost = host;
        if (proxy.ProxyType == ProxyType.None) {
          var ip = SecureDns.Resolve(host);
          if (ip != null) connectHost = ip.ToString();
        }

        var attempts = 0;
        var connected = false;
        while (attempts < 5 && !connected) {
          try {
            sock.Connect(connectHost, port);
            connected = true;
            log?.Invoke("Connected Successfully");
          }
          catch (Exception ex) {
            log?.Invoke("Exception: " + ex.Message + "; Type: " + ex.GetType());
            log?.Invoke("Failed connection attempt: " + attempts);
            attempts++;
          }
        }

        var cmd = "GET " + path + " " + client.HttpProtocol + "\r\n" +
                  client.Headers.Replace("{host}", host) + "\r\n";
        log?.Invoke("======== Sending Command to Tracker ========");
        log?.Invoke(cmd);
        sock.Send(Enc.GetBytes(cmd));

        try {
          var data = new byte[32 * 1024];
          var memStream = new MemoryStream();
          while (true) {
            var dataLen = sock.Receive(data);
            if (dataLen == 0) break;
            memStream.Write(data, 0, dataLen);
          }

          if (memStream.Length == 0) {
            log?.Invoke("Error : Tracker Response is empty");
            return null;
          }

          var response = new TrackerResponse(memStream);
          if (response.doRedirect) return Fetch(new Uri(response.RedirectionURL), client, proxy, log);

          log?.Invoke("======== Tracker Response ========");
          log?.Invoke(response.Headers);
          if (response.Dict == null) {
            log?.Invoke("*** Failed to decode tracker response :");
            log?.Invoke(response.Body);
          }

          memStream.Dispose();
          return response;
        }
        catch (Exception ex) {
          sock.Close();
          log?.Invoke(Environment.NewLine + ex.Message);
          return null;
        }
      }
      catch (Exception ex) {
        sock?.Close();
        log?.Invoke("Exception:" + ex.Message);
        return null;
      }
    }

    private static TrackerResponse FetchHttps(Uri reqUri, TorrentClient client, ProxyInfo proxy, Action<string> log) {
      if (proxy.ProxyType != ProxyType.None) {
        log?.Invoke("HTTPS through a proxy is not supported yet - aborting to avoid leaking your real IP. " +
                    "Use an HTTP tracker URL with the proxy, or disable the proxy for HTTPS trackers.");
        return null;
      }

      try {
        var host = reqUri.Host;
        var port = reqUri.Port;
        var path = reqUri.PathAndQuery;
        log?.Invoke("Connecting to tracker (" + host + ") over TLS on port " + port);

        var cmd = "GET " + path + " " + client.HttpProtocol + "\r\n" +
                  client.Headers.Replace("{host}", host) + "\r\n";
        log?.Invoke("======== Sending Command to Tracker (TLS) ========");
        log?.Invoke(cmd);

        var responseBytes = HttpsTransport.Fetch(host, port, cmd, Enc);
        if (responseBytes.Length == 0) {
          log?.Invoke("Error : Tracker Response is empty");
          return null;
        }

        using var memStream = new MemoryStream(responseBytes);
        var response = new TrackerResponse(memStream);
        if (response.doRedirect) return Fetch(new Uri(response.RedirectionURL), client, proxy, log);

        log?.Invoke("======== Tracker Response ========");
        log?.Invoke(response.Headers);
        if (response.Dict == null) {
          log?.Invoke("*** Failed to decode tracker response :");
          log?.Invoke(response.Body);
        }

        return response;
      }
      catch (Exception ex) {
        log?.Invoke("HTTPS Exception: " + ex.Message);
        return null;
      }
    }

    private static SocketEx CreateSocket(ProxyInfo proxy, Action<string> log) {
      try {
        var sock = new SocketEx(proxy.ProxyType, proxy.ProxyServer, proxy.ProxyPort, proxy.ProxyUser, proxy.ProxyPassword);
        sock.SetTimeout(0x30d40);
        return sock;
      }
      catch (Exception ex) {
        log?.Invoke("createSocket error: " + ex.Message);
        return null;
      }
    }
  }
}
