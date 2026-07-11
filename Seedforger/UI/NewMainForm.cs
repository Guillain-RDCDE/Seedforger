using System;
using System.Drawing;
using System.Windows.Forms;

namespace Seedforger.UI {

  /// <summary>
  /// The brand-new interface. It owns the proven RM engine as a hidden child (so
  /// the battle-tested announce logic is reused untouched) and drives it through a
  /// clean, flat, modern layout — with a header nav that reaches every feature
  /// (guided mode, campaigns, tools, settings, help) instead of a 2000s menu bar.
  /// </summary>
  internal sealed class NewMainForm : Form {

    private readonly RM engine = new RM();
    private readonly Timer poll = new Timer { Interval = 500 };
    private readonly ToolTip tips = new ToolTip { AutoPopDelay = 15000, InitialDelay = 300, ReshowDelay = 100 };

    private readonly Field torrentField = new Field();
    private readonly ComboBox familyBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox versionBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Field uploadField = new Field();
    private readonly ComboBox modeBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly PillButton startBtn = new PillButton { Text = "Start seeding", Fill = Modern.Green };
    private readonly PillButton stopBtn = new PillButton { Text = "Stop", Fill = Modern.Red };
    private readonly PillButton advancedBtn = new PillButton { Text = "Advanced…", Fill = Modern.CardHi };

    private readonly Label ratioValue = new Label();
    private readonly Label ratioCaption = new Label();
    private readonly Label upValue = new Label();
    private readonly Label swarmValue = new Label();
    private readonly Label stateValue = new Label();
    private readonly RichTextBox log = new RichTextBox();

    private GraphForm graphForm;

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
      var up = Math.Max(0, engine.UploadedBytes);
      var down = Math.Max(0, engine.DownloadedBytes);
      // A pure seeder downloads nothing, so the ratio is mathematically infinite —
      // showing "0.00" or "∞" both confuse. We show "—" and explain it in a tooltip.
      ratioValue.Text = down > 0 ? ((double) up / down).ToString("0.00") : "—";
      upValue.Text = RM.FormatFileSize((ulong) up);
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

    // ---- header + navigation ----

    private void BuildHeader() {
      var header = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Modern.Bg };

      var title = new Label { Text = "Seedforger", Font = Modern.Semibold(15f), ForeColor = Modern.Text, AutoSize = true, Location = new Point(22, 12), BackColor = Modern.Bg };
      var sub = new Label { Text = "believable torrent stats — no bytes moved", Font = Modern.F(8.5f), ForeColor = Modern.Muted, AutoSize = true, Location = new Point(24, 36), BackColor = Modern.Bg };

      var nav = new FlowLayoutPanel {
        Dock = DockStyle.Right, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
        FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = Modern.Bg,
        Padding = new Padding(0, 17, 14, 0),
      };
      // RightToLeft: first added sits rightmost, so add in reverse visual order.
      nav.Controls.Add(MakeNav("?", 40, ShowHelpMenu, "About & links"));
      nav.Controls.Add(MakeNav("⚙", 40, ShowSettingsMenu, "Settings"));
      nav.Controls.Add(MakeNav("Tools", 64, ShowToolsMenu, "Magnet, dry-run, live graph…"));
      nav.Controls.Add(MakeNav("Campaigns", 94, _ => OpenCampaigns(), "Run many torrents on a schedule"));
      nav.Controls.Add(MakeNav("Guided", 74, _ => OpenGuided(), "Step-by-step newbie mode"));

      header.Controls.Add(nav);
      header.Controls.Add(sub);
      header.Controls.Add(title);
      Controls.Add(header);
    }

    private NavButton MakeNav(string text, int width, Action<Control> onClick, string tip) {
      var b = new NavButton(text) { Width = width, Margin = new Padding(3, 0, 3, 0) };
      b.Click += (s, e) => onClick(b);
      if (tip != null) tips.SetToolTip(b, tip);
      return b;
    }

    private void ShowMenuUnder(Control anchor, ContextMenuStrip menu) =>
      menu.Show(anchor, new Point(0, anchor.Height));

    // ---- header actions ----

    private void OpenGuided() {
      using (var g = new GuideForm(engine, ApplyProfileToEngine)) g.ShowDialog(this);
    }

    private void OpenCampaigns() {
      using (var wizard = new CampaignForm()) {
        if (wizard.ShowDialog(this) != DialogResult.OK || wizard.Result == null) return;
        // Campaigns are inherently multi-torrent; the classic multi-tab window owns
        // that orchestration, so we open it to actually run the campaign.
        MessageBox.Show(this,
          "Campaigns run many torrents at once, so they open in the multi-torrent window.",
          AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
        var mf = new MainForm();
        mf.Show();
        mf.RunCampaign(wizard.Result);
      }
    }

    private void ShowToolsMenu(Control anchor) {
      var m = DarkMenu.Create();
      m.Items.Add(DarkMenu.Item("Open magnet…", (s, e) => OpenMagnet()));
      m.Items.Add(DarkMenu.Item("Load a .torrent…", (s, e) => Browse()));
      m.Items.Add(new ToolStripSeparator());
      m.Items.Add(DarkMenu.Item("Test announce (dry-run)", (s, e) => engine.TestAnnounce()));
      m.Items.Add(DarkMenu.Item("Serve a real file (advanced)…", (s, e) => ServeReal()));
      m.Items.Add(DarkMenu.Item("Live graph…", (s, e) => ShowGraph()));
      ShowMenuUnder(anchor, m);
    }

    private void ShowSettingsMenu(Control anchor) {
      var m = DarkMenu.Create();

      var realistic = DarkMenu.Item("Realistic speed (ramp-up)", null, AppOptions.RealisticSpeed);
      realistic.Click += (s, e) => { AppOptions.RealisticSpeed = realistic.Checked; Settings.Current.RealisticSpeed = realistic.Checked; Settings.Current.Save(); };
      var swarm = DarkMenu.Item("Swarm-aware speeds", null, AppOptions.SwarmAware);
      swarm.Click += (s, e) => { AppOptions.SwarmAware = swarm.Checked; Settings.Current.SwarmAware = swarm.Checked; Settings.Current.Save(); };
      var rotate = DarkMenu.Item("Randomize client on start", null, AppOptions.RandomizeClientOnStart);
      rotate.Click += (s, e) => { AppOptions.RandomizeClientOnStart = rotate.Checked; Settings.Current.RandomizeClientOnStart = rotate.Checked; Settings.Current.Save(); };
      m.Items.Add(realistic); m.Items.Add(swarm); m.Items.Add(rotate);

      m.Items.Add(new ToolStripSeparator());
      var conn = DarkMenu.Item("Connection profile");
      foreach (var p in ConnectionProfiles.All) {
        var name = p.Name;
        conn.DropDownItems.Add(DarkMenu.Item(name, (s, e) => ApplyProfileToEngine(name)));
      }
      if (conn.HasDropDownItems && conn.DropDown is ToolStripDropDownMenu dd) dd.Renderer = m.Renderer;
      m.Items.Add(conn);
      m.Items.Add(DarkMenu.Item("Active hours…", (s, e) => SetActiveHours()));

      m.Items.Add(new ToolStripSeparator());
      var lang = DarkMenu.Item("Language");
      var en = DarkMenu.Item("English", (s, e) => SetLanguage(Language.English), AppOptions.Language == Language.English);
      var fr = DarkMenu.Item("Français", (s, e) => SetLanguage(Language.French), AppOptions.Language == Language.French);
      lang.DropDownItems.Add(en); lang.DropDownItems.Add(fr);
      if (lang.DropDown is ToolStripDropDownMenu ld) ld.Renderer = m.Renderer;
      m.Items.Add(lang);

      ShowMenuUnder(anchor, m);
    }

    private void ShowHelpMenu(Control anchor) {
      var m = DarkMenu.Create();
      m.Items.Add(DarkMenu.Item("About " + AppInfo.Name, (s, e) => MessageBox.Show(this,
        $"{AppInfo.Name} v{AppInfo.Version}\n\n{AppInfo.SiteUrl}", AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Information)));
      m.Items.Add(DarkMenu.Item("Open the GitHub repo", (s, e) => OpenUrl(AppInfo.SiteUrl)));
      ShowMenuUnder(anchor, m);
    }

    // ---- action helpers ----

    private void OpenMagnet() {
      using (var p = new Prompt("Open magnet", "Paste a magnet link:", "")) {
        if (p.ShowDialog() != DialogResult.OK) return;
        var uri = (p.Result ?? "").Trim();
        if (uri.Length > 0) { try { engine.LoadMagnet(uri); torrentField.Box.Text = engine.TorrentDisplayName; } catch (Exception ex) { MessageBox.Show(this, ex.Message, AppInfo.Name); } }
      }
    }

    private void ServeReal() {
      using (var dlg = new OpenFileDialog { Title = "Pick the downloaded file that matches this torrent" }) {
        if (dlg.ShowDialog(this) == DialogResult.OK) engine.EnableRealSeed(dlg.FileName);
      }
    }

    private void ShowGraph() {
      if (graphForm == null || graphForm.IsDisposed) { graphForm = new GraphForm(() => engine); graphForm.Show(this); }
      else graphForm.Activate();
    }

    private void SetActiveHours() {
      var current = AppOptions.ActiveHoursEnabled ? $"{AppOptions.ActiveHoursStart}-{AppOptions.ActiveHoursEnd}" : "";
      using (var prompt = new Prompt("Active hours",
               "Seed only between these hours (0-24), e.g. 8-24 or 22-6.\nLeave empty for 24/7:", current)) {
        if (prompt.ShowDialog() != DialogResult.OK) return;
        var text = (prompt.Result ?? "").Trim();
        if (text.Length == 0) { AppOptions.ActiveHoursEnabled = false; }
        else {
          var parts = text.Split('-');
          if (parts.Length != 2 || !int.TryParse(parts[0].Trim(), out var a) || !int.TryParse(parts[1].Trim(), out var b)
              || a < 0 || a > 24 || b < 0 || b > 24) {
            MessageBox.Show(this, "Use a format like 8-24 or 22-6.", AppInfo.Name); return;
          }
          AppOptions.ActiveHoursEnabled = true; AppOptions.ActiveHoursStart = a; AppOptions.ActiveHoursEnd = b;
        }
        var s = Settings.Current;
        s.ActiveHoursEnabled = AppOptions.ActiveHoursEnabled;
        s.ActiveHoursStart = AppOptions.ActiveHoursStart;
        s.ActiveHoursEnd = AppOptions.ActiveHoursEnd;
        s.Save();
      }
    }

    private void SetLanguage(Language lang) {
      AppOptions.Language = lang;
      Settings.Current.Language = Localization.Code(lang);
      Settings.Current.Save();
    }

    /// <summary>Applies a connection profile to our single engine (upload/download
    /// caps with a small jitter, plus the global upstream budget), mirroring the
    /// classic MainForm behaviour.</summary>
    private void ApplyProfileToEngine(string name) {
      ConnectionProfile prof = null;
      foreach (var p in ConnectionProfiles.All) if (p.Name == name) { prof = p; break; }
      if (prof == null) return;
      var r = new Random();
      int Jitter(int v) => Math.Max(1, (int) (v * (0.92 + r.NextDouble() * 0.16)));
      engine.SetUploadKBps(Jitter(prof.UpKBps));
      engine.SetDownloadKBps(Jitter(prof.DownKBps));
      Bandwidth.GlobalUpKBps = prof.UpKBps;
      Settings.Current.GlobalUpstreamKBps = prof.UpKBps;
      Settings.Current.Save();
      uploadField.Box.Text = prof.UpKBps.ToString();
    }

    private static void OpenUrl(string url) {
      try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    }

    // ---- content layout ----

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
      tips.SetToolTip(advancedBtn, "Custom fingerprint & proxy");
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
      ratioValue.SetBounds(16, 40, 270, 64); ratioValue.BackColor = Modern.Card; ratioValue.Text = "—";
      statsCard.Controls.Add(ratioValue);
      ratioCaption.Text = "RATIO"; ratioCaption.Font = Modern.F(8f); ratioCaption.ForeColor = Modern.Muted;
      ratioCaption.AutoSize = true; ratioCaption.Location = new Point(18, 106); ratioCaption.BackColor = Modern.Card;
      statsCard.Controls.Add(ratioCaption);
      // The "—" is not obvious, so explain it right on the numbers.
      var ratioTip = "Ratio = uploaded ÷ downloaded.\nA seeder downloads nothing, so the ratio is infinite — shown as “—”.\nIt becomes a real number only if you simulate some download.";
      tips.SetToolTip(ratioValue, ratioTip);
      tips.SetToolTip(ratioCaption, ratioTip);

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
