using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;

namespace Seedforger {

  /// <summary>
  /// Minimal TLS transport for HTTPS trackers. Sends a hand-built raw HTTP request
  /// (preserving header order and User-Agent for wire fidelity) over an SslStream
  /// and reads the full response. Kept UI-free and static so it can be tested.
  /// </summary>
  internal static class HttpsTransport {

    /// <summary>
    /// Opens TLS to host:port, writes <paramref name="rawRequest"/> and returns the
    /// raw response bytes (headers + body). The connection is expected to close at
    /// the end of the response (clients send "Connection: close").
    /// </summary>
    internal static byte[] Fetch(string host, int port, string rawRequest, Encoding enc,
      RemoteCertificateValidationCallback certValidation = null, int timeoutMs = 200000) {

      using (var tcp = new TcpClient()) {
        tcp.SendTimeout = timeoutMs;
        tcp.ReceiveTimeout = timeoutMs;
        tcp.Connect(host, port);

        // Trackers occasionally use self-signed certs; be lenient like a torrent client.
        using (var ssl = new SslStream(tcp.GetStream(), false,
                 certValidation ?? ((sender, cert, chain, errors) => true))) {
          ssl.AuthenticateAsClient(host);

          var reqBytes = enc.GetBytes(rawRequest);
          ssl.Write(reqBytes, 0, reqBytes.Length);
          ssl.Flush();

          var data = new byte[32 * 1024];
          using (var ms = new MemoryStream()) {
            int dataLen;
            while ((dataLen = ssl.Read(data, 0, data.Length)) > 0) {
              ms.Write(data, 0, dataLen);
            }

            return ms.ToArray();
          }
        }
      }
    }
  }
}
