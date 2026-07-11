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
      // The brand-new interface is opt-in while it reaches feature parity:
      //   Seedforger.exe --new   (or File -> Try the new interface)
      var args = Environment.GetCommandLineArgs();
      if (args.Length > 1 && args[1] == "--new") Application.Run(new UI.NewMainForm());
      else Application.Run(new MainForm());

      GC.KeepAlive(singleInstanceMutex);
    }
  }
}
