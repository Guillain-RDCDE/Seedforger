using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Seedforger.Net {

  /// <summary>
  /// Opens a TCP connection to a host, either directly or through an HTTP-CONNECT,
  /// SOCKS4, SOCKS4a or SOCKS5 proxy, and hands back a plain <see cref="Stream"/>
  /// that owns the socket. Synchronous and small on purpose: the tracker protocol
  /// is one request / one response, so the whole exchange fits in a few hundred
  /// bytes and a single blocking call with timeouts is the simplest correct thing.
  ///
  /// Hostname resolution: SOCKS4a, SOCKS5 and HTTP CONNECT send the hostname to
  /// the proxy, which resolves it (no DNS leak). SOCKS4 and direct connections need
  /// an address here, which comes from <paramref name="resolve"/>.
  /// </summary>
  internal static class ProxyConnector {

    internal const int DefaultTimeoutMs = 30_000;

    /// <summary>
    /// Connects to <paramref name="host"/>:<paramref name="port"/> and returns a
    /// connected, bidirectional stream. Throws <see cref="IOException"/> (or a
    /// <see cref="SocketException"/>) when the connection or the proxy handshake
    /// fails — the message says which.
    /// </summary>
    internal static Stream Connect(string host, int port, ProxySettings proxy, int timeoutMs = DefaultTimeoutMs,
                                   Func<string, IPAddress> resolve = null) {
      if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("A host is required.", nameof(host));
      if (port <= 0 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
      proxy ??= ProxySettings.None;
      resolve ??= ResolveDefault;

      if (!proxy.Enabled)
        return OpenTcp(resolve(host) ?? ResolveDefault(host), host, port, timeoutMs);

      var stream = OpenTcp(resolve(proxy.Host) ?? ResolveDefault(proxy.Host), proxy.Host, proxy.Port, timeoutMs);
      try {
        switch (proxy.Type) {
          case ProxyType.HttpConnect: HttpConnect(stream, host, port, proxy); break;
          case ProxyType.Socks4: Socks4(stream, resolve(host) ?? ResolveDefault(host), port, proxy); break;
          case ProxyType.Socks4a: Socks4a(stream, host, port, proxy); break;
          case ProxyType.Socks5: Socks5(stream, host, port, proxy); break;
          default: throw new IOException("Unknown proxy type " + proxy.Type);
        }
        return stream;
      }
      catch {
        stream.Dispose();
        throw;
      }
    }

    // ---- plain TCP ----

    private static IPAddress ResolveDefault(string host) {
      if (IPAddress.TryParse(host, out var literal)) return literal;
      foreach (var a in Dns.GetHostAddresses(host))
        if (a.AddressFamily == AddressFamily.InterNetwork) return a;
      foreach (var a in Dns.GetHostAddresses(host)) return a;
      throw new IOException("Could not resolve " + host);
    }

    private static Stream OpenTcp(IPAddress address, string displayName, int port, int timeoutMs) {
      var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) {
        SendTimeout = timeoutMs, ReceiveTimeout = timeoutMs, NoDelay = true,
      };
      try {
        var connect = socket.BeginConnect(address, port, null, null);
        if (!connect.AsyncWaitHandle.WaitOne(timeoutMs)) {
          socket.Close();
          throw new IOException($"Connection to {displayName}:{port} timed out after {timeoutMs / 1000}s.");
        }
        socket.EndConnect(connect);
        return new NetworkStream(socket, ownsSocket: true);
      }
      catch (SocketException ex) {
        socket.Dispose();
        throw new IOException($"Could not connect to {displayName}:{port} — {ex.Message}", ex);
      }
    }

    // ---- HTTP CONNECT (RFC 7231 §4.3.6) ----

    private static void HttpConnect(Stream s, string host, int port, ProxySettings proxy) {
      var sb = new StringBuilder();
      sb.Append("CONNECT ").Append(host).Append(':').Append(port).Append(" HTTP/1.1\r\n");
      sb.Append("Host: ").Append(host).Append(':').Append(port).Append("\r\n");
      if (proxy.HasCredentials) {
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes(proxy.User + ":" + proxy.Password));
        sb.Append("Proxy-Authorization: Basic ").Append(token).Append("\r\n");
      }
      sb.Append("Proxy-Connection: keep-alive\r\n\r\n");
      Write(s, Encoding.ASCII.GetBytes(sb.ToString()));

      var head = ReadHttpHead(s);
      var firstLine = head.Split('\n')[0].Trim();
      var parts = firstLine.Split(' ');
      if (parts.Length < 2 || !int.TryParse(parts[1], out var status) || status < 200 || status > 299)
        throw new IOException("HTTP proxy refused the tunnel: " + firstLine);
    }

    private static string ReadHttpHead(Stream s) {
      var buf = new MemoryStream();
      var one = new byte[1];
      while (buf.Length < 64 * 1024) {
        if (s.Read(one, 0, 1) <= 0) break;
        buf.WriteByte(one[0]);
        if (buf.Length >= 4) {
          var b = buf.GetBuffer();
          var n = (int) buf.Length;
          if (b[n - 4] == '\r' && b[n - 3] == '\n' && b[n - 2] == '\r' && b[n - 1] == '\n') break;
        }
      }
      return Encoding.ASCII.GetString(buf.ToArray());
    }

    // ---- SOCKS4 / SOCKS4a ----

    private static void Socks4(Stream s, IPAddress address, int port, ProxySettings proxy) {
      var ip = address.AddressFamily == AddressFamily.InterNetwork
        ? address.GetAddressBytes()
        : throw new IOException("SOCKS4 only carries IPv4 addresses; use SOCKS4a or SOCKS5.");
      var user = Encoding.ASCII.GetBytes(proxy.User ?? "");
      var req = new byte[9 + user.Length];
      req[0] = 4; req[1] = 1;
      req[2] = (byte) (port >> 8); req[3] = (byte) port;
      Array.Copy(ip, 0, req, 4, 4);
      Array.Copy(user, 0, req, 8, user.Length);
      req[req.Length - 1] = 0;
      Write(s, req);
      ReadSocks4Reply(s);
    }

    private static void Socks4a(Stream s, string host, int port, ProxySettings proxy) {
      var user = Encoding.ASCII.GetBytes(proxy.User ?? "");
      var name = Encoding.ASCII.GetBytes(host);
      var req = new byte[9 + user.Length + name.Length + 1];
      req[0] = 4; req[1] = 1;
      req[2] = (byte) (port >> 8); req[3] = (byte) port;
      req[4] = 0; req[5] = 0; req[6] = 0; req[7] = 1;           // 0.0.0.1 = "resolve the name that follows"
      Array.Copy(user, 0, req, 8, user.Length);
      req[8 + user.Length] = 0;
      Array.Copy(name, 0, req, 9 + user.Length, name.Length);
      req[req.Length - 1] = 0;
      Write(s, req);
      ReadSocks4Reply(s);
    }

    private static void ReadSocks4Reply(Stream s) {
      var reply = ReadExactly(s, 8);
      if (reply[1] != 0x5A)
        throw new IOException("SOCKS4 proxy refused the connection (code 0x" + reply[1].ToString("X2") + ").");
    }

    // ---- SOCKS5 (RFC 1928, username/password per RFC 1929) ----

    private static void Socks5(Stream s, string host, int port, ProxySettings proxy) {
      // Method negotiation.
      var greeting = proxy.HasCredentials ? new byte[] { 5, 2, 0, 2 } : new byte[] { 5, 1, 0 };
      Write(s, greeting);
      var choice = ReadExactly(s, 2);
      if (choice[0] != 5) throw new IOException("Not a SOCKS5 proxy.");
      switch (choice[1]) {
        case 0x00: break;
        case 0x02:
          if (!proxy.HasCredentials) throw new IOException("SOCKS5 proxy requires a username and password.");
          Socks5Auth(s, proxy);
          break;
        case 0xFF: throw new IOException("SOCKS5 proxy accepted none of our authentication methods.");
        default: throw new IOException("SOCKS5 proxy asked for an unsupported authentication method.");
      }

      // CONNECT with a domain name: the proxy resolves it.
      var name = Encoding.ASCII.GetBytes(host);
      if (name.Length > 255) throw new IOException("Host name too long for SOCKS5.");
      var req = new byte[7 + name.Length];
      req[0] = 5; req[1] = 1; req[2] = 0; req[3] = 3; req[4] = (byte) name.Length;
      Array.Copy(name, 0, req, 5, name.Length);
      req[5 + name.Length] = (byte) (port >> 8);
      req[6 + name.Length] = (byte) port;
      Write(s, req);

      var head = ReadExactly(s, 4);
      if (head[1] != 0)
        throw new IOException("SOCKS5 proxy refused the connection: " + Socks5Error(head[1]));
      // Drain the bound address so the stream starts at the tunnelled bytes.
      switch (head[3]) {
        case 1: ReadExactly(s, 4 + 2); break;
        case 3: ReadExactly(s, ReadExactly(s, 1)[0] + 2); break;
        case 4: ReadExactly(s, 16 + 2); break;
        default: throw new IOException("SOCKS5 proxy sent an unknown address type.");
      }
    }

    private static void Socks5Auth(Stream s, ProxySettings proxy) {
      var user = Encoding.UTF8.GetBytes(proxy.User ?? "");
      var pass = Encoding.UTF8.GetBytes(proxy.Password ?? "");
      if (user.Length > 255 || pass.Length > 255) throw new IOException("SOCKS5 credentials too long.");
      var req = new byte[3 + user.Length + pass.Length];
      req[0] = 1; req[1] = (byte) user.Length;
      Array.Copy(user, 0, req, 2, user.Length);
      req[2 + user.Length] = (byte) pass.Length;
      Array.Copy(pass, 0, req, 3 + user.Length, pass.Length);
      Write(s, req);
      var reply = ReadExactly(s, 2);
      if (reply[1] != 0) throw new IOException("SOCKS5 proxy rejected the username/password.");
    }

    private static string Socks5Error(byte code) {
      switch (code) {
        case 1: return "general failure";
        case 2: return "connection not allowed by ruleset";
        case 3: return "network unreachable";
        case 4: return "host unreachable";
        case 5: return "connection refused";
        case 6: return "TTL expired";
        case 7: return "command not supported";
        case 8: return "address type not supported";
        default: return "error 0x" + code.ToString("X2");
      }
    }

    // ---- stream helpers ----

    private static void Write(Stream s, byte[] data) {
      s.Write(data, 0, data.Length);
      s.Flush();
    }

    private static byte[] ReadExactly(Stream s, int count) {
      var buf = new byte[count];
      var got = 0;
      while (got < count) {
        var n = s.Read(buf, got, count - got);
        if (n <= 0) throw new IOException("The proxy closed the connection during the handshake.");
        got += n;
      }
      return buf;
    }
  }
}
