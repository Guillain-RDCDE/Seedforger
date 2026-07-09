using Seedforger.BytesRoads;

namespace Seedforger {
  internal struct ProxyInfo {
    public ProxyType ProxyType;
    public string ProxyServer;
    public int ProxyPort;
    public byte[] ProxyUser;
    public byte[] ProxyPassword;
  }
}