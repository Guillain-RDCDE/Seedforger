using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Seedforger {
  public static class TorrentClientFactory {
    private static readonly RandomStringGenerator stringGenerator = new RandomStringGenerator();

    private const string FallbackClientName = "uTorrent 3.3.2";

    private static List<ClientProfile> profiles;

    /// <summary>
    /// Lazily built list of profiles: the built-in defaults, then merged with an
    /// optional clients.json sitting next to the executable (entries there override
    /// or add by FullName). Invalid JSON is ignored rather than crashing.
    /// </summary>
    private static List<ClientProfile> Profiles {
      get {
        if (profiles != null) {
          return profiles;
        }

        var list = new List<ClientProfile>(DefaultClientProfiles.All);
        MergeExternalOverrides(list);
        profiles = list;
        return profiles;
      }
    }

    private static void MergeExternalOverrides(List<ClientProfile> list) {
      try {
        var path = Path.Combine(AppContext.BaseDirectory, "clients.json");
        if (!File.Exists(path)) {
          return;
        }

        var json = File.ReadAllText(path);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var overrides = JsonSerializer.Deserialize<List<ClientProfile>>(json, options);
        if (overrides == null) {
          return;
        }

        foreach (var ov in overrides) {
          if (ov == null || string.IsNullOrWhiteSpace(ov.Family) || string.IsNullOrWhiteSpace(ov.Version)) {
            continue;
          }

          var index = list.FindIndex(p => p.FullName == ov.FullName);
          if (index >= 0) {
            list[index] = ov;
          }
          else {
            list.Add(ov);
          }
        }
      }
      catch {
        // Malformed clients.json: fall back to the built-in defaults.
      }
    }

    public static TorrentClient GetClient(string name) {
      var profile = Profiles.FirstOrDefault(p => p.FullName == name)
                    ?? Profiles.FirstOrDefault(p => p.FullName == FallbackClientName)
                    ?? Profiles[0];
      return Build(profile);
    }

    private static TorrentClient Build(ClientProfile p) {
      var client = new TorrentClient(p.FullName) {
        Name = p.FullName,
        HttpProtocol = p.HttpProtocol,
        HashUpperCase = p.HashUpperCase,
        Key = GenerateIdString(p.Key),
        PeerID = p.PeerIdPrefix + (p.PeerIdRandom != null ? GenerateIdString(p.PeerIdRandom) : ""),
        Headers = p.Headers,
        Query = p.Query,
        DefNumWant = p.DefNumWant,
        Parse = p.Parse,
        SearchString = p.SearchString,
        ProcessName = p.ProcessName,
        StartOffset = p.StartOffset,
        MaxOffset = p.MaxOffset,
      };
      return client;
    }

    #region UI helpers

    public static IReadOnlyList<string> GetFamilies() {
      var result = new List<string>();
      foreach (var p in Profiles) {
        if (!result.Contains(p.Family)) {
          result.Add(p.Family);
        }
      }

      return result;
    }

    public static IReadOnlyList<string> GetVersions(string family) {
      var result = new List<string>();
      foreach (var p in Profiles) {
        if (p.Family == family) {
          result.Add(p.Version);
        }
      }

      return result;
    }

    public static int GetDefaultNumWant(string family) {
      var first = Profiles.FirstOrDefault(p => p.Family == family);
      return first?.DefNumWant ?? 200;
    }

    #endregion

    private static string GenerateIdString(IdSpec spec) {
      if (spec == null) {
        return string.Empty;
      }

      return GenerateIdString(spec.Type, spec.Length, spec.UrlEncode, spec.UpperCase);
    }

    private static string GenerateIdString(string keyType, int keyLength, bool urlencoding, bool upperCase = false) {
      string text1;
      var text2 = keyType;
      if (text2 != null) {
        if (text2 == "alphanumeric") {
          text1 = stringGenerator.Generate(keyLength);
          goto Label_00A2;
        }

        if (text2 == "numeric") {
          text1 = stringGenerator.Generate(keyLength, "0123456789".ToCharArray());
          goto Label_00A2;
        }

        if (text2 == "random") {
          text1 = stringGenerator.Generate(keyLength, true);
          goto Label_00A2;
        }

        if (text2 == "hex") {
          text1 = stringGenerator.Generate(keyLength, "0123456789ABCDEF".ToCharArray());
          goto Label_00A2;
        }
      }

      text1 = stringGenerator.Generate(keyLength);
      Label_00A2:
      if (urlencoding) {
        return stringGenerator.Generate(text1, upperCase);
      }

      if (upperCase) {
        text1 = text1.ToUpper();
      }

      return text1;
    }
  }
}
