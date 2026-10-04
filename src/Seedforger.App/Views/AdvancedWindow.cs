using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Seedforger;
using Seedforger.UI;
using Seedforger.Net;

namespace Seedforger.App.Views {

  /// <summary>Advanced settings — proxy, fingerprint overrides, announce interval,
  /// stop rule, leecher download speed — editing a <see cref="RunPreferences"/>.
  /// <see cref="Saved"/> is true when OK was pressed.</summary>
  public sealed class AdvancedWindow : Window {

    internal bool Saved { get; private set; }

    private readonly RunPreferences prefs;

    private readonly ComboBox proxyType = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBox proxyHost = new TextBox();
    private readonly NumericUpDown proxyPort = Num(0, 0, 65535);
    private readonly TextBox proxyUser = new TextBox();
    private readonly TextBox proxyPass = new TextBox { PasswordChar = '•' };
    private readonly TextBox peerId = new TextBox();
    private readonly TextBox key = new TextBox();
    private readonly NumericUpDown port = Num(0, 0, 65535);
    private readonly NumericUpDown numWant = Num(0, 0, 1000);
    private readonly NumericUpDown interval = Num(0, 0, 86400);
    private readonly NumericUpDown leechDown = Num(30, 0, 1_000_000);
    private readonly ComboBox stopKind = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly NumericUpDown stopValue = Num(0, 0, 10_000_000);

    private static readonly ProxyType[] ProxyTypes = { ProxyType.None, ProxyType.HttpConnect, ProxyType.Socks4, ProxyType.Socks4a, ProxyType.Socks5 };
    private static readonly StopKind[] StopKinds = {
      StopKind.Never, StopKind.AfterMinutes, StopKind.UploadedMB, StopKind.DownloadedMB, StopKind.RatioAbove, StopKind.SeedersBelow, StopKind.LeechersBelow,
    };

    private static string P(string en, string fr) => UiStrings.Pick(en, fr);

    internal AdvancedWindow(RunPreferences prefs) {
      this.prefs = prefs;
      Title = UiStrings.Get("menu.advanced_settings").TrimEnd('…');
      Width = 520; SizeToContent = SizeToContent.Height; CanResize = false;
      WindowStartupLocation = WindowStartupLocation.CenterOwner;

      proxyType.ItemsSource = new[] { P("None", "Aucun"), "HTTP CONNECT", "SOCKS4", "SOCKS4a", "SOCKS5" };
      stopKind.ItemsSource = new[] {
        P("never", "jamais"), P("after N minutes", "après N minutes"), P("once uploaded ≥ N MB", "quand envoyé ≥ N Mo"),
        P("once downloaded ≥ N MB", "quand téléchargé ≥ N Mo"), P("once ratio ≥ N", "quand ratio ≥ N"),
        P("when seeders < N", "quand seeders < N"), P("when leechers < N", "quand leechers < N"),
      };

      var px = prefs.Proxy ?? ProxySettings.None;
      proxyType.SelectedIndex = Math.Max(0, Array.IndexOf(ProxyTypes, px.Type));
      proxyHost.Text = px.Host; proxyPort.Value = px.Port; proxyUser.Text = px.User; proxyPass.Text = px.Password;
      peerId.Text = prefs.PeerIdOverride; key.Text = prefs.KeyOverride;
      port.Value = prefs.Port; numWant.Value = prefs.NumWant;
      interval.Value = prefs.AnnounceIntervalSeconds; leechDown.Value = prefs.LeechDownloadKBps;
      stopKind.SelectedIndex = Math.Max(0, Array.IndexOf(StopKinds, prefs.StopWhen.Kind));
      stopValue.Value = (decimal) prefs.StopWhen.Value;

      var ok = new Button { Content = "OK", MinWidth = 90 };
      ok.Classes.Add("accent");
      var cancel = new Button { Content = P("Cancel", "Annuler"), MinWidth = 90 };
      ok.Click += (s, e) => { if (Collect()) { Saved = true; Close(); } };
      cancel.Click += (s, e) => Close();

      Content = new StackPanel {
        Margin = new Thickness(20), Spacing = 6,
        Children = {
          Section(P("Proxy (tracker traffic)", "Proxy (trafic vers le tracker)")),
          Row(P("Type", "Type"), proxyType),
          Row(P("Host", "Hôte"), proxyHost),
          Row(P("Port", "Port"), proxyPort),
          Row(P("Username (optional)", "Utilisateur (option)"), proxyUser),
          Row(P("Password (optional)", "Mot de passe (option)"), proxyPass),
          Section(P("Fingerprint overrides (blank = the client's own)", "Empreinte personnalisée (vide = celle du client)")),
          Row("peer_id", peerId),
          Row(P("Key", "Clé"), key),
          Row(P("Port (0 = random)", "Port (0 = aléatoire)"), port),
          Row(P("numwant (0 = default)", "numwant (0 = défaut)"), numWant),
          Section(P("Run", "Exécution")),
          Row(P("Announce interval, s (0 = tracker's)", "Intervalle d'annonce, s (0 = tracker)"), interval),
          Row(P("Download as leecher, kB/s", "Téléchargement en leecher, ko/s"), leechDown),
          Row(P("Stop automatically", "Arrêt automatique"), stopKind),
          Row(P("…when the value reaches", "…quand la valeur atteint"), stopValue),
          new StackPanel {
            Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0), Children = { cancel, ok },
          },
        },
      };
    }

    private bool Collect() {
      var type = ProxyTypes[Math.Max(0, proxyType.SelectedIndex)];
      var host = (proxyHost.Text ?? "").Trim();
      var pport = (int) (proxyPort.Value ?? 0);
      if (type != ProxyType.None && (host.Length == 0 || pport == 0)) return false;
      prefs.Proxy = ProxySettings.From(type, host, pport, proxyUser.Text ?? "", proxyPass.Text ?? "");
      prefs.PeerIdOverride = (peerId.Text ?? "").Trim();
      prefs.KeyOverride = (key.Text ?? "").Trim();
      prefs.Port = (int) (port.Value ?? 0);
      prefs.NumWant = (int) (numWant.Value ?? 0);
      prefs.AnnounceIntervalSeconds = (int) (interval.Value ?? 0);
      prefs.LeechDownloadKBps = (int) (leechDown.Value ?? 0);
      prefs.StopWhen = StopRule.After(StopKinds[Math.Max(0, stopKind.SelectedIndex)], (double) (stopValue.Value ?? 0));
      return true;
    }

    private static TextBlock Section(string t) => new TextBlock { Text = t, FontWeight = Avalonia.Media.FontWeight.SemiBold, Margin = new Thickness(0, 10, 0, 2) };

    private static Control Row(string label, Control field) {
      var g = new Grid { ColumnDefinitions = new ColumnDefinitions("200,*") };
      var l = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
      Grid.SetColumn(l, 0); Grid.SetColumn(field, 1);
      field.VerticalAlignment = VerticalAlignment.Center;
      g.Children.Add(l); g.Children.Add(field);
      return g;
    }

    private static NumericUpDown Num(int val, int min, int max) =>
      new NumericUpDown { Value = val, Minimum = min, Maximum = max, Width = 150, HorizontalAlignment = HorizontalAlignment.Left, FormatString = "0" };
  }
}
