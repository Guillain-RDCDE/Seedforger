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

      // Drop a template so users can discover the external override format.
      TorrentClientFactory.ExportSampleIfMissing();

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
      Application.Run(new MainForm());

      GC.KeepAlive(singleInstanceMutex);
    }
  }
}
