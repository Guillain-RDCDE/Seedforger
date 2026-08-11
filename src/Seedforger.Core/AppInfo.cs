using System.Reflection;

namespace Seedforger {

  /// <summary>
  /// Application metadata, previously provided by the sergiye.Common Updater.
  /// Kept local so the project has no external branding dependency.
  /// </summary>
  internal static class AppInfo {

    internal const string Name = "Seedforger";

    internal const string SiteUrl = "https://github.com/Guillain-RDCDE/Seedforger";

    internal static string Version {
      get {
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        return v == null ? string.Empty : $"{v.Major}.{v.Minor}.{v.Build}";
      }
    }

    internal static string Title => $"{Name} v{Version}";
  }
}
