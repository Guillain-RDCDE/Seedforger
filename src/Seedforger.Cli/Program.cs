using System;
using Seedforger.Cli;

namespace Seedforger.ConsoleHost {

  /// <summary>The cross-platform console executable: everything lives in
  /// <see cref="CliApp"/>, which the Windows GUI executable shares.</summary>
  internal static class Program {
    internal static int Main(string[] args) => CliApp.Run(args, Console.Out, Console.Error);
  }
}
