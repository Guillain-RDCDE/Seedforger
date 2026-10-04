using System;
using System.Text;
using Avalonia;

namespace Seedforger.App {

  internal static class Program {

    // Avalonia entry point. Kept minimal — the app is configured in App.axaml(.cs).
    [STAThread]
    public static void Main(string[] args) {
      Encodings.Register();
      BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
      AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();
  }
}
