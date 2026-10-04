using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Seedforger;
using Seedforger.BytesRoads;

namespace Seedforger.App.Views {

  /// <summary>Advanced settings — proxy configuration (SOCKS / HTTP-CONNECT), as a
  /// plain labelled form in the theme's own controls.</summary>
  public sealed class AdvancedWindow : Window {

    internal ProxyInfo? Result { get; private set; }

    private readonly ComboBox type = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBox host = new TextBox();
    private readonly TextBox port = new TextBox();
    private readonly TextBox user = new TextBox();
    private readonly TextBox pass = new TextBox { PasswordChar = '•' };

    private static string P(string en, string fr) => AppOptions.Language == Language.French ? fr : en;

    internal AdvancedWindow(ProxyInfo current) {
      Title = P("Advanced settings — proxy", "Réglages avancés — proxy");
      Width = 460; SizeToContent = SizeToContent.Height; CanResize = false;
      WindowStartupLocation = WindowStartupLocation.CenterOwner;

      type.ItemsSource = new[] { P("None", "Aucun"), "HTTP CONNECT", "SOCKS4", "SOCKS4a", "SOCKS5" };
      type.SelectedIndex = (int) current.ProxyType;
      host.Text = current.ProxyServer ?? "";
      port.Text = current.ProxyPort > 0 ? current.ProxyPort.ToString() : "";
      var enc = Encoding.GetEncoding(0x4e4);
      user.Text = current.ProxyUser != null ? enc.GetString(current.ProxyUser) : "";
      pass.Text = current.ProxyPassword != null ? enc.GetString(current.ProxyPassword) : "";

      var ok = new Button { Content = "OK", MinWidth = 90 };
      ok.Classes.Add("accent");
      var cancel = new Button { Content = P("Cancel", "Annuler"), MinWidth = 90 };
      ok.Click += (s, e) => { Result = Build(); Close(); };
      cancel.Click += (s, e) => { Result = null; Close(); };

      Content = new StackPanel {
        Margin = new Thickness(20), Spacing = 8,
        Children = {
          Row(P("Proxy type", "Type de proxy"), type),
          Row(P("Host", "Hôte"), host),
          Row(P("Port", "Port"), port),
          Row(P("Username (optional)", "Utilisateur (option)"), user),
          Row(P("Password (optional)", "Mot de passe (option)"), pass),
          new StackPanel {
            Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0), Children = { cancel, ok },
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

    private static Control Row(string label, Control field) {
      var g = new Grid { ColumnDefinitions = new ColumnDefinitions("170,*") };
      var l = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
      Grid.SetColumn(l, 0); Grid.SetColumn(field, 1);
      g.Children.Add(l); g.Children.Add(field);
      return g;
    }
  }
}
