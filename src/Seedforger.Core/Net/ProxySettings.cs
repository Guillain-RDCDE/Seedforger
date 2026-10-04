using System;

namespace Seedforger.Net {

  /// <summary>How outbound tracker connections leave the machine.</summary>
  internal enum ProxyType {
    None,
    HttpConnect,
    Socks4,
    Socks4a,
    Socks5,
  }

  /// <summary>
  /// An optional proxy for tracker traffic. Plain data: the engine and both GUIs
  /// pass it around, <see cref="ProxyConnector"/> is the only thing that speaks
  /// the protocols.
  /// </summary>
  internal sealed class ProxySettings {

    /// <summary>No proxy: connect directly.</summary>
    internal static readonly ProxySettings None = new ProxySettings();

    public ProxyType Type { get; set; } = ProxyType.None;
    public string Host { get; set; } = "";
    public int Port { get; set; }
    public string User { get; set; } = "";
    public string Password { get; set; } = "";

    /// <summary>True when a usable proxy is configured.</summary>
    public bool Enabled => Type != ProxyType.None && !string.IsNullOrWhiteSpace(Host) && Port > 0 && Port <= 65535;

    public bool HasCredentials => !string.IsNullOrEmpty(User);

    /// <summary>"SOCKS5 proxy.example:1080" or "direct" — for logs and status bars.</summary>
    public override string ToString() => Enabled ? $"{Type} {Host}:{Port}" : "direct";

    /// <summary>Parses the names used by settings files and the command line
    /// ("none", "http", "httpconnect", "https", "socks4", "socks4a", "socks5").</summary>
    internal static ProxyType ParseType(string name) {
      switch ((name ?? "").Trim().ToLowerInvariant()) {
        case "http": case "https": case "httpconnect": case "http connect": case "http-connect": return ProxyType.HttpConnect;
        case "socks4": return ProxyType.Socks4;
        case "socks4a": return ProxyType.Socks4a;
        case "socks5": return ProxyType.Socks5;
        default: return ProxyType.None;
      }
    }

    internal static ProxySettings From(ProxyType type, string host, int port, string user = "", string password = "") =>
      new ProxySettings { Type = type, Host = host ?? "", Port = port, User = user ?? "", Password = password ?? "" };

    internal ProxySettings Clone() => (ProxySettings) MemberwiseClone();

    /// <summary>Builds the proxy from the persisted settings fields.</summary>
    internal static ProxySettings FromSettings(Settings s) {
      if (s == null) return None;
      return From(ParseType(s.ProxyType), s.ProxyAddress, s.ProxyPort, s.ProxyUser, s.ProxyPass);
    }

    /// <summary>Writes the proxy back into the persisted settings fields.</summary>
    internal void SaveTo(Settings s) {
      if (s == null) return;
      s.ProxyType = Type.ToString();
      s.ProxyAddress = Host ?? "";
      s.ProxyPort = Port;
      s.ProxyUser = User ?? "";
      s.ProxyPass = Password ?? "";
    }

    public static bool operator ==(ProxySettings a, ProxySettings b) => Equals(a, b);
    public static bool operator !=(ProxySettings a, ProxySettings b) => !Equals(a, b);

    public override bool Equals(object obj) =>
      obj is ProxySettings o && o.Type == Type && o.Host == Host && o.Port == Port && o.User == User && o.Password == Password;

    public override int GetHashCode() => HashCode.Combine(Type, Host, Port, User, Password);
  }
}
