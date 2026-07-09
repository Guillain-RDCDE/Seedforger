using System.Text.Json.Serialization;

namespace Seedforger {

  /// <summary>
  /// Describes how a random identifier (peer_id tail or key) is generated.
  /// Type: "alphanumeric" | "numeric" | "random" | "hex".
  /// </summary>
  public class IdSpec {
    public string Type { get; set; }
    public int Length { get; set; }
    public bool UrlEncode { get; set; }
    public bool UpperCase { get; set; }
  }

  /// <summary>
  /// Data-driven definition of an emulated torrent client. Serializable with
  /// System.Text.Json so it can be overridden through an external clients.json.
  /// </summary>
  public class ClientProfile {
    public string Family { get; set; }
    public string Version { get; set; }
    public string HttpProtocol { get; set; } = "HTTP/1.1";
    public bool HashUpperCase { get; set; }
    public IdSpec Key { get; set; }
    public string PeerIdPrefix { get; set; } = "";
    public IdSpec PeerIdRandom { get; set; }
    public string Headers { get; set; }
    public string Query { get; set; }
    public int DefNumWant { get; set; } = 200;
    public bool Parse { get; set; }
    public string SearchString { get; set; } = "";
    public string ProcessName { get; set; } = "";
    public long StartOffset { get; set; } = 10000000;
    public long MaxOffset { get; set; } = 25000000;

    [JsonIgnore]
    public string FullName => $"{Family} {Version}";
  }
}
