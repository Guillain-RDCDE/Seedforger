using System;
using System.Text;
using Avalonia;

namespace Seedforger.App {

  internal static class Program {

    // Avalonia entry point. Kept minimal — the app is configured in App.axaml(.cs).
    [STAThread]
    public static void Main(string[] args) {
      // Legacy code pages (Windows-1252) used by the BEncode/tracker layer.
      Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
      BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
      AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();
  }
}
