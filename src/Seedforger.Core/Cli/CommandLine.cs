using System;
using System.Collections.Generic;
using System.Globalization;

namespace Seedforger.Cli {

  /// <summary>
  /// A small, predictable command-line reader: flags (<c>--daemon</c>) and
  /// "--key value" pairs, case-insensitive, with aliases. A value is never
  /// another option, so <c>-t --quiet</c> reports a missing torrent rather than
  /// a torrent called "--quiet". Unknown options are reported, not ignored.
  /// </summary>
  internal sealed class CommandLine {

    private readonly string[] args;

    internal CommandLine(string[] args) { this.args = args ?? Array.Empty<string>(); }

    internal bool IsEmpty => args.Length == 0;

    internal bool Has(params string[] names) {
      foreach (var a in args)
        foreach (var name in names)
          if (string.Equals(a, name, StringComparison.OrdinalIgnoreCase)) return true;
      return false;
    }

    /// <summary>The token after the option, or null when absent or when the next
    /// token is itself an option.</summary>
    internal string Value(params string[] names) {
      for (var i = 0; i < args.Length - 1; i++)
        foreach (var name in names)
          if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            return IsOption(args[i + 1]) ? null : args[i + 1];
      return null;
    }

    internal int Int(int fallback, params string[] names) =>
      int.TryParse(Value(names), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : fallback;

    internal double Double(double fallback, params string[] names) =>
      double.TryParse((Value(names) ?? "").Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : fallback;

    /// <summary>on/off, true/false, yes/no, 1/0; anything else keeps the fallback.</summary>
    internal bool Bool(bool fallback, params string[] names) {
      var v = Value(names);
      if (v == null) return fallback;
      switch (v.Trim().ToLowerInvariant()) {
        case "on": case "true": case "yes": case "1": return true;
        case "off": case "false": case "no": case "0": return false;
        default: return fallback;
      }
    }

    /// <summary>Options present on the line that are not in <paramref name="known"/>.</summary>
    internal IReadOnlyList<string> Unknown(IEnumerable<string> known) {
      var set = new HashSet<string>(known, StringComparer.OrdinalIgnoreCase);
      var result = new List<string>();
      foreach (var a in args)
        if (IsOption(a) && !set.Contains(a) && !result.Contains(a)) result.Add(a);
      return result;
    }

    /// <summary>True for "--name", "-n", "/?" style tokens (a lone "-" is a value).</summary>
    internal static bool IsOption(string token) =>
      !string.IsNullOrEmpty(token) && token.Length > 1 && (token[0] == '-' || token == "/?") && !IsNumber(token);

    private static bool IsNumber(string token) =>
      double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
  }
}
