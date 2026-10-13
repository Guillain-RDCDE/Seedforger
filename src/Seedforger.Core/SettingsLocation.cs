using System;
using System.IO;

namespace Seedforger {

  /// <summary>The operating-system families whose per-user config folders differ.</summary>
  internal enum ConfigPlatform { Windows, MacOS, Unix }

  /// <summary>
  /// Where <c>settings.json</c> is read and written. A portable copy (an existing
  /// settings.json next to the executable, in a folder the user can write) keeps
  /// working exactly as before. Everything else uses the per-user config folder,
  /// because the executable's folder is often read-only: the Flatpak's <c>/app</c>,
  /// Program Files without admin rights, a macOS app bundle, a distribution package
  /// under <c>/usr</c>. A settings.json left next to a read-only executable is read
  /// once as the starting point (<see cref="MigrateFrom"/>) and then lives on in the
  /// user folder.
  /// </summary>
  internal sealed class SettingsLocation {

    internal const string FileName = "settings.json";

    /// <summary>The file every read and save uses.</summary>
    internal string FilePath { get; }

    /// <summary>An older settings.json to start from when <see cref="FilePath"/> does
    /// not exist yet; null when there is nothing to carry over.</summary>
    internal string? MigrateFrom { get; }

    /// <summary>True when the file sits next to the executable.</summary>
    internal bool Portable { get; }

    internal SettingsLocation(string filePath, string? migrateFrom, bool portable) {
      FilePath = filePath;
      MigrateFrom = migrateFrom;
      Portable = portable;
    }

    /// <summary>The location for this process: the executable's folder and the
    /// current user's config folder, probed on disk.</summary>
    internal static SettingsLocation ForCurrentProcess() =>
      Resolve(AppContext.BaseDirectory, UserConfigDirectory(), CanWriteTo);

    /// <summary>Chooses between the portable file and the per-user one.
    /// <paramref name="canWrite"/> answers whether a folder accepts new files.</summary>
    internal static SettingsLocation Resolve(string exeDirectory, string userConfigDirectory, Func<string, bool> canWrite) {
      var portableFile = Path.Combine(exeDirectory, FileName);
      var userFile = Path.Combine(userConfigDirectory, FileName);
      if (!File.Exists(portableFile)) return new SettingsLocation(userFile, null, portable: false);
      if (canWrite(exeDirectory) && !IsReadOnlyFile(portableFile)) return new SettingsLocation(portableFile, null, portable: true);
      return new SettingsLocation(userFile, File.Exists(userFile) ? null : portableFile, portable: false);
    }

    // ---- the other user-editable files (clients.json, the samples) ----

    /// <summary>The <paramref name="fileName"/> to read: the copy in the user folder
    /// first, then the one next to the executable; null when neither exists.</summary>
    internal static string? FindDataFile(string fileName, string exeDirectory, string userConfigDirectory) {
      foreach (var dir in new[] { userConfigDirectory, exeDirectory }) {
        var path = Path.Combine(dir, fileName);
        if (File.Exists(path)) return path;
      }
      return null;
    }

    /// <summary>Same lookup for this process.</summary>
    internal static string? FindDataFile(string fileName) =>
      FindDataFile(fileName, AppContext.BaseDirectory, UserConfigDirectory());

    /// <summary>Where a new sample file goes: next to the executable when that folder
    /// accepts files (as before), the user folder otherwise.</summary>
    internal static string SampleDirectory(string exeDirectory, string userConfigDirectory, Func<string, bool> canWrite) =>
      canWrite(exeDirectory) ? exeDirectory : userConfigDirectory;

    /// <summary>Same choice for this process.</summary>
    internal static string SampleDirectory() =>
      SampleDirectory(AppContext.BaseDirectory, UserConfigDirectory(), CanWriteTo);

    /// <summary>The per-user config folder of this process's platform.</summary>
    internal static string UserConfigDirectory() {
      var platform = OperatingSystem.IsWindows() ? ConfigPlatform.Windows
                   : OperatingSystem.IsMacOS() ? ConfigPlatform.MacOS
                   : ConfigPlatform.Unix;
      return UserConfigDirectory(platform,
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        Environment.GetEnvironmentVariable("XDG_CONFIG_HOME"));
    }

    /// <summary>%APPDATA%\Seedforger on Windows; ~/Library/Application Support/Seedforger
    /// on macOS (.NET maps ApplicationData to ~/.config there, which is not where Mac
    /// apps keep their settings); $XDG_CONFIG_HOME/seedforger, else ~/.config/seedforger,
    /// elsewhere. Inside a Flatpak, XDG_CONFIG_HOME is the sandbox's own config folder.</summary>
    internal static string UserConfigDirectory(ConfigPlatform platform, string home, string? appData, string? xdgConfigHome) {
      switch (platform) {
        case ConfigPlatform.Windows:
          return Path.Combine(string.IsNullOrEmpty(appData) ? Path.Combine(home, "AppData", "Roaming") : appData, AppInfo.Name);
        case ConfigPlatform.MacOS:
          return Path.Combine(home, "Library", "Application Support", AppInfo.Name);
        default:
          // The XDG spec ignores a relative XDG_CONFIG_HOME.
          var config = !string.IsNullOrEmpty(xdgConfigHome) && Path.IsPathRooted(xdgConfigHome)
            ? xdgConfigHome
            : Path.Combine(home, ".config");
          return Path.Combine(config, AppInfo.Name.ToLowerInvariant());
      }
    }

    /// <summary>True when a new file can be created in <paramref name="directory"/>.
    /// Tried for real: permission bits, ACLs, read-only mounts and sandboxes all
    /// answer the same way to an actual create.</summary>
    internal static bool CanWriteTo(string directory) {
      try {
        var probe = Path.Combine(directory, ".seedforger-write-test-" + Guid.NewGuid().ToString("N"));
        using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) { }
        return true;
      }
      catch (Exception) {
        return false;
      }
    }

    private static bool IsReadOnlyFile(string path) {
      try { return (File.GetAttributes(path) & FileAttributes.ReadOnly) != 0; }
      catch (Exception) { return true; }
    }
  }
}
