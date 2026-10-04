using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Seedforger {

  /// <summary>
  /// Portable, JSON-backed settings: a <c>settings.json</c> next to the executable,
  /// no registry. Two groups: what the interface remembers about itself (window
  /// behaviour, language, believability toggles) and the last-used run values
  /// (client, speeds, proxy, stop rule) so the next launch starts where you left off.
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

    internal static readonly string FilePath = Path.Combine(AppContext.BaseDirectory, "settings.json");

    private static readonly object gate = new object();
    private static Settings current;

    /// <summary>The one shared instance every window and command reads and writes.</summary>
    internal static Settings Current {
      get { lock (gate) return current ??= Load(); }
    }

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

    /// <summary>Loads the settings from disk; defaults when the file is missing. A
    /// corrupt file is set aside as settings.json.bad instead of being overwritten.</summary>
    internal static Settings Load() {
      try {
        if (!File.Exists(FilePath)) return new Settings();
        var loaded = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), JsonOptions);
        return loaded ?? new Settings();
      }
      catch (Exception) {
        try { File.Copy(FilePath, FilePath + ".bad", overwrite: true); } catch (Exception) { /* best effort */ }
        return new Settings();
      }
    }

    /// <summary>Writes the current values (indented). Returns false if the write failed.</summary>
    internal bool Save() {
      try {
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
        return true;
      }
      catch (Exception) {
        return false; // a settings write must never take the app down
      }
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
