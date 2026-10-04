using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Seedforger;
using Seedforger.Net;
using Xunit;

namespace Seedforger.Tests {

  /// <summary>
  /// The tracker transport and the proxy connector, exercised against in-process
  /// fakes: a plain HTTP "tracker", a SOCKS5 proxy with username/password, an
  /// HTTP-CONNECT proxy, and a SOCKS4a proxy. No outbound network needed.
  /// </summary>
  public class TrackerTransportTests {
    static TrackerTransportTests() { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); }

    private const string Body = "d8:completei9e10:incompletei6e8:intervali1800ee";

    [Fact]
    public void FetchRaw_Direct_ReturnsTheWholeHttpResponse() {
      using var tracker = FakeHttpServer.Start(Body);
      var raw = TrackerTransport.FetchRaw("127.0.0.1", tracker.Port, false,
        "GET /announce HTTP/1.1\r\nHost: x\r\nConnection: close\r\n\r\n", ProxySettings.None);
      var text = Encoding.ASCII.GetString(raw);
      Assert.StartsWith("HTTP/1.1 200 OK", text);
      Assert.EndsWith(Body, text);
      Assert.Contains("GET /announce HTTP/1.1", tracker.LastRequest);
    }

    [Fact]
    public void Fetch_ParsesSwarmCountsFromTheBencodedAnswer() {
      using var tracker = FakeHttpServer.Start(Body);
      var client = TorrentClientFactory.GetClient("qBittorrent 5.2.4");
      var lines = new System.Collections.Generic.List<string>();
      var resp = TrackerTransport.Fetch(new Uri($"http://127.0.0.1:{tracker.Port}/announce?x=1"), client, ProxySettings.None, lines.Add);
      Assert.NotNull(resp?.Dict);
      var r = Announce.FromDict(resp.Dict);
      Assert.Equal(9, r.Seeders);
      Assert.Equal(6, r.Leechers);
      Assert.Equal(1800, r.Interval);
      Assert.Contains("User-Agent: qBittorrent/5.2.4", tracker.LastRequest);
      Assert.Contains(lines, l => l.Contains("Tracker Response"));
    }

    [Fact]
    public void Fetch_UnreachableTracker_ReturnsNullAndLogs() {
      var client = TorrentClientFactory.GetClient("qBittorrent 5.2.4");
      var lines = new System.Collections.Generic.List<string>();
      // Port 1 on loopback refuses instantly; the transport must not throw.
      var resp = TrackerTransport.Fetch(new Uri("http://127.0.0.1:1/announce"), client, ProxySettings.None, lines.Add);
      Assert.Null(resp);
      Assert.Contains(lines, l => l.Contains("failed"));
    }

    [Fact]
    public void ProxyConnector_Socks5_WithCredentials_TunnelsToTheTarget() {
      using var tracker = FakeHttpServer.Start(Body);
      using var proxy = FakeProxy.Start(FakeProxy.Kind.Socks5, "alice", "secret");
      var settings = ProxySettings.From(ProxyType.Socks5, "127.0.0.1", proxy.Port, "alice", "secret");

      var raw = TrackerTransport.FetchRaw("tracker.test", tracker.Port, false,
        "GET /a HTTP/1.1\r\nHost: tracker.test\r\nConnection: close\r\n\r\n", settings,
        resolve: h => IPAddress.Loopback);
      Assert.StartsWith("HTTP/1.1 200 OK", Encoding.ASCII.GetString(raw));
      Assert.Equal("tracker.test", proxy.RequestedHost);   // the proxy got the name, not an IP
      Assert.Equal(tracker.Port, proxy.RequestedPort);
      Assert.True(proxy.Authenticated);
    }

    [Fact]
    public void ProxyConnector_Socks5_WrongPassword_Fails() {
      using var proxy = FakeProxy.Start(FakeProxy.Kind.Socks5, "alice", "secret");
      var settings = ProxySettings.From(ProxyType.Socks5, "127.0.0.1", proxy.Port, "alice", "nope");
      var ex = Assert.Throws<IOException>(() => ProxyConnector.Connect("tracker.test", 80, settings, 5000, h => IPAddress.Loopback));
      Assert.Contains("username/password", ex.Message);
    }

    [Fact]
    public void ProxyConnector_HttpConnect_TunnelsToTheTarget() {
      using var tracker = FakeHttpServer.Start(Body);
      using var proxy = FakeProxy.Start(FakeProxy.Kind.HttpConnect, "", "");
      var settings = ProxySettings.From(ProxyType.HttpConnect, "127.0.0.1", proxy.Port);
      var raw = TrackerTransport.FetchRaw("tracker.test", tracker.Port, false,
        "GET /a HTTP/1.1\r\nHost: tracker.test\r\nConnection: close\r\n\r\n", settings,
        resolve: h => IPAddress.Loopback);
      Assert.StartsWith("HTTP/1.1 200 OK", Encoding.ASCII.GetString(raw));
      Assert.Equal("tracker.test", proxy.RequestedHost);
    }

    [Fact]
    public void ProxyConnector_Socks4a_TunnelsToTheTarget() {
      using var tracker = FakeHttpServer.Start(Body);
      using var proxy = FakeProxy.Start(FakeProxy.Kind.Socks4a, "", "");
      var settings = ProxySettings.From(ProxyType.Socks4a, "127.0.0.1", proxy.Port);
      var raw = TrackerTransport.FetchRaw("tracker.test", tracker.Port, false,
        "GET /a HTTP/1.1\r\nHost: tracker.test\r\nConnection: close\r\n\r\n", settings,
        resolve: h => IPAddress.Loopback);
      Assert.StartsWith("HTTP/1.1 200 OK", Encoding.ASCII.GetString(raw));
      Assert.Equal("tracker.test", proxy.RequestedHost);
    }

    [Theory]
    [InlineData("none", ProxyType.None)]
    [InlineData("http", ProxyType.HttpConnect)]
    [InlineData("HTTP CONNECT", ProxyType.HttpConnect)]
    [InlineData("socks4", ProxyType.Socks4)]
    [InlineData("Socks4a", ProxyType.Socks4a)]
    [InlineData("SOCKS5", ProxyType.Socks5)]
    [InlineData("garbage", ProxyType.None)]
    public void ProxySettings_ParseType_AcceptsTheNamesUsedBySettingsAndCli(string name, ProxyType expected) {
      Assert.Equal(expected, ProxySettings.ParseType(name));
    }

    [Fact]
    public void ProxySettings_RoundTripsThroughSettings() {
      var s = new Settings();
      ProxySettings.From(ProxyType.Socks5, "p.example", 1080, "u", "pw").SaveTo(s);
      var back = ProxySettings.FromSettings(s);
      Assert.Equal(ProxyType.Socks5, back.Type);
      Assert.Equal("p.example", back.Host);
      Assert.Equal(1080, back.Port);
      Assert.Equal("u", back.User);
      Assert.Equal("pw", back.Password);
      Assert.True(back.Enabled);
      Assert.False(ProxySettings.None.Enabled);
    }
  }

  /// <summary>A one-connection-at-a-time HTTP server that answers every request
  /// with 200 and a fixed body, remembering the last request it saw.</summary>
  internal sealed class FakeHttpServer : IDisposable {
    private readonly TcpListener listener;
    private readonly string body;
    private volatile bool stopped;
    public int Port { get; }
    public string LastRequest { get; private set; } = "";

    private FakeHttpServer(string body) {
      this.body = body;
      listener = new TcpListener(IPAddress.Loopback, 0);
      listener.Start();
      Port = ((IPEndPoint) listener.LocalEndpoint).Port;
      Task.Run(Loop);
    }

    public static FakeHttpServer Start(string body) => new FakeHttpServer(body);

    private void Loop() {
      while (!stopped) {
        TcpClient c;
        try { c = listener.AcceptTcpClient(); } catch { return; }
        try { Serve(c); } catch { }
      }
    }

    internal void Serve(TcpClient c) {
      using (c) {
        var s = c.GetStream();
        LastRequest = ReadHead(s);
        var bytes = Encoding.ASCII.GetBytes(body);
        var head = "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: " + bytes.Length + "\r\nConnection: close\r\n\r\n";
        var hb = Encoding.ASCII.GetBytes(head);
        s.Write(hb, 0, hb.Length);
        s.Write(bytes, 0, bytes.Length);
        s.Flush();
      }
    }

    internal static string ReadHead(Stream s) {
      var sb = new StringBuilder();
      var one = new byte[1];
      while (s.Read(one, 0, 1) > 0) {
        sb.Append((char) one[0]);
        if (sb.Length >= 4 && sb.ToString(sb.Length - 4, 4) == "\r\n\r\n") break;
      }
      return sb.ToString();
    }

    public void Dispose() { stopped = true; try { listener.Stop(); } catch { } }
  }

  /// <summary>A minimal proxy that speaks just enough SOCKS4a / SOCKS5 / HTTP CONNECT
  /// to accept one tunnel, records what was asked, and relays bytes to the target.</summary>
  internal sealed class FakeProxy : IDisposable {
    internal enum Kind { Socks4a, Socks5, HttpConnect }

    private readonly TcpListener listener;
    private readonly Kind kind;
    private readonly string user, pass;
    private volatile bool stopped;
    public int Port { get; }
    public string RequestedHost { get; private set; }
    public int RequestedPort { get; private set; }
    public bool Authenticated { get; private set; }

    private FakeProxy(Kind kind, string user, string pass) {
      this.kind = kind; this.user = user; this.pass = pass;
      listener = new TcpListener(IPAddress.Loopback, 0);
      listener.Start();
      Port = ((IPEndPoint) listener.LocalEndpoint).Port;
      Task.Run(Loop);
    }

    public static FakeProxy Start(Kind kind, string user, string pass) => new FakeProxy(kind, user, pass);

    private void Loop() {
      while (!stopped) {
        TcpClient c;
        try { c = listener.AcceptTcpClient(); } catch { return; }
        Task.Run(() => { try { Handle(c); } catch { } });
      }
    }

    private void Handle(TcpClient c) {
      using (c) {
        var s = c.GetStream();
        switch (kind) {
          case Kind.Socks5: if (!HandleSocks5(s)) return; break;
          case Kind.Socks4a: if (!HandleSocks4a(s)) return; break;
          case Kind.HttpConnect: if (!HandleHttpConnect(s)) return; break;
        }
        // Relay to the target until either side closes.
        using var target = new TcpClient();
        target.Connect(IPAddress.Loopback, RequestedPort);
        var t = target.GetStream();
        var up = Task.Run(() => { try { s.CopyTo(t); } catch { } try { target.Client.Shutdown(SocketShutdown.Send); } catch { } });
        try { t.CopyTo(s); } catch { }
        try { c.Client.Shutdown(SocketShutdown.Send); } catch { }
        up.Wait(2000);
      }
    }

    private static byte[] Read(Stream s, int n) {
      var b = new byte[n]; var got = 0;
      while (got < n) { var r = s.Read(b, got, n - got); if (r <= 0) throw new IOException("eof"); got += r; }
      return b;
    }

    private bool HandleSocks5(Stream s) {
      var hdr = Read(s, 2);
      var methods = Read(s, hdr[1]);
      var wantAuth = !string.IsNullOrEmpty(user);
      if (wantAuth) {
        if (Array.IndexOf(methods, (byte) 2) < 0) { s.Write(new byte[] { 5, 0xFF }, 0, 2); return false; }
        s.Write(new byte[] { 5, 2 }, 0, 2);
        var v = Read(s, 2);
        var u = Encoding.UTF8.GetString(Read(s, v[1]));
        var pl = Read(s, 1)[0];
        var p = Encoding.UTF8.GetString(Read(s, pl));
        if (u != user || p != pass) { s.Write(new byte[] { 1, 1 }, 0, 2); return false; }
        s.Write(new byte[] { 1, 0 }, 0, 2);
        Authenticated = true;
      }
      else {
        s.Write(new byte[] { 5, 0 }, 0, 2);
      }
      var req = Read(s, 4);
      if (req[3] != 3) return false;
      var len = Read(s, 1)[0];
      RequestedHost = Encoding.ASCII.GetString(Read(s, len));
      var port = Read(s, 2);
      RequestedPort = (port[0] << 8) | port[1];
      s.Write(new byte[] { 5, 0, 0, 1, 127, 0, 0, 1, 0, 80 }, 0, 10);
      return true;
    }

    private bool HandleSocks4a(Stream s) {
      var head = Read(s, 8);
      RequestedPort = (head[2] << 8) | head[3];
      ReadCString(s); // user id
      RequestedHost = ReadCString(s);
      s.Write(new byte[] { 0, 0x5A, 0, 0, 0, 0, 0, 0 }, 0, 8);
      return true;
    }

    private bool HandleHttpConnect(Stream s) {
      var head = FakeHttpServer.ReadHead(s);
      var first = head.Split('\n')[0].Trim().Split(' ');
      if (first.Length < 2 || first[0] != "CONNECT") return false;
      var hp = first[1].Split(':');
      RequestedHost = hp[0];
      RequestedPort = int.Parse(hp[1]);
      var ok = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection established\r\n\r\n");
      s.Write(ok, 0, ok.Length);
      return true;
    }

    private static string ReadCString(Stream s) {
      var sb = new StringBuilder();
      while (true) { var b = Read(s, 1)[0]; if (b == 0) break; sb.Append((char) b); }
      return sb.ToString();
    }

    public void Dispose() { stopped = true; try { listener.Stop(); } catch { } }
  }
}
