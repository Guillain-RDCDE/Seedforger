using System.Text;

namespace Seedforger {

  /// <summary>
  /// The legacy code page the BitTorrent wire and tracker layers use. .NET Core
  /// does not ship it by default, so every entry point calls <see cref="Register"/>
  /// once; the property then hands out the encoding.
  /// </summary>
  internal static class Encodings {

    private static readonly object gate = new object();
    private static Encoding win1252;

    /// <summary>Makes the code-page encodings available. Safe to call repeatedly.</summary>
    internal static void Register() {
      lock (gate) {
        if (win1252 != null) return;
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        win1252 = Encoding.GetEncoding(1252);
      }
    }

    /// <summary>Windows-1252: what real clients use for raw announce bytes.</summary>
    internal static Encoding Win1252 {
      get { Register(); return win1252; }
    }
  }
}
