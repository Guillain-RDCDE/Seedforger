using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Seedforger {

  /// <summary>
  /// Turns a client profile into a fresh <see cref="TorrentClient"/> fingerprint
  /// (random key and peer_id tail each time), and answers the questions the
  /// front-ends ask: which families and versions exist, what is current, what is
  /// the default. Profiles are the built-in list merged with an optional
  /// <c>clients.json</c> next to the executable.
  /// </summary>
  internal static class TorrentClientFactory {

    /// <summary>The client a fresh install impersonates.</summary>
    internal const string DefaultFamily = "qBittorrent";
    internal const string DefaultClientName = "qBittorrent 5.2.4";

    /// <summary>What an unknown name falls back to (a very common legacy client).</summary>
    internal const string FallbackClientName = "uTorrent 3.3.2";

    /// <summary>Current, tracker-friendly clients used by the "randomize client on
    /// start" rotation (never an ancient, easily-flagged version).</summary>
    internal static readonly string[] ModernClients = {
      "qBittorrent 5.2.4",
      "qBittorrent 5.2.3",
      "qBittorrent 5.1.4",
      "qBittorrent 5.0.5",
      "Transmission 4.1.3",
      "Transmission 4.0.6",
      "Deluge 2.2.0",
      "libtorrent 2.1.2",
      "libtorrent 2.0.15",
      "BiglyBT 4.1.0.0",
    };

    private static readonly Lazy<List<ClientProfile>> profiles =
      new Lazy<List<ClientProfile>>(LoadProfiles, true);

    private static readonly RandomStringGenerator stringGenerator = new RandomStringGenerator();
    private static readonly object generatorGate = new object();

    private static List<ClientProfile> Profiles => profiles.Value;

    /// <summary>Built-in defaults merged with an optional clients.json (entries
    /// override or add by FullName). Invalid JSON is ignored rather than crashing.</summary>
    private static List<ClientProfile> LoadProfiles() {
      var list = new List<ClientProfile>(DefaultClientProfiles.All);
      try {
        var path = Path.Combine(AppContext.BaseDirectory, "clients.json");
        if (!File.Exists(path)) return list;
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var overrides = JsonSerializer.Deserialize<List<ClientProfile>>(File.ReadAllText(path), options);
        if (overrides == null) return list;
        foreach (var ov in overrides) {
          if (ov == null || string.IsNullOrWhiteSpace(ov.Family) || string.IsNullOrWhiteSpace(ov.Version)) continue;
          var index = list.FindIndex(p => p.FullName == ov.FullName);
          if (index >= 0) list[index] = ov; else list.Add(ov);
        }
      }
      catch (Exception) {
        // Malformed clients.json: the built-in defaults still work.
      }
      return list;
    }

    /// <summary>
    /// Writes a <c>clients.sample.json</c> next to the executable (once) so users can
    /// see the exact override format. It is NOT loaded automatically: copy it to
    /// <c>clients.json</c> and edit to add or override fingerprints without recompiling.
    /// </summary>
    public static void ExportSampleIfMissing() {
      try {
        var path = Path.Combine(AppContext.BaseDirectory, "clients.sample.json");
        if (File.Exists(path)) return;
        var options = new JsonSerializerOptions {
          WriteIndented = true,
          DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };
        File.WriteAllText(path, JsonSerializer.Serialize(DefaultClientProfiles.All, options));
      }
      catch (Exception) {
        // Read-only directory or similar: a sample file is a convenience, not critical.
      }
    }

    // ---- picking a client ----

    /// <summary>A fresh fingerprint for "Family Version"; unknown names fall back
    /// to <see cref="FallbackClientName"/>.</summary>
    public static TorrentClient GetClient(string name) {
      var profile = Profiles.FirstOrDefault(p => p.FullName == name)
                    ?? Profiles.FirstOrDefault(p => p.FullName == FallbackClientName)
                    ?? Profiles[0];
      return Build(profile);
    }

    /// <summary>"qBittorrent" + "5.2.4" → the client; an empty version means the
    /// newest known version of that family.</summary>
    public static TorrentClient Resolve(string family, string version) {
      family = string.IsNullOrWhiteSpace(family) ? DefaultFamily : family.Trim();
      if (string.IsNullOrWhiteSpace(version)) version = NewestVersion(family);
      return GetClient((family + " " + version).Trim());
    }

    /// <summary>The newest known version of a family, or "" if the family is unknown.</summary>
    public static string NewestVersion(string family) {
      var versions = GetVersions(family);
      return versions.Count > 0 ? versions[0] : "";
    }

    public static TorrentClient Default() => GetClient(DefaultClientName);

    /// <summary>A random current client, for rotation between runs.</summary>
    public static TorrentClient PickRandomModern(Random rand) =>
      GetClient(ModernClients[(rand ?? Random.Shared).Next(ModernClients.Length)]);

    // ---- UI helpers ----

    public static IReadOnlyList<string> GetFamilies() {
      var result = new List<string>();
      foreach (var p in Profiles) if (!result.Contains(p.Family)) result.Add(p.Family);
      return result;
    }

    public static IReadOnlyList<string> GetVersions(string family) =>
      Profiles.Where(p => p.Family == family).Select(p => p.Version).ToList();

    public static int GetDefaultNumWant(string family) =>
      Profiles.FirstOrDefault(p => p.Family == family)?.DefNumWant ?? 200;

    // ---- building a fingerprint ----

    private static TorrentClient Build(ClientProfile p) => new TorrentClient(p.FullName) {
      Name = p.FullName,
      HttpProtocol = p.HttpProtocol,
      HashUpperCase = p.HashUpperCase,
      Key = GenerateIdString(p.Key),
      PeerID = p.PeerIdPrefix + GeneratePeerIdTail(p),
      Headers = p.Headers,
      Query = p.Query,
      DefNumWant = p.DefNumWant,
      Parse = p.Parse,
      SearchString = p.SearchString,
      ProcessName = p.ProcessName,
      StartOffset = p.StartOffset,
      MaxOffset = p.MaxOffset,
    };

    private static string GeneratePeerIdTail(ClientProfile p) {
      if (string.Equals(p.PeerIdChecksum, "transmission", StringComparison.OrdinalIgnoreCase))
        return PeerId.TransmissionTail(Random.Shared);
      return p.PeerIdRandom != null ? GenerateIdString(p.PeerIdRandom) : string.Empty;
    }

    /// <summary>Generates an identifier per its spec: "alphanumeric", "numeric",
    /// "hex" or "random" (any byte), optionally percent-encoded / upper-cased.</summary>
    private static string GenerateIdString(IdSpec spec) {
      if (spec == null) return string.Empty;
      string raw;
      lock (generatorGate) {
        switch (spec.Type) {
          case "numeric": raw = stringGenerator.Generate(spec.Length, "0123456789".ToCharArray()); break;
          case "hex": raw = stringGenerator.Generate(spec.Length, "0123456789ABCDEF".ToCharArray()); break;
          case "random": raw = stringGenerator.Generate(spec.Length, true); break;
          default: raw = stringGenerator.Generate(spec.Length); break; // "alphanumeric"
        }
      }
      if (spec.UrlEncode) return Announce.PercentEncode(raw, spec.UpperCase);
      return spec.UpperCase ? raw.ToUpperInvariant() : raw;
    }
  }
}
