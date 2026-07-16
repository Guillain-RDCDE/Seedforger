using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Seedforger;
using Seedforger.BytesRoads;

namespace Seedforger.App.Views {

  /// <summary>Advanced settings — proxy configuration (SOCKS / HTTP-CONNECT).</summary>
  public sealed class AdvancedWindow : Window {

    internal ProxyInfo? Result { get; private set; }

    private readonly ComboBox type = new ComboBox { Width = 200, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBox host = Field();
    private readonly TextBox port = Field();
    private readonly TextBox user = Field();
    private readonly TextBox pass = Field();

    internal AdvancedWindow(ProxyInfo current) {
      Title = "Advanced — proxy";
      Width = 420; SizeToContent = SizeToContent.Height; CanResize = false;
      WindowStartupLocation = WindowStartupLocation.CenterOwner;
      Background = Brush.Parse("#15161A");

      type.ItemsSource = new[] { "None", "HTTP CONNECT", "SOCKS4", "SOCKS4a", "SOCKS5" };
      type.SelectedIndex = (int) current.ProxyType;
      host.Text = current.ProxyServer ?? "";
      port.Text = current.ProxyPort > 0 ? current.ProxyPort.ToString() : "";
      var enc = Encoding.GetEncoding(0x4e4);
      user.Text = current.ProxyUser != null ? enc.GetString(current.ProxyUser) : "";
      pass.Text = current.ProxyPassword != null ? enc.GetString(current.ProxyPassword) : "";

      var ok = new Button { Content = "OK", Width = 90, Background = Brush.Parse("#4C8DF6"), Foreground = Brushes.White };
      var cancel = new Button { Content = "Cancel", Width = 90 };
      ok.Click += (s, e) => { Result = Build(); Close(); };
      cancel.Click += (s, e) => { Result = null; Close(); };

      Content = new StackPanel {
        Margin = new Thickness(20), Spacing = 10,
        Children = {
          Label("Proxy type"), type,
          Label("Host"), host,
          Label("Port"), port,
          Label("Username (optional)"), user,
          Label("Password (optional)"), pass,
          new StackPanel {
            Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0), Children = { cancel, ok },
          },
        },
      };
    }

    private ProxyInfo Build() {
      var pt = type.SelectedIndex switch {
        1 => ProxyType.HttpConnect,
        2 => ProxyType.Socks4,
        3 => ProxyType.Socks4a,
        4 => ProxyType.Socks5,
        _ => ProxyType.None,
      };
      var enc = Encoding.GetEncoding(0x4e4);
      int.TryParse((port.Text ?? "").Trim(), out var p);
      return new ProxyInfo {
        ProxyType = pt,
        ProxyServer = (host.Text ?? "").Trim(),
        ProxyPort = p,
        ProxyUser = enc.GetBytes(user.Text ?? ""),
        ProxyPassword = enc.GetBytes(pass.Text ?? ""),
      };
    }

    private static TextBox Field() => new TextBox {
      Background = Brush.Parse("#16171B"), Foreground = Brush.Parse("#ECEEF2"),
      BorderBrush = Brush.Parse("#2C2F36"), CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 7),
    };

    private static TextBlock Label(string t) => new TextBlock {
      Text = t, Foreground = Brush.Parse("#8A909C"), FontSize = 12, Margin = new Thickness(0, 4, 0, 0),
    };
  }
}
