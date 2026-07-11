using System;
using System.Drawing;
using System.Windows.Forms;

namespace Seedforger.UI {

  /// <summary>
  /// The brand-new interface. It owns the proven RM engine as a hidden child (so
  /// the battle-tested announce logic is reused untouched) and drives it through a
  /// clean, flat, modern layout. Phase 1: the core seeding flow.
  /// </summary>
  internal sealed class NewMainForm : Form {

    private readonly RM engine = new RM();
    private readonly Timer poll = new Timer { Interval = 500 };

    private readonly Field torrentField = new Field();
    private readonly ComboBox familyBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox versionBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Field uploadField = new Field();
    private readonly ComboBox modeBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly PillButton startBtn = new PillButton { Text = "Start seeding", Fill = Modern.Green };
    private readonly PillButton stopBtn = new PillButton { Text = "Stop", Fill = Modern.Red };
    private readonly PillButton advancedBtn = new PillButton { Text = "Advanced…", Fill = Modern.CardHi };

    private readonly Label ratioValue = new Label();
    private readonly Label upValue = new Label();
    private readonly Label swarmValue = new Label();
    private readonly Label stateValue = new Label();
    private readonly RichTextBox log = new RichTextBox();

    internal NewMainForm() {
      Text = AppInfo.Title;
      BackColor = Modern.Bg;
      Font = Modern.F(9.5f);
      ClientSize = new Size(900, 640);
      MinimumSize = new Size(820, 560);
      StartPosition = FormStartPosition.CenterScreen;
      try { Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath); } catch { }
      Theme.EnableDarkAppMode();
      HandleCreated += (s, e) => Theme.TrySetDarkTitleBarPublic(this);

      BuildHeader();
      BuildLog();
      BuildContent();
      HostEngine();
      WireEngine();

      poll.Tick += (s, e) => Refresh_();
      poll.Start();
      Refresh_();
    }

    // ---- engine ----

    private void HostEngine() {
      // Keep the RM engine alive with a real handle, but invisible (clipped to 1px).
      var host = new Panel { Size = new Size(1, 1), Location = new Point(-4, -4), TabStop = false };
      engine.Dock = DockStyle.None; engine.Location = new Point(0, 0);
      host.Controls.Add(engine);
      Controls.Add(host);
      host.SendToBack();
    }

    private void WireEngine() {
      foreach (var f in engine.ClientFamilies) familyBox.Items.Add(f);
      if (familyBox.Items.Count > 0) familyBox.SelectedIndex = 0;
      RefreshVersions();
      familyBox.SelectedIndexChanged += (s, e) => { RefreshVersions(); PushClient(); };
      versionBox.SelectedIndexChanged += (s, e) => PushClient();

      modeBox.Items.AddRange(new object[] { "Seeder (100% — recommended)", "Leecher (0%)" });
      modeBox.SelectedIndex = 0;

      uploadField.Box.Text = "1024";

      engine.LogLineAdded += (s, line) => {
        if (log.InvokeRequired) { try { log.BeginInvoke((Action) (() => AppendLog(line))); } catch { } }
        else AppendLog(line);
      };
    }

    private void RefreshVersions() {
      versionBox.Items.Clear();
      var fam = familyBox.SelectedItem?.ToString();
      if (fam == null) return;
      foreach (var v in TorrentClientFactory.GetVersions(fam)) versionBox.Items.Add(v);
      if (versionBox.Items.Count > 0) versionBox.SelectedIndex = 0;
    }

    private void PushClient() =>
      engine.SetClientSelection(familyBox.SelectedItem?.ToString(), versionBox.SelectedItem?.ToString());

    private void Browse() {
      using (var dlg = new OpenFileDialog { Filter = "Torrent file (*.torrent)|*.torrent", Title = "Choose a .torrent" }) {
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try { engine.LoadTorrentFileInfo(dlg.FileName); torrentField.Box.Text = engine.TorrentDisplayName; }
        catch (Exception ex) { MessageBox.Show(this, "Couldn't read that .torrent: " + ex.Message, AppInfo.Name); }
      }
    }

    private void Start() {
      PushClient();
      engine.SetFinishedPercent(modeBox.SelectedIndex == 0 ? 100 : 0);
      if (modeBox.SelectedIndex == 0) engine.SetDownloadKBps(0);
      if (int.TryParse(uploadField.Box.Text.Trim(), out var up) && up > 0) engine.SetUploadKBps(up);
      engine.CampaignStart();
    }

    private void Refresh_() {
      var running = engine.IsRunning;
      startBtn.Enabled = !running;
      stopBtn.Enabled = running;
      ratioValue.Text = engine.Ratio.ToString("0.00");
      upValue.Text = RM.FormatFileSize((ulong) Math.Max(0, engine.UploadedBytes));
      var seed = engine.SeederCount; var leech = engine.LeecherCount;
      swarmValue.Text = (seed < 0 ? "–" : seed.ToString()) + "  /  " + (leech < 0 ? "–" : leech.ToString());
      stateValue.Text = running ? "Seeding" : "Idle";
      stateValue.ForeColor = running ? Modern.Green : Modern.Muted;
    }

    private void AppendLog(string line) {
      log.SelectionStart = log.TextLength;
      log.AppendText(line + "\n");
      log.ScrollToCaret();
    }

    // ---- layout ----

    private void BuildHeader() {
      var header = new Panel { Dock = DockStyle.Top, Height = 60, BackColor = Modern.Bg };
      var title = new Label { Text = "Seedforger", Font = Modern.Semibold(15f), ForeColor = Modern.Text, AutoSize = true, Location = new Point(22, 12), BackColor = Modern.Bg };
      var sub = new Label { Text = "believable torrent stats — no bytes moved", Font = Modern.F(8.5f), ForeColor = Modern.Muted, AutoSize = true, Location = new Point(24, 35), BackColor = Modern.Bg };
      header.Controls.Add(sub); header.Controls.Add(title);
      Controls.Add(header);
    }

    private void BuildLog() {
      var wrap = new Panel { Dock = DockStyle.Bottom, Height = 168, BackColor = Modern.Bg, Padding = new Padding(20, 6, 20, 16) };
      var lbl = new Label { Text = "ACTIVITY", Font = Modern.Semibold(8f), ForeColor = Modern.Muted, AutoSize = true, Dock = DockStyle.Top, BackColor = Modern.Bg };
      log.BorderStyle = BorderStyle.None; log.BackColor = Modern.LogBg; log.ForeColor = Modern.LogText;
      log.Font = new Font("Cascadia Mono", 8.5f, FontStyle.Regular, GraphicsUnit.Point);
      log.ReadOnly = true; log.Dock = DockStyle.Fill; log.WordWrap = true; log.ScrollBars = RichTextBoxScrollBars.Vertical;
      var host = new Panel { Dock = DockStyle.Fill, BackColor = Modern.LogBg, Padding = new Padding(12, 10, 8, 10) };
      host.Controls.Add(log);
      wrap.Controls.Add(host); wrap.Controls.Add(lbl);
      Controls.Add(wrap);
    }

    private void BuildContent() {
      var content = new Panel { Dock = DockStyle.Fill, BackColor = Modern.Bg, Padding = new Padding(20, 4, 20, 4) };
      Controls.Add(content);
      content.BringToFront();

      // ----- left config column -----
      var left = new Panel { BackColor = Modern.Bg, Location = new Point(20, 6), Size = new Size(520, 380), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom };
      content.Controls.Add(left);

      var torrentCard = MakeCard(left, 0, "TORRENT", 92);
      torrentField.SetBounds(16, 40, 360, 38); torrentField.Box.ReadOnly = true; torrentField.Box.Text = "No torrent loaded";
      var browse = new PillButton { Text = "Browse", Fill = Modern.Accent, Bounds = new Rectangle(388, 40, 108, 38), Font = Modern.F(9.5f) };
      browse.Click += (s, e) => Browse();
      torrentCard.Controls.Add(torrentField); torrentCard.Controls.Add(browse);

      var clientCard = MakeCard(left, 104, "IMPERSONATE (which client)", 92);
      StyleCombo(familyBox); familyBox.SetBounds(16, 40, 230, 30);
      StyleCombo(versionBox); versionBox.SetBounds(258, 40, 120, 30);
      advancedBtn.SetBounds(392, 39, 104, 32); advancedBtn.Font = Modern.F(9f); advancedBtn.TextColor = Modern.Text;
      advancedBtn.Click += (s, e) => engine.ShowAdvanced();
      clientCard.Controls.Add(familyBox); clientCard.Controls.Add(versionBox); clientCard.Controls.Add(advancedBtn);

      var speedCard = MakeCard(left, 208, "SPEED & MODE", 96);
      var upLbl = new Label { Text = "Upload kB/s", Font = Modern.F(8.5f), ForeColor = Modern.Muted, AutoSize = true, Location = new Point(16, 38), BackColor = Modern.Card };
      uploadField.SetBounds(16, 56, 150, 34);
      var modeLbl = new Label { Text = "Mode", Font = Modern.F(8.5f), ForeColor = Modern.Muted, AutoSize = true, Location = new Point(186, 38), BackColor = Modern.Card };
      StyleCombo(modeBox); modeBox.SetBounds(186, 57, 310, 30);
      speedCard.Controls.Add(upLbl); speedCard.Controls.Add(uploadField); speedCard.Controls.Add(modeLbl); speedCard.Controls.Add(modeBox);

      startBtn.SetBounds(0, 320, 250, 46); startBtn.Font = Modern.Semibold(11f);
      stopBtn.SetBounds(262, 320, 150, 46); stopBtn.Font = Modern.Semibold(11f);
      startBtn.Click += (s, e) => Start();
      stopBtn.Click += (s, e) => engine.CampaignStop();
      left.Controls.Add(startBtn); left.Controls.Add(stopBtn);

      // ----- right stats column -----
      var statsCard = new Card { Location = new Point(556, 6), Size = new Size(300, 366), Anchor = AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom };
      content.Controls.Add(statsCard);
      statsCard.Controls.Add(new Label { Text = "LIVE", Font = Modern.Semibold(8f), ForeColor = Modern.Muted, AutoSize = true, Location = new Point(18, 16), BackColor = Modern.Card });

      ratioValue.Font = new Font(Modern.Family, 40f, FontStyle.Bold, GraphicsUnit.Point);
      ratioValue.ForeColor = Modern.Text; ratioValue.AutoSize = false; ratioValue.TextAlign = ContentAlignment.MiddleLeft;
      ratioValue.SetBounds(16, 40, 270, 64); ratioValue.BackColor = Modern.Card; ratioValue.Text = "0.00";
      statsCard.Controls.Add(ratioValue);
      statsCard.Controls.Add(new Label { Text = "RATIO", Font = Modern.F(8f), ForeColor = Modern.Muted, AutoSize = true, Location = new Point(18, 106), BackColor = Modern.Card });

      AddStat(statsCard, 150, "Uploaded", upValue);
      AddStat(statsCard, 210, "Seeders / Leechers", swarmValue);
      AddStat(statsCard, 270, "Status", stateValue);
    }

    private Card MakeCard(Panel parent, int y, string title, int height) {
      var card = new Card { Location = new Point(0, y), Size = new Size(516, height), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
      parent.Controls.Add(card);
      card.Controls.Add(new Label { Text = title, Font = Modern.Semibold(8f), ForeColor = Modern.Muted, AutoSize = true, Location = new Point(16, 14), BackColor = Modern.Card });
      return card;
    }

    private void AddStat(Card parent, int y, string label, Label value) {
      parent.Controls.Add(new Label { Text = label.ToUpperInvariant(), Font = Modern.F(8f), ForeColor = Modern.Muted, AutoSize = true, Location = new Point(18, y), BackColor = Modern.Card });
      value.Font = Modern.Semibold(13f); value.ForeColor = Modern.Text; value.AutoSize = false;
      value.SetBounds(16, y + 16, 270, 26); value.BackColor = Modern.Card; value.TextAlign = ContentAlignment.MiddleLeft;
      value.Text = "–";
      parent.Controls.Add(value);
    }

    private static void StyleCombo(ComboBox cb) => Modern.DarkCombo(cb);
  }
}
