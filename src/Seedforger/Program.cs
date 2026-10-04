using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Seedforger.Cli;

namespace Seedforger {

  /// <summary>
  /// The Windows executable. Started with command-line options it behaves exactly
  /// like the console build (the shared <see cref="CliApp"/>); otherwise it opens
  /// the window.
  /// </summary>
  internal static class Program {

    // Held for the process lifetime to enforce a single running instance.
    private static Mutex singleInstanceMutex;

    [STAThread]
    internal static void Main() {
      Encodings.Register();

      var args = SkipExe(Environment.GetCommandLineArgs());
      if (CliApp.IsCliInvocation(args)) {
        EnsureConsole();
        Environment.ExitCode = CliApp.Run(args, Console.Out, Console.Error);
        return;
      }

      // Drop templates so users can discover the override / campaign formats.
      TorrentClientFactory.ExportSampleIfMissing();
      Campaign.ExportSampleIfMissing();

      singleInstanceMutex = new Mutex(true, @"Global\Seedforger.SingleInstance", out var createdNew);
      if (!createdNew) {
        MessageBox.Show($"{AppInfo.Name} is already running.", AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
        return;
      }

      Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
      Application.EnableVisualStyles();
      Application.SetCompatibleTextRenderingDefault(false);
      Application.Run(new UI.MainForm());

      GC.KeepAlive(singleInstanceMutex);
    }

    /// <summary>Environment.GetCommandLineArgs()[0] is the exe path; drop it.</summary>
    private static string[] SkipExe(string[] argv) {
      if (argv == null || argv.Length <= 1) return Array.Empty<string>();
      var rest = new string[argv.Length - 1];
      Array.Copy(argv, 1, rest, 0, rest.Length);
      return rest;
    }

    // A WinExe has no console of its own: attach to the parent's (a terminal) or
    // allocate one, so the CLI output lands somewhere visible.
    [DllImport("kernel32.dll")] private static extern bool AttachConsole(int pid);
    [DllImport("kernel32.dll")] private static extern bool AllocConsole();
    private const int AttachParent = -1;

    private static void EnsureConsole() {
      try {
        if (!AttachConsole(AttachParent)) AllocConsole();
        // Re-open the standard streams on the freshly attached console.
        var stdout = new System.IO.StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
        var stderr = new System.IO.StreamWriter(Console.OpenStandardError()) { AutoFlush = true };
        Console.SetOut(stdout);
        Console.SetError(stderr);
        Console.WriteLine();
      }
      catch (Exception) { /* no console available: output is simply lost */ }
    }
  }
}
