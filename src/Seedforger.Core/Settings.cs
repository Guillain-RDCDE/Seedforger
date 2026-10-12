using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Seedforger.UI;

namespace Seedforger {

  /// <summary>
  /// JSON-backed settings, no registry: a <c>settings.json</c> next to the executable
  /// for a portable copy, in the per-user config folder otherwise (see
  /// <see cref="SettingsLocation"/>). Two groups: what the interface remembers about
  /// itself (window behaviour, language, believability toggles) and the last-used run
  /// values (client, speeds, proxy, stop rule) so the next launch starts where you left off.
  ///
  /// JSON property names are kept as they always were (misspellings included),
  /// so files written by earlier versions still load; numbers that used to be
  /// stored as strings are read leniently.
  /// </summary>
  internal sealed class Settings {

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions {
      WriteIndented = true,
      PropertyNameCaseInsensitive = true,
      NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private static readonly object gate = new object();
    private static SettingsLocation? location;
    private static Settings? current;
    private static Action<string>? log;
    private static readonly List<string> pendingLog = new List<string>();
    private static string? lastSaveError;

    /// <summary>Where this process keeps its settings, resolved once.</summary>
    internal static SettingsLocation Location {
      get { lock (gate) return location ??= SettingsLocation.ForCurrentProcess(); }
    }

    internal static string FilePath => Location.FilePath;

    /// <summary>The one shared instance every window and command reads and writes.</summary>
    internal static Settings Current {
      get { lock (gate) return current ??= Load(Location); }
    }

    /// <summary>Where settings problems are reported (a failed save, a corrupt file, a
    /// carried-over file). The front-ends point it at their log; messages raised before
    /// that are kept and delivered when it is set.</summary>
    internal static Action<string>? Log {
      get { lock (gate) return log; }
      set {
        string[] queued;
        lock (gate) {
          log = value;
          queued = value == null ? Array.Empty<string>() : pendingLog.ToArray();
          if (value != null) pendingLog.Clear();
        }
        foreach (var line in queued) value?.Invoke(line);
      }
    }

    /// <summary>The file this instance saves to; null means the process-wide location.</summary>
    private string? savePath;

    // ---- interface ----

    [JsonPropertyName("BallonTip")]
    public bool BalloonTip { get; set; }
    /// <summary>The minimize button hides the window into the notification area.</summary>
    public bool MinimizeToTray { get; set; } = true;
    /// <summary>The close button minimizes instead of quitting (kept under its
    /// original name so existing files still apply).</summary>
    public bool CloseToTray { get; set; } = true;
    public string Language { get; set; } = "en";

    // ---- believability (mirrored into StealthOptions.Shared at launch) ----

    public bool RealisticSpeed { get; set; } = true;
    public bool SwarmAware { get; set; } = true;
    public bool RandomizeClientOnStart { get; set; }
    public bool ActiveHoursEnabled { get; set; }
    public int ActiveHoursStart { get; set; } = 8;
    public int ActiveHoursEnd { get; set; } = 24;
    /// <summary>Total upstream budget shared by every run, kB/s (0 = off).</summary>
    public int GlobalUpstreamKBps { get; set; }

    // ---- last-used run values ----

    public string Client { get; set; } = TorrentClientFactory.DefaultFamily;
    public string ClientVersion { get; set; } = "5.2.4";
    [JsonConverter(typeof(LenientIntConverter))]
    public int UploadRate { get; set; } = 1024;
    [JsonConverter(typeof(LenientIntConverter))]
    public int DownloadRate { get; set; } = 30;
    /// <summary>Base announce interval in seconds (0 = the tracker's).</summary>
    [JsonConverter(typeof(LenientIntConverter))]
    public int Interval { get; set; }
    public string StopWhen { get; set; } = "Never";
    [JsonConverter(typeof(LenientDoubleConverter))]
    public double StopAfter { get; set; }

    public string ProxyType { get; set; } = "None";
    [JsonPropertyName("ProxyAdress")]
    public string ProxyAddress { get; set; } = "";
    public string ProxyUser { get; set; } = "";
    public string ProxyPass { get; set; } = "";
    [JsonConverter(typeof(LenientIntConverter))]
    public int ProxyPort { get; set; }

    /// <summary>Fingerprint overrides from the Advanced dialog; empty/0 = the client's own.</summary>
    public string CustomKey { get; set; } = "";
    public string CustomPeerID { get; set; } = "";
    [JsonConverter(typeof(LenientIntConverter))]
    public int CustomPeers { get; set; }
    [JsonConverter(typeof(LenientIntConverter))]
    public int CustomPort { get; set; }

    // ---- persistence ----

    /// <summary>Loads the settings of <paramref name="from"/>; defaults when there is no
    /// file (an empty one counts as none, so creating an empty settings.json next to
    /// the executable is how a copy is made portable). A corrupt file is set aside as
    /// settings.json.bad instead of being overwritten. When only the older file of
    /// <see cref="SettingsLocation.MigrateFrom"/> exists, it is read once and saved
    /// to the new location.</summary>
    internal static Settings Load(SettingsLocation from) {
      var loaded = Read(from.FilePath);
      if (loaded == null && from.MigrateFrom != null) {
        loaded = Read(from.MigrateFrom);
        if (loaded != null) {
          loaded.savePath = from.FilePath;
          if (loaded.Save())
            Report(string.Format(UiStrings.Get("log.settings_moved"), from.MigrateFrom, from.FilePath));
          return loaded;
        }
      }
      loaded ??= new Settings();
      loaded.savePath = from.FilePath;
      return loaded;
    }

    private static Settings? Read(string path) {
      try {
        if (!File.Exists(path)) return null;
        var text = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(text)) return null;
        return JsonSerializer.Deserialize<Settings>(text, JsonOptions);
      }
      catch (Exception ex) {
        var kept = path + ".bad";
        try { File.Copy(path, kept, overwrite: true); } catch (Exception) { kept = path; }
        Report(string.Format(UiStrings.Get("log.settings_bad"), path, ex.Message, kept));
        return null;
      }
    }

    /// <summary>Writes the current values (indented), creating the folder if needed.
    /// Returns false if the write failed; the failure is reported through
    /// <see cref="Log"/> (once until a save succeeds again, not on every change).</summary>
    internal bool Save() {
      var path = savePath ?? FilePath;
      try {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
        lock (gate) lastSaveError = null;
        return true;
      }
      catch (Exception ex) {
        // A settings write must never take the app down, but it must not vanish either.
        var message = string.Format(UiStrings.Get("log.settings_save_failed"), path, ex.Message);
        bool repeat;
        lock (gate) { repeat = message == lastSaveError; lastSaveError = message; }
        if (!repeat) Report(message);
        return false;
      }
    }

    private static void Report(string message) {
      Action<string>? sink;
      lock (gate) {
        sink = log;
        if (sink == null) {
          if (pendingLog.Count < 20) pendingLog.Add(message);
          return;
        }
      }
      sink(message);
    }

    /// <summary>Change-and-save in one call: <c>Settings.Current.Update(s => s.Language = "fr")</c>.</summary>
    internal bool Update(Action<Settings> change) {
      change?.Invoke(this);
      return Save();
    }

    /// <summary>Reads an int that older files stored as a string ("10240", "").</summary>
    internal sealed class LenientIntConverter : JsonConverter<int> {
      public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if (reader.TokenType == JsonTokenType.Number) return reader.TryGetInt32(out var n) ? n : (int) reader.GetDouble();
        if (reader.TokenType == JsonTokenType.String)
          return int.TryParse(reader.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s) ? s : 0;
        return 0;
      }
      public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options) => writer.WriteNumberValue(value);
    }

    internal sealed class LenientDoubleConverter : JsonConverter<double> {
      public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if (reader.TokenType == JsonTokenType.Number) return reader.GetDouble();
        if (reader.TokenType == JsonTokenType.String)
          return double.TryParse((reader.GetString() ?? "").Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;
        return 0;
      }
      public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options) => writer.WriteNumberValue(value);
    }
  }
}
