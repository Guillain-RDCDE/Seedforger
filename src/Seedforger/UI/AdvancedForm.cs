using System;
using System.Drawing;
using System.Windows.Forms;
using Seedforger.Net;

namespace Seedforger.UI {

  /// <summary>
  /// Advanced settings: proxy, fingerprint overrides, announce interval, an
  /// automatic stop rule and the leecher download speed. Edits a
  /// <see cref="RunPreferences"/> in place when OK is pressed; the caller persists it.
  /// </summary>
  internal sealed class AdvancedForm : Form {

    private readonly RunPreferences prefs;

    private readonly ComboBox proxyType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox proxyHost = new TextBox();
    private readonly NumericUpDown proxyPort = new NumericUpDown { Minimum = 0, Maximum = 65535 };
    private readonly TextBox proxyUser = new TextBox();
    private readonly TextBox proxyPass = new TextBox { UseSystemPasswordChar = true };

    private readonly TextBox peerId = new TextBox();
    private readonly TextBox key = new TextBox();
    private readonly NumericUpDown port = new NumericUpDown { Minimum = 0, Maximum = 65535 };
    private readonly NumericUpDown numWant = new NumericUpDown { Minimum = 0, Maximum = 1000 };

    private readonly NumericUpDown interval = new NumericUpDown { Minimum = 0, Maximum = 86400, Increment = 60 };
    private readonly NumericUpDown leechDown = new NumericUpDown { Minimum = 0, Maximum = 1_000_000, Increment = 10 };
    private readonly ComboBox stopKind = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown stopValue = new NumericUpDown { Minimum = 0, Maximum = 10_000_000, DecimalPlaces = 2 };

    private static readonly (ProxyType type, string label)[] ProxyTypes = {
      (ProxyType.None, "None"), (ProxyType.HttpConnect, "HTTP CONNECT"),
      (ProxyType.Socks4, "SOCKS4"), (ProxyType.Socks4a, "SOCKS4a"), (ProxyType.Socks5, "SOCKS5"),
    };

    private static readonly StopKind[] StopKinds = {
      StopKind.Never, StopKind.AfterMinutes, StopKind.UploadedMB, StopKind.DownloadedMB,
      StopKind.RatioAbove, StopKind.SeedersBelow, StopKind.LeechersBelow,
    };

    private static string P(string en, string fr) => UiStrings.Pick(en, fr);

    internal AdvancedForm(RunPreferences prefs) {
      this.prefs = prefs;
      Text = UiStrings.Get("menu.advanced_settings").TrimEnd('…');
      FormBorderStyle = FormBorderStyle.FixedDialog;
      MaximizeBox = false; MinimizeBox = false;
      StartPosition = FormStartPosition.CenterParent;
      AutoScaleMode = AutoScaleMode.Font;
      AutoScaleDimensions = new SizeF(7F, 15F);
      Font = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
      try { Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath); } catch (Exception) { }

      foreach (var (_, label) in ProxyTypes) proxyType.Items.Add(label);
      foreach (var k in StopKinds) stopKind.Items.Add(StopKindLabel(k));

      var body = new TableLayoutPanel {
        Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, Padding = new Padding(12, 10, 12, 0),
      };
      body.Controls.Add(Group(P("Proxy (tracker traffic)", "Proxy (trafic vers le tracker)"),
        (P("Type:", "Type :"), proxyType),
        (P("Host:", "Hôte :"), proxyHost),
        (P("Port:", "Port :"), proxyPort),
        (P("Username:", "Utilisateur :"), proxyUser),
        (P("Password:", "Mot de passe :"), proxyPass)));
      body.Controls.Add(Group(P("Fingerprint overrides (blank = the client's own)", "Empreinte personnalisée (vide = celle du client)"),
        (P("peer_id:", "peer_id :"), peerId),
        (P("Key:", "Clé :"), key),
        (P("Port (0 = random):", "Port (0 = aléatoire) :"), port),
        (P("numwant (0 = default):", "numwant (0 = défaut) :"), numWant)));
      body.Controls.Add(Group(P("Run", "Exécution"),
        (P("Announce interval, s (0 = tracker's):", "Intervalle d'annonce, s (0 = celui du tracker) :"), interval),
        (P("Download speed as leecher, kB/s:", "Vitesse de téléchargement en leecher, ko/s :"), leechDown),
        (P("Stop automatically:", "Arrêt automatique :"), stopKind),
        (P("…when the value reaches:", "…quand la valeur atteint :"), stopValue)));

      var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Size = new Size(90, 28) };
      var cancel = new Button { Text = P("Cancel", "Annuler"), DialogResult = DialogResult.Cancel, Size = new Size(90, 28) };
      var bar = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(10) };
      bar.Controls.Add(cancel); bar.Controls.Add(ok);
      Controls.Add(body);
      Controls.Add(bar);
      AcceptButton = ok; CancelButton = cancel;

      ok.Click += (s, e) => { if (!Validate2()) DialogResult = DialogResult.None; else Collect(); };
      proxyType.SelectedIndexChanged += (s, e) => EnableProxyFields();
      stopKind.SelectedIndexChanged += (s, e) => stopValue.Enabled = stopKind.SelectedIndex > 0;

      Fill();
      ClientSize = new Size(520, body.PreferredSize.Height + bar.Height + 10);
    }

    private static string StopKindLabel(StopKind k) {
      switch (k) {
        case StopKind.AfterMinutes: return P("after N minutes", "après N minutes");
        case StopKind.UploadedMB: return P("once uploaded ≥ N MB", "quand envoyé ≥ N Mo");
        case StopKind.DownloadedMB: return P("once downloaded ≥ N MB", "quand téléchargé ≥ N Mo");
        case StopKind.RatioAbove: return P("once ratio ≥ N", "quand ratio ≥ N");
        case StopKind.SeedersBelow: return P("when seeders < N", "quand seeders < N");
        case StopKind.LeechersBelow: return P("when leechers < N", "quand leechers < N");
        default: return P("never", "jamais");
      }
    }

    private void Fill() {
      var px = prefs.Proxy ?? ProxySettings.None;
      proxyType.SelectedIndex = Math.Max(0, Array.FindIndex(ProxyTypes, t => t.type == px.Type));
      proxyHost.Text = px.Host; proxyPort.Value = Math.Clamp(px.Port, 0, 65535);
      proxyUser.Text = px.User; proxyPass.Text = px.Password;
      peerId.Text = prefs.PeerIdOverride; key.Text = prefs.KeyOverride;
      port.Value = Math.Clamp(prefs.Port, 0, 65535); numWant.Value = Math.Clamp(prefs.NumWant, 0, 1000);
      interval.Value = Math.Clamp(prefs.AnnounceIntervalSeconds, 0, 86400);
      leechDown.Value = Math.Clamp(prefs.LeechDownloadKBps, 0, 1_000_000);
      stopKind.SelectedIndex = Math.Max(0, Array.IndexOf(StopKinds, prefs.StopWhen.Kind));
      stopValue.Value = (decimal) Math.Clamp(prefs.StopWhen.Value, 0, 10_000_000);
      stopValue.Enabled = stopKind.SelectedIndex > 0;
      EnableProxyFields();
    }

    private void EnableProxyFields() {
      var on = proxyType.SelectedIndex > 0;
      proxyHost.Enabled = proxyPort.Enabled = proxyUser.Enabled = proxyPass.Enabled = on;
    }

    private bool Validate2() {
      if (proxyType.SelectedIndex > 0 && (proxyHost.Text.Trim().Length == 0 || proxyPort.Value == 0)) {
        MessageBox.Show(this, P("Give the proxy a host and a port.", "Indiquez l'hôte et le port du proxy."), AppInfo.Name,
          MessageBoxButtons.OK, MessageBoxIcon.Information);
        return false;
      }
      return true;
    }

    private void Collect() {
      prefs.Proxy = ProxySettings.From(ProxyTypes[proxyType.SelectedIndex].type, proxyHost.Text.Trim(), (int) proxyPort.Value,
        proxyUser.Text, proxyPass.Text);
      prefs.PeerIdOverride = peerId.Text.Trim();
      prefs.KeyOverride = key.Text.Trim();
      prefs.Port = (int) port.Value;
      prefs.NumWant = (int) numWant.Value;
      prefs.AnnounceIntervalSeconds = (int) interval.Value;
      prefs.LeechDownloadKBps = (int) leechDown.Value;
      prefs.StopWhen = StopRule.After(StopKinds[stopKind.SelectedIndex], (double) stopValue.Value);
    }

    private static GroupBox Group(string title, params (string label, Control field)[] rows) {
      var g = new GroupBox { Text = title, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top, Padding = new Padding(8, 4, 8, 8), Margin = new Padding(0, 0, 0, 10) };
      var grid = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2, RowCount = rows.Length };
      grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
      grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
      for (var i = 0; i < rows.Length; i++) {
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.Controls.Add(new Label { Text = rows[i].label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 0, 8, 0) }, 0, i);
        var f = rows[i].field;
        if (f is NumericUpDown) { f.Width = 120; f.Anchor = AnchorStyles.Left; }
        else f.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        grid.Controls.Add(f, 1, i);
      }
      g.Controls.Add(grid);
      return g;
    }
  }
}
