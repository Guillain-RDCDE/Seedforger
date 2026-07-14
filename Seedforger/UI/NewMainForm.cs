using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace Seedforger.UI {

  /// <summary>
  /// The interface. It owns the proven RM engine as a hidden child (so the
  /// battle-tested announce logic is reused untouched) and drives it through a
  /// clean, flat, modern layout — with a header nav that reaches every feature
  /// (guided mode, campaigns, tools, settings, help). It is also the campaign
  /// host: multi-torrent runs live in hidden engines owned by this window.
  /// </summary>
  internal sealed class NewMainForm : Form, ICampaignHost {

    private readonly RM engine = new RM();
    private readonly Timer poll = new Timer { Interval = 500 };
    private readonly ToolTip tips = new ToolTip { AutoPopDelay = 15000, InitialDelay = 300, ReshowDelay = 100 };
    private readonly List<Control> campaignHosts = new List<Control>();
    private CampaignRunner campaignRunner;

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

    // i18n: controls whose Text / tooltip follow the language toggle at runtime.
    private readonly List<(Control c, string key)> loc = new List<(Control, string)>();
    private readonly List<(Control c, string key)> tipReg = new List<(Control, string)>();

    // Tray (minimize/close to notification area).
    private readonly NotifyIcon tray = new NotifyIcon();
    private bool reallyExit;
    private bool trayHintShown;

    private static string T(string key) => UiStrings.Get(key);
    private TC Reg<TC>(TC c, string key) where TC : Control { c.Text = UiStrings.Get(key); loc.Add((c, key)); return c; }
    private void RegTip(Control c, string key) { tips.SetToolTip(c, UiStrings.Get(key)); tipReg.Add((c, key)); }

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

      LoadPersistedSettings();
      BuildHeader();
      BuildLog();
      BuildContent();
      HostEngine();
      WireEngine();
      SetupTray();

      poll.Tick += (s, e) => Refresh_();
      poll.Start();
      Refresh_();
      StartUpdateCheck();
    }

    // ---- tray (minimize / close to the notification area) ----

    private void SetupTray() {
      tray.Icon = Icon;
      tray.Text = AppInfo.Title;
      var menu = DarkMenu.Create();
      var restore = DarkMenu.Item(T("tray.restore"), (s, e) => RestoreFromTray());
      var quit = DarkMenu.Item(T("tray.quit"), (s, e) => { reallyExit = true; Close(); });
      // Menu items are ToolStripItems (not Controls), so relabel them when the menu opens.
      menu.Opening += (s, e) => { restore.Text = T("tray.restore"); quit.Text = T("tray.quit"); };
      menu.Items.Add(restore); menu.Items.Add(quit);
      tray.ContextMenuStrip = menu;
      tray.DoubleClick += (s, e) => RestoreFromTray();

      Resize += (s, e) => {
        if (WindowState == FormWindowState.Minimized && Settings.Current.MinimizeToTray) HideToTray();
      };
      FormClosing += (s, e) => {
        if (!reallyExit && e.CloseReason == CloseReason.UserClosing && Settings.Current.CloseToTray) {
          e.Cancel = true;
          HideToTray();
          return;
        }
        // Real exit: stop the main engine and any campaign engines cleanly.
        try { engine.CampaignStop(); } catch { }
        try { campaignRunner?.Stop(); } catch { }
        tray.Visible = false;
      };
    }

    private void HideToTray() {
      Hide();
      tray.Visible = true;
      // Show the hint at least the first time (and thereafter if the user enabled it),
      // so closing/minimizing doesn't look like the app vanished.
      if (!trayHintShown || Settings.Current.BallonTip) {
        trayHintShown = true;
        try { tray.ShowBalloonTip(4000, AppInfo.Name, T("tray.balloon_text"), ToolTipIcon.Info); } catch { }
      }
    }

    private void RestoreFromTray() {
      Show();
      WindowState = FormWindowState.Normal;
      tray.Visible = false;
      Activate();
    }

    // Mirror the persisted settings into the process-wide options at launch.
    private static void LoadPersistedSettings() {
      try {
        var s = Settings.Current;
        AppOptions.RealisticSpeed = s.RealisticSpeed;
        AppOptions.RandomizeClientOnStart = s.RandomizeClientOnStart;
        AppOptions.ActiveHoursEnabled = s.ActiveHoursEnabled;
        AppOptions.ActiveHoursStart = s.ActiveHoursStart;
        AppOptions.ActiveHoursEnd = s.ActiveHoursEnd;
        AppOptions.Language = Localization.Parse(s.Language);
        AppOptions.SwarmAware = s.SwarmAware;
        AppOptions.DarkMode = true; // the new interface is dark-only
        Bandwidth.GlobalUpKBps = s.GlobalUpstreamKBps;
        s.Save();
      }
      catch { /* first run / unreadable settings: keep defaults */ }
    }

    // Silent update-check at launch: only prompts if a newer release exists.
    private void StartUpdateCheck() {
      UpdateChecker.CheckInBackground((tag, url) => {
        try {
          BeginInvoke((Action) (() => {
            var r = MessageBox.Show(this,
              string.Format(T("dlg.update_text"), tag, AppInfo.Version),
              AppInfo.Name + " — " + T("dlg.update_title"), MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (r == DialogResult.Yes) OpenUrl(url);
          }));
        }
        catch { /* form gone */ }
      });
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

      modeBox.Items.AddRange(new object[] { T("mode.seeder"), T("mode.leecher") });
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
      using (var dlg = new OpenFileDialog { Filter = T("dlg.torrent_filter"), Title = T("dlg.choose_torrent") }) {
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try { engine.LoadTorrentFileInfo(dlg.FileName); torrentField.Box.Text = engine.TorrentDisplayName; }
        catch (Exception ex) { MessageBox.Show(this, T("dlg.read_error") + ex.Message, AppInfo.Name); }
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
      stateValue.Text = running ? T("seeding") : T("idle");
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
      var sub = Reg(new Label { Font = Modern.F(8.5f), ForeColor = Modern.Muted, AutoSize = true, Location = new Point(24, 36), BackColor = Modern.Bg }, "subtitle");

      var nav = new FlowLayoutPanel {
        Dock = DockStyle.Right, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
        FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = Modern.Bg,
        Padding = new Padding(0, 17, 14, 0),
      };
      // RightToLeft: first added sits rightmost, so add in reverse visual order.
      nav.Controls.Add(MakeNav("?", 40, ShowHelpMenu, tipKey: "tip.help"));
      nav.Controls.Add(MakeNav("⚙", 40, ShowSettingsMenu, tipKey: "tip.settings"));
      nav.Controls.Add(MakeNav("Tools", 64, ShowToolsMenu, tipKey: "tip.tools", textKey: "nav.tools"));
      nav.Controls.Add(MakeNav("Campaigns", 94, _ => OpenCampaigns(), tipKey: "tip.campaigns", textKey: "nav.campaigns"));
      nav.Controls.Add(MakeNav("Guided", 74, _ => OpenGuided(), tipKey: "tip.guided", textKey: "nav.guided"));

      header.Controls.Add(nav);
      header.Controls.Add(sub);
      header.Controls.Add(title);
      Controls.Add(header);
    }

    private NavButton MakeNav(string text, int width, Action<Control> onClick, string tipKey = null, string textKey = null) {
      var b = new NavButton(text) { Width = width, Margin = new Padding(3, 0, 3, 0) };
      b.Click += (s, e) => onClick(b);
      if (textKey != null) Reg(b, textKey);
      if (tipKey != null) RegTip(b, tipKey);
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
        // Multi-torrent runs are orchestrated right here, in hidden engines this
        // window hosts (see the ICampaignHost implementation below).
        campaignRunner?.Stop();
        campaignRunner = new CampaignRunner(this, wizard.Result);
        AppendLog("[campaign] starting…");
        campaignRunner.Start();
      }
    }

    // ---- ICampaignHost: multi-torrent orchestration in hidden engines ----

    RM ICampaignHost.CreateEngine(string torrentPath) {
      var rm = new RM();
      var host = new Panel { Size = new Size(1, 1), Location = new Point(-4, -4), TabStop = false };
      rm.Dock = DockStyle.None; rm.Location = new Point(0, 0);
      host.Controls.Add(rm);
      Controls.Add(host);
      host.SendToBack();
      _ = host.Handle; _ = rm.Handle;
      rm.LoadTorrentFileInfo(torrentPath);
      campaignHosts.Add(host);
      return rm;
    }

    void ICampaignHost.ApplyConnectionProfile(RM rm, string name) => ApplyProfileTo(rm, name);

    void ICampaignHost.Log(string message) {
      if (log.InvokeRequired) { try { log.BeginInvoke((Action) (() => AppendLog("[campaign] " + message))); } catch { } }
      else AppendLog("[campaign] " + message);
    }

    private void ShowToolsMenu(Control anchor) {
      var m = DarkMenu.Create();
      m.Items.Add(DarkMenu.Item(T("menu.open_magnet"), (s, e) => OpenMagnet()));
      m.Items.Add(DarkMenu.Item(T("menu.load_torrent"), (s, e) => Browse()));
      m.Items.Add(new ToolStripSeparator());
      m.Items.Add(DarkMenu.Item(T("menu.test_announce"), (s, e) => engine.TestAnnounce()));
      m.Items.Add(DarkMenu.Item(T("menu.serve_real"), (s, e) => ServeReal()));
      m.Items.Add(DarkMenu.Item(T("menu.live_graph"), (s, e) => ShowGraph()));
      ShowMenuUnder(anchor, m);
    }

    private void ShowSettingsMenu(Control anchor) {
      var m = DarkMenu.Create();

      var realistic = DarkMenu.Item(T("menu.realistic"), null, AppOptions.RealisticSpeed);
      realistic.Click += (s, e) => { AppOptions.RealisticSpeed = realistic.Checked; Settings.Current.RealisticSpeed = realistic.Checked; Settings.Current.Save(); };
      var swarm = DarkMenu.Item(T("menu.swarm"), null, AppOptions.SwarmAware);
      swarm.Click += (s, e) => { AppOptions.SwarmAware = swarm.Checked; Settings.Current.SwarmAware = swarm.Checked; Settings.Current.Save(); };
      var rotate = DarkMenu.Item(T("menu.randomize"), null, AppOptions.RandomizeClientOnStart);
      rotate.Click += (s, e) => { AppOptions.RandomizeClientOnStart = rotate.Checked; Settings.Current.RandomizeClientOnStart = rotate.Checked; Settings.Current.Save(); };
      m.Items.Add(realistic); m.Items.Add(swarm); m.Items.Add(rotate);

      m.Items.Add(new ToolStripSeparator());
      var conn = DarkMenu.Item(T("menu.connection"));
      foreach (var p in ConnectionProfiles.All) {
        var name = p.Name;
        conn.DropDownItems.Add(DarkMenu.Item(name, (s, e) => ApplyProfileToEngine(name)));
      }
      if (conn.HasDropDownItems && conn.DropDown is ToolStripDropDownMenu dd) dd.Renderer = m.Renderer;
      m.Items.Add(conn);
      m.Items.Add(DarkMenu.Item(T("menu.active_hours"), (s, e) => SetActiveHours()));

      // Tray behaviour.
      m.Items.Add(new ToolStripSeparator());
      var minTray = DarkMenu.Item(T("menu.minimize_tray"), null, Settings.Current.MinimizeToTray);
      minTray.Click += (s, e) => { Settings.Current.MinimizeToTray = minTray.Checked; Settings.Current.Save(); };
      var closeTray = DarkMenu.Item(T("menu.close_tray"), null, Settings.Current.CloseToTray);
      closeTray.Click += (s, e) => { Settings.Current.CloseToTray = closeTray.Checked; Settings.Current.Save(); };
      var balloon = DarkMenu.Item(T("menu.tray_balloon"), null, Settings.Current.BallonTip);
      balloon.Click += (s, e) => { Settings.Current.BallonTip = balloon.Checked; Settings.Current.Save(); };
      m.Items.Add(minTray); m.Items.Add(closeTray); m.Items.Add(balloon);

      m.Items.Add(new ToolStripSeparator());
      var lang = DarkMenu.Item(T("menu.language"));
      var en = DarkMenu.Item("English", (s, e) => SetLanguage(Language.English), AppOptions.Language == Language.English);
      var fr = DarkMenu.Item("Français", (s, e) => SetLanguage(Language.French), AppOptions.Language == Language.French);
      lang.DropDownItems.Add(en); lang.DropDownItems.Add(fr);
      if (lang.DropDown is ToolStripDropDownMenu ld) ld.Renderer = m.Renderer;
      m.Items.Add(lang);

      ShowMenuUnder(anchor, m);
    }

    private void ShowHelpMenu(Control anchor) {
      var m = DarkMenu.Create();
      m.Items.Add(DarkMenu.Item(T("menu.about"), (s, e) => MessageBox.Show(this,
        $"{AppInfo.Name} v{AppInfo.Version}\n\n{AppInfo.SiteUrl}", AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Information)));
      m.Items.Add(DarkMenu.Item(T("menu.open_repo"), (s, e) => OpenUrl(AppInfo.SiteUrl)));
      ShowMenuUnder(anchor, m);
    }

    // ---- action helpers ----

    private void OpenMagnet() {
      using (var p = new Prompt(T("dlg.magnet_title"), T("dlg.magnet_label"), "")) {
        if (p.ShowDialog() != DialogResult.OK) return;
        var uri = (p.Result ?? "").Trim();
        if (uri.Length > 0) { try { engine.LoadMagnet(uri); torrentField.Box.Text = engine.TorrentDisplayName; } catch (Exception ex) { MessageBox.Show(this, ex.Message, AppInfo.Name); } }
      }
    }

    private void ServeReal() {
      using (var dlg = new OpenFileDialog { Title = T("dlg.serve_title") }) {
        if (dlg.ShowDialog(this) == DialogResult.OK) engine.EnableRealSeed(dlg.FileName);
      }
    }

    private void ShowGraph() {
      if (graphForm == null || graphForm.IsDisposed) { graphForm = new GraphForm(() => engine); graphForm.Show(this); }
      else graphForm.Activate();
    }

    private void SetActiveHours() {
      var current = AppOptions.ActiveHoursEnabled ? $"{AppOptions.ActiveHoursStart}-{AppOptions.ActiveHoursEnd}" : "";
      using (var prompt = new Prompt(T("dlg.hours_title"), T("dlg.hours_label"), current)) {
        if (prompt.ShowDialog() != DialogResult.OK) return;
        var text = (prompt.Result ?? "").Trim();
        if (text.Length == 0) { AppOptions.ActiveHoursEnabled = false; }
        else {
          var parts = text.Split('-');
          if (parts.Length != 2 || !int.TryParse(parts[0].Trim(), out var a) || !int.TryParse(parts[1].Trim(), out var b)
              || a < 0 || a > 24 || b < 0 || b > 24) {
            MessageBox.Show(this, T("dlg.hours_bad"), AppInfo.Name); return;
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
      ApplyLanguage();
    }

    /// <summary>Re-applies every registered text/tooltip in the current language.</summary>
    private void ApplyLanguage() {
      foreach (var (c, k) in loc) c.Text = UiStrings.Get(k);
      foreach (var (c, k) in tipReg) tips.SetToolTip(c, UiStrings.Get(k));
      tray.Text = AppInfo.Title;
      // The torrent field shows a placeholder only when nothing is loaded.
      if (string.IsNullOrEmpty(engine.TorrentDisplayName)) torrentField.Box.Text = T("no_torrent");
      // Rebuild the mode combo (localized items), preserving the selection.
      var idx = modeBox.SelectedIndex;
      modeBox.Items.Clear();
      modeBox.Items.AddRange(new object[] { T("mode.seeder"), T("mode.leecher") });
      modeBox.SelectedIndex = idx < 0 ? 0 : idx;
      Refresh_(); // status label (Seeding/Idle)
    }

    /// <summary>Applies a connection profile to the main engine (upload/download
    /// caps with a small jitter, plus the global upstream budget) and reflects the
    /// cap in the upload field.</summary>
    private void ApplyProfileToEngine(string name) {
      var prof = ApplyProfileTo(engine, name);
      if (prof != null) uploadField.Box.Text = prof.UpKBps.ToString();
    }

    /// <summary>Shared profile application, usable on any engine (main or a
    /// hidden campaign engine). Returns the matched profile, or null.</summary>
    private ConnectionProfile ApplyProfileTo(RM rm, string name) {
      ConnectionProfile prof = null;
      foreach (var p in ConnectionProfiles.All) if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) { prof = p; break; }
      if (prof == null) return null;
      var r = new Random();
      int Jitter(int v) => Math.Max(1, (int) (v * (0.92 + r.NextDouble() * 0.16)));
      rm.SetUploadKBps(Jitter(prof.UpKBps));
      rm.SetDownloadKBps(Jitter(prof.DownKBps));
      Bandwidth.GlobalUpKBps = prof.UpKBps;
      Settings.Current.GlobalUpstreamKBps = prof.UpKBps;
      Settings.Current.Save();
      return prof;
    }

    private static void OpenUrl(string url) {
      try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    }

    // ---- content layout ----

    private void BuildLog() {
      var wrap = new Panel { Dock = DockStyle.Bottom, Height = 168, BackColor = Modern.Bg, Padding = new Padding(20, 6, 20, 16) };
      var lbl = Reg(new Label { Font = Modern.Semibold(8f), ForeColor = Modern.Muted, AutoSize = true, Dock = DockStyle.Top, BackColor = Modern.Bg }, "activity");
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

      var torrentCard = MakeCard(left, 0, "card.torrent", 92);
      torrentField.SetBounds(16, 40, 360, 38); torrentField.Box.ReadOnly = true; torrentField.Box.Text = T("no_torrent");
      var browse = Reg(new PillButton { Fill = Modern.Accent, Bounds = new Rectangle(388, 40, 108, 38), Font = Modern.F(9.5f) }, "browse");
      browse.Click += (s, e) => Browse();
      torrentCard.Controls.Add(torrentField); torrentCard.Controls.Add(browse);

      var clientCard = MakeCard(left, 104, "card.client", 96);
      var appLbl = Reg(new Label { Font = Modern.F(8.5f), ForeColor = Modern.Muted, AutoSize = true, Location = new Point(16, 36), BackColor = Modern.Card }, "client");
      var verLbl = Reg(new Label { Font = Modern.F(8.5f), ForeColor = Modern.Muted, AutoSize = true, Location = new Point(258, 36), BackColor = Modern.Card }, "version");
      StyleCombo(familyBox); familyBox.SetBounds(16, 54, 230, 30);
      StyleCombo(versionBox); versionBox.SetBounds(258, 54, 120, 30);
      Reg(advancedBtn, "advanced");
      advancedBtn.SetBounds(392, 53, 104, 32); advancedBtn.Font = Modern.F(9f); advancedBtn.TextColor = Modern.Text;
      advancedBtn.Click += (s, e) => engine.ShowAdvanced();
      RegTip(advancedBtn, "tip.advanced");
      RegTip(familyBox, "tip.client");
      clientCard.Controls.Add(appLbl); clientCard.Controls.Add(verLbl);
      clientCard.Controls.Add(familyBox); clientCard.Controls.Add(versionBox); clientCard.Controls.Add(advancedBtn);

      var speedCard = MakeCard(left, 208, "card.speed", 96);
      var upLbl = Reg(new Label { Font = Modern.F(8.5f), ForeColor = Modern.Muted, AutoSize = true, Location = new Point(16, 38), BackColor = Modern.Card }, "upload_kbs");
      uploadField.SetBounds(16, 56, 150, 34);
      var modeLbl = Reg(new Label { Font = Modern.F(8.5f), ForeColor = Modern.Muted, AutoSize = true, Location = new Point(186, 38), BackColor = Modern.Card }, "mode");
      StyleCombo(modeBox); modeBox.SetBounds(186, 57, 310, 30);
      speedCard.Controls.Add(upLbl); speedCard.Controls.Add(uploadField); speedCard.Controls.Add(modeLbl); speedCard.Controls.Add(modeBox);

      Reg(startBtn, "start_seeding"); Reg(stopBtn, "stop");
      startBtn.SetBounds(0, 320, 250, 46); startBtn.Font = Modern.Semibold(11f);
      stopBtn.SetBounds(262, 320, 150, 46); stopBtn.Font = Modern.Semibold(11f);
      startBtn.Click += (s, e) => Start();
      stopBtn.Click += (s, e) => engine.CampaignStop();
      left.Controls.Add(startBtn); left.Controls.Add(stopBtn);

      // ----- right stats column -----
      var statsCard = new Card { Location = new Point(556, 6), Size = new Size(300, 366), Anchor = AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom };
      content.Controls.Add(statsCard);
      statsCard.Controls.Add(Reg(new Label { Font = Modern.Semibold(8f), ForeColor = Modern.Muted, AutoSize = true, Location = new Point(18, 16), BackColor = Modern.Card }, "live"));

      ratioValue.Font = new Font(Modern.Family, 40f, FontStyle.Bold, GraphicsUnit.Point);
      ratioValue.ForeColor = Modern.Text; ratioValue.AutoSize = false; ratioValue.TextAlign = ContentAlignment.MiddleLeft;
      ratioValue.SetBounds(16, 40, 270, 64); ratioValue.BackColor = Modern.Card; ratioValue.Text = "—";
      statsCard.Controls.Add(ratioValue);
      ratioCaption.Font = Modern.F(8f); ratioCaption.ForeColor = Modern.Muted;
      ratioCaption.AutoSize = true; ratioCaption.Location = new Point(18, 106); ratioCaption.BackColor = Modern.Card;
      Reg(ratioCaption, "ratio");
      statsCard.Controls.Add(ratioCaption);
      // The "—" is not obvious, so explain it right on the numbers.
      RegTip(ratioValue, "tip.ratio");
      RegTip(ratioCaption, "tip.ratio");

      AddStat(statsCard, 150, "uploaded", upValue);
      AddStat(statsCard, 210, "swarm", swarmValue);
      AddStat(statsCard, 270, "status", stateValue);
    }

    private Card MakeCard(Panel parent, int y, string titleKey, int height) {
      var card = new Card { Location = new Point(0, y), Size = new Size(516, height), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
      parent.Controls.Add(card);
      card.Controls.Add(Reg(new Label { Font = Modern.Semibold(8f), ForeColor = Modern.Muted, AutoSize = true, Location = new Point(16, 14), BackColor = Modern.Card }, titleKey));
      return card;
    }

    private void AddStat(Card parent, int y, string labelKey, Label value) {
      parent.Controls.Add(Reg(new Label { Font = Modern.F(8f), ForeColor = Modern.Muted, AutoSize = true, Location = new Point(18, y), BackColor = Modern.Card }, labelKey));
      value.Font = Modern.Semibold(13f); value.ForeColor = Modern.Text; value.AutoSize = false;
      value.SetBounds(16, y + 16, 270, 26); value.BackColor = Modern.Card; value.TextAlign = ContentAlignment.MiddleLeft;
      value.Text = "–";
      parent.Controls.Add(value);
    }

    private static void StyleCombo(ComboBox cb) => Modern.DarkCombo(cb);
  }
}
