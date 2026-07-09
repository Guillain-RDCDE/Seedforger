using System;
using System.IO;
using System.Text.Json;

namespace Seedforger {

  /// <summary>
  /// Portable, JSON-backed settings store. Replaces the former
  /// <c>HKCU\Software\Seedforger</c> registry storage with a
  /// <c>settings.json</c> file sitting next to the executable, so the app is
  /// fully portable and leaves no trace in the registry.
  ///
  /// Every persisted value keeps the exact same default it had in the legacy
  /// registry code so functional behaviour is unchanged. Booleans that were
  /// stored as DWord (via <c>BtoI</c>/<c>ItoB</c>) are plain <see cref="bool"/>
  /// here; everything else stays <see cref="string"/>.
  /// </summary>
  internal sealed class Settings {

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions {
      WriteIndented = true,
      PropertyNameCaseInsensitive = true,
    };

    private static readonly string FilePath =
      Path.Combine(AppContext.BaseDirectory, "settings.json");

    private static Settings current;

    /// <summary>
    /// True when no settings file existed at first <see cref="Load"/>. Mirrors
    /// the legacy "Version" == "none" marker that triggered a reset to default
    /// values on first launch.
    /// </summary>
    internal static bool IsFirstRun { get; private set; }

    /// <summary>Shared instance so MainForm (app-level) and every RM tab
    /// (per-tab) read/write the same file/object.</summary>
    internal static Settings Current => current ??= Load();

    #region App-level options

    public bool BallonTip { get; set; } = false;
    public bool MinimizeToTray { get; set; } = true;
    public bool CloseToTray { get; set; } = true;
    public bool RealisticSpeed { get; set; } = true;

    #endregion

    #region Per-tab "last used" values

    public bool NewValues { get; set; } = true;
    public string Client { get; set; } = "qBittorrent";
    public string ClientVersion { get; set; } = "5.2.3";
    public string UploadRate { get; set; } = "10240";
    public string DownloadRate { get; set; } = "30";
    public string Interval { get; set; } = "300";
    public string FileSize { get; set; } = "0";
    public string Directory { get; set; } = "";
    public bool TCPlistener { get; set; } = true;
    public bool ScrapeInfo { get; set; } = true;

    public bool GetRandUp { get; set; } = true;
    public bool GetRandDown { get; set; } = true;
    public string MinRandUp { get; set; } = "1";
    public string MaxRandUp { get; set; } = "10";
    public string MinRandDown { get; set; } = "1";
    public string MaxRandDown { get; set; } = "10";

    public string CustomKey { get; set; } = "";
    public string CustomPeerID { get; set; } = "";
    public string CustomPeers { get; set; } = "";
    public string CustomPort { get; set; } = "";

    public string StopWhen { get; set; } = "Never";
    public string StopAfter { get; set; } = "0";

    public string ProxyType { get; set; } = "None";
    public string ProxyAdress { get; set; } = "";
    public string ProxyUser { get; set; } = "";
    public string ProxyPass { get; set; } = "";
    public string ProxyPort { get; set; } = "";

    public bool GetRandUpNext { get; set; } = false;
    public bool GetRandDownNext { get; set; } = false;
    public string MinRandUpNext { get; set; } = "50";
    public string MaxRandUpNext { get; set; } = "100";
    public string MinRandDownNext { get; set; } = "10";
    public string MaxRandDownNext { get; set; } = "50";

    public bool IgnoreFailureReason { get; set; } = false;

    #endregion

    /// <summary>Loads the settings from disk, falling back to defaults when the
    /// file is missing or corrupt. Never throws.</summary>
    internal static Settings Load() {
      try {
        if (!File.Exists(FilePath)) {
          IsFirstRun = true;
          return new Settings();
        }

        var json = File.ReadAllText(FilePath);
        var loaded = JsonSerializer.Deserialize<Settings>(json, JsonOptions);
        return loaded ?? new Settings();
      }
      catch {
        // Corrupt or unreadable file: fall back to defaults, never crash.
        return new Settings();
      }
    }

    /// <summary>Serialises the current values (indented) to disk. Never throws.</summary>
    internal void Save() {
      try {
        var json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(FilePath, json);
      }
      catch {
        // Best effort: never let a settings write take down the app.
      }
    }
  }
}
