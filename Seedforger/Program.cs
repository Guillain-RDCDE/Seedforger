using System;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Seedforger {
  internal static class Program {

    // Held for the process lifetime to enforce a single running instance.
    private static Mutex singleInstanceMutex;

    [STAThread]
    internal static void Main() {

      // Legacy code pages (e.g. Windows-1252) used by the BEncode/tracker layer
      // are not registered by default on .NET Core/8.
      Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

      // Command-line / headless mode: no window, no single-instance lock — an
      // automation can drive the engine directly (Seedforger.exe --help).
      var cliArgs = SkipExe(Environment.GetCommandLineArgs());
      if (Cli.IsCliInvocation(cliArgs)) {
        Environment.ExitCode = Cli.Run(cliArgs);
        return;
      }

      // Drop templates so users can discover the override / campaign formats.
      TorrentClientFactory.ExportSampleIfMissing();
      Campaign.ExportSampleIfMissing();

      singleInstanceMutex = new Mutex(true, @"Global\Seedforger.SingleInstance", out var createdNew);
      if (!createdNew) {
        MessageBox.Show($"{AppInfo.Name} is already running.", AppInfo.Name,
          MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
        return;
      }

      // The legacy WinForms layout was designed for system-DPI scaling (the old
      // manifest used dpiAware=true). PerMonitorV2 mis-scales the hand-placed
      // controls on high-DPI screens, so keep the original System-aware behaviour.
      Application.SetHighDpiMode(HighDpiMode.SystemAware);
      Application.EnableVisualStyles();
      Application.SetCompatibleTextRenderingDefault(false);
      Theme.EnableDarkAppMode(); // enable per-window dark mode before any control exists
      Application.Run(new UI.NewMainForm());

      GC.KeepAlive(singleInstanceMutex);
    }

    /// <summary>Environment.GetCommandLineArgs()[0] is the exe path; drop it.</summary>
    private static string[] SkipExe(string[] argv) {
      if (argv == null || argv.Length <= 1) return System.Array.Empty<string>();
      var rest = new string[argv.Length - 1];
      System.Array.Copy(argv, 1, rest, 0, rest.Length);
      return rest;
    }
  }
}
