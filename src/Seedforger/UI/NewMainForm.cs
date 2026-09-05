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
    private readonly Label downValue = new Label();
    private readonly Label speedValue = new Label();
    private readonly Label swarmValue = new Label();
    private readonly Label elapsedValue = new Label();
    private readonly Label stateValue = new Label();
    private readonly RichTextBox log = new RichTextBox();

    // Live up-speed (bytes/s) and elapsed are derived from the poll, not the engine.
    private long prevUpBytes;
    private DateTime prevTick = DateTime.UtcNow;
    private DateTime? startedAt;

    private GraphForm graphForm;

    // i18n: controls whose Text / tooltip follow the language toggle at runtime.
    private readonly List<(Control c, string key)> loc = new List<(Control, string)>();
    private readonly List<(Control c, string key)> tipReg = new List<(Control, string)>();

    // Tray (minimize/close to notification area).
    private readonly NotifyIcon tray = new NotifyIcon();
    private bool reallyExit;
    private bool trayHintShown;
    private bool minimizingFromClose;

    private static string T(string key) => UiStrings.Get(key);
    private TC Reg<TC>(TC c, string key) where TC : Control { c.Text = UiStrings.Get(key); loc.Add((c, key)); return c; }
    private void RegTip(Control c, string key) { tips.SetToolTip(c, UiStrings.Get(key)); tipReg.Add((c, key)); }

    internal NewMainForm() {
      Text = AppInfo.Title;
      BackColor = Modern.Bg;
      Font = Modern.F(9f);
      // Compact, RatioMaster-dense: everything on one screen, no wasted space.
      ClientSize = new Size(716, 470);
      MinimumSize = new Size(700, 470);
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

    // ---- window behaviour: tray on minimize, taskbar on close ----

    private void SetupTray() {
      tray.Icon = Icon ?? System.Drawing.SystemIcons.Application;
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
        if (WindowState == FormWindowState.Minimized && Settings.Current.MinimizeToTray && !minimizingFromClose)
          HideToTray();
      };
      FormClosing += (s, e) => {
        // A user-initiated close (X button, Alt+F4) shouldn't kill a run in
        // progress, so it minimizes to the taskbar instead — where the window
        // stays plainly visible, unlike a tray icon Windows 11 hides in the
        // overflow. Quit for real from the ⚙ menu or the tray icon. Never fight
        // a real OS shutdown, task-manager kill or explicit app exit.
        var systemClose = e.CloseReason == CloseReason.WindowsShutDown
          || e.CloseReason == CloseReason.TaskManagerClosing
          || e.CloseReason == CloseReason.ApplicationExitCall;
        if (!reallyExit && !systemClose && Settings.Current.CloseToTray) {
          e.Cancel = true;
          try { MinimizeToTaskbar(); } catch { }
          return;
        }
        // Real exit: stop the main engine and any campaign engines cleanly.
        try { engine.CampaignStop(); } catch { }
        try { campaignRunner?.Stop(); } catch { }
        tray.Visible = false;
      };
    }

    /// <summary>
    /// Minimize to the taskbar rather than to the notification area: the button
    /// stays where the user is looking. The flag keeps the Resize handler from
    /// re-routing this into the tray when "minimize to tray" is on — that option
    /// is about the minimize button, not about closing.
    /// </summary>
    private void MinimizeToTaskbar() {
      minimizingFromClose = true;
      try {
        ShowInTaskbar = true;
        if (!Visible) Show();
        WindowState = FormWindowState.Minimized;
      }
      finally { minimizingFromClose = false; }
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
      downValue.Text = RM.FormatFileSize((ulong) down);

      // Live up-speed from the byte delta between polls; elapsed from the first running tick.
      var now = DateTime.UtcNow;
      var dt = (now - prevTick).TotalSeconds;
      if (running) {
        if (startedAt == null) startedAt = now;
        if (dt > 0 && up >= prevUpBytes) {
          var bps = (up - prevUpBytes) / dt;
          speedValue.Text = RM.FormatFileSize((ulong) Math.Max(0, bps)) + "/s";
        }
        elapsedValue.Text = (now - startedAt.Value).ToString(@"hh\:mm\:ss");
      } else {
        startedAt = null;
        speedValue.Text = "–";
        elapsedValue.Text = "–";
      }
      prevUpBytes = up; prevTick = now;

      var seed = engine.SeederCount; var leech = engine.LeecherCount;
      swarmValue.Text = (seed < 0 ? "–" : seed.ToString()) + " / " + (leech < 0 ? "–" : leech.ToString());
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
      var header = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = Modern.Bg };

      var title = new Label { Text = "Seedforger", Font = Modern.Semibold(13f), ForeColor = Modern.Text, AutoSize = true, Location = new Point(16, 12), BackColor = Modern.Bg };

      var nav = new FlowLayoutPanel {
        Dock = DockStyle.Right, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
        FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = Modern.Bg,
        Padding = new Padding(0, 9, 12, 0),
      };
      // RightToLeft: first added sits rightmost, so add in reverse visual order.
      nav.Controls.Add(MakeNav("?", 34, ShowHelpMenu, tipKey: "tip.help"));
      nav.Controls.Add(MakeNav("⚙", 34, ShowSettingsMenu, tipKey: "tip.settings"));
      nav.Controls.Add(MakeNav("Tools", 58, ShowToolsMenu, tipKey: "tip.tools", textKey: "nav.tools"));
      nav.Controls.Add(MakeNav("Campaigns", 88, _ => OpenCampaigns(), tipKey: "tip.campaigns", textKey: "nav.campaigns"));
      nav.Controls.Add(MakeNav("Guided", 66, _ => OpenGuided(), tipKey: "tip.guided", textKey: "nav.guided"));

      header.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Modern.Border });
      header.Controls.Add(nav);
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

      // Window behaviour: the minimize button can go to the notification area, the
      // close button minimizes to the taskbar.
      m.Items.Add(new ToolStripSeparator());
      var minTray = DarkMenu.Item(T("menu.minimize_tray"), null, Settings.Current.MinimizeToTray);
      minTray.Click += (s, e) => { Settings.Current.MinimizeToTray = minTray.Checked; Settings.Current.Save(); };
      var closeMin = DarkMenu.Item(T("menu.close_minimizes"), null, Settings.Current.CloseToTray);
      closeMin.Click += (s, e) => { Settings.Current.CloseToTray = closeMin.Checked; Settings.Current.Save(); };
      var balloon = DarkMenu.Item(T("menu.tray_balloon"), null, Settings.Current.BallonTip);
      balloon.Click += (s, e) => { Settings.Current.BallonTip = balloon.Checked; Settings.Current.Save(); };
      m.Items.Add(minTray); m.Items.Add(closeMin); m.Items.Add(balloon);

      // With the X button minimizing, quitting needs a home that isn't the tray
      // icon (which Windows 11 likes to hide).
      m.Items.Add(new ToolStripSeparator());
      m.Items.Add(DarkMenu.Item(T("menu.quit"), (s, e) => { reallyExit = true; Close(); }));

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
      var wrap = new Panel { Dock = DockStyle.Bottom, Height = 132, BackColor = Modern.Bg, Padding = new Padding(14, 2, 14, 12) };
      var lbl = Reg(new Label { Font = Modern.Semibold(7.5f), ForeColor = Modern.Muted, AutoSize = true, Dock = DockStyle.Top, BackColor = Modern.Bg }, "activity");
      log.BorderStyle = BorderStyle.None; log.BackColor = Modern.LogBg; log.ForeColor = Modern.LogText;
      log.Font = new Font("Cascadia Mono", 8f, FontStyle.Regular, GraphicsUnit.Point);
      log.ReadOnly = true; log.Dock = DockStyle.Fill; log.WordWrap = true; log.ScrollBars = RichTextBoxScrollBars.Vertical;
      var host = new Panel { Dock = DockStyle.Fill, BackColor = Modern.LogBg, Padding = new Padding(10, 8, 6, 8) };
      host.Controls.Add(log);
      wrap.Controls.Add(host); wrap.Controls.Add(lbl);
      Controls.Add(wrap);
    }

    private void BuildContent() {
      var content = new Panel { Dock = DockStyle.Fill, BackColor = Modern.Bg, Padding = new Padding(0) };
      Controls.Add(content);
      content.BringToFront();

      // ----- left: one dense SETUP card -----
      var setup = new Card { Location = new Point(14, 4), Size = new Size(384, 252), Anchor = AnchorStyles.Top | AnchorStyles.Left };
      content.Controls.Add(setup);
      setup.Controls.Add(Reg(new Label { Font = Modern.Semibold(8f), ForeColor = Modern.Muted, AutoSize = true, Location = new Point(14, 12), BackColor = Modern.Card }, "card.setup"));

      // torrent
      setup.Controls.Add(Reg(new Label { Font = Modern.F(7.5f), ForeColor = Modern.Muted, AutoSize = true, Location = new Point(14, 34), BackColor = Modern.Card }, "card.torrent"));
      torrentField.SetBounds(14, 50, 250, 32); torrentField.Box.ReadOnly = true; torrentField.Box.Text = T("no_torrent");
      var browse = Reg(new PillButton { Fill = Modern.Accent, Bounds = new Rectangle(270, 50, 100, 32), Font = Modern.F(9f) }, "browse");
      browse.Click += (s, e) => Browse();
      setup.Controls.Add(torrentField); setup.Controls.Add(browse);

      // client
      setup.Controls.Add(Reg(new Label { Font = Modern.F(7.5f), ForeColor = Modern.Muted, AutoSize = true, Location = new Point(14, 90), BackColor = Modern.Card }, "hdr.client"));
      StyleCombo(familyBox); familyBox.SetBounds(14, 107, 180, 30);
      StyleCombo(versionBox); versionBox.SetBounds(198, 107, 84, 30);
      Reg(advancedBtn, "advanced");
      advancedBtn.SetBounds(286, 107, 84, 30); advancedBtn.Font = Modern.F(8.5f); advancedBtn.TextColor = Modern.Text;
      advancedBtn.Click += (s, e) => engine.ShowAdvanced();
      RegTip(advancedBtn, "tip.advanced");
      RegTip(familyBox, "tip.client");
      setup.Controls.Add(familyBox); setup.Controls.Add(versionBox); setup.Controls.Add(advancedBtn);

      // mode + upload
      setup.Controls.Add(Reg(new Label { Font = Modern.F(7.5f), ForeColor = Modern.Muted, AutoSize = true, Location = new Point(14, 144), BackColor = Modern.Card }, "hdr.mode"));
      StyleCombo(modeBox); modeBox.SetBounds(14, 161, 230, 30);
      setup.Controls.Add(Reg(new Label { Font = Modern.F(7.5f), ForeColor = Modern.Muted, AutoSize = true, Location = new Point(250, 144), BackColor = Modern.Card }, "hdr.upload"));
      uploadField.SetBounds(250, 160, 120, 32);
      setup.Controls.Add(modeBox); setup.Controls.Add(uploadField);

      // actions
      Reg(startBtn, "start_seeding"); Reg(stopBtn, "stop");
      startBtn.SetBounds(14, 204, 230, 36); startBtn.Font = Modern.Semibold(10f);
      stopBtn.SetBounds(250, 204, 120, 36); stopBtn.Font = Modern.Semibold(10f);
      startBtn.Click += (s, e) => Start();
      stopBtn.Click += (s, e) => engine.CampaignStop();
      setup.Controls.Add(startBtn); setup.Controls.Add(stopBtn);

      // ----- right: a compact LIVE readout with the ratio front and centre -----
      var live = new Card { Location = new Point(412, 4), Size = new Size(290, 252), Anchor = AnchorStyles.Top | AnchorStyles.Right };
      content.Controls.Add(live);
      live.Controls.Add(Reg(new Label { Font = Modern.Semibold(8f), ForeColor = Modern.Muted, AutoSize = true, Location = new Point(14, 12), BackColor = Modern.Card }, "live"));

      ratioCaption.Font = Modern.F(7.5f); ratioCaption.ForeColor = Modern.Muted;
      ratioCaption.AutoSize = true; ratioCaption.Location = new Point(16, 32); ratioCaption.BackColor = Modern.Card;
      Reg(ratioCaption, "ratio");
      live.Controls.Add(ratioCaption);
      ratioValue.Font = new Font(Modern.Family, 26f, FontStyle.Bold, GraphicsUnit.Point);
      ratioValue.ForeColor = Modern.Text; ratioValue.AutoSize = false; ratioValue.TextAlign = ContentAlignment.MiddleLeft;
      ratioValue.SetBounds(14, 44, 262, 42); ratioValue.BackColor = Modern.Card; ratioValue.Text = "—";
      live.Controls.Add(ratioValue);
      RegTip(ratioValue, "tip.ratio");
      RegTip(ratioCaption, "tip.ratio");

      var sep = new Panel { BackColor = Modern.Border, Bounds = new Rectangle(14, 94, 262, 1) };
      live.Controls.Add(sep);

      AddRow(live, 104, "uploaded", upValue);
      AddRow(live, 128, "downloaded", downValue);
      AddRow(live, 152, "up_speed", speedValue);
      AddRow(live, 176, "swarm", swarmValue);
      AddRow(live, 200, "elapsed", elapsedValue);
      AddRow(live, 224, "status", stateValue);
    }

    /// <summary>A tight label-left / value-right readout row (RatioMaster density).</summary>
    private void AddRow(Card parent, int y, string labelKey, Label value) {
      parent.Controls.Add(Reg(new Label { Font = Modern.F(8f), ForeColor = Modern.Muted, AutoSize = true, Location = new Point(16, y + 2), BackColor = Modern.Card }, labelKey));
      value.Font = Modern.Semibold(10f); value.ForeColor = Modern.Text; value.AutoSize = false;
      value.SetBounds(120, y, 156, 19); value.BackColor = Modern.Card; value.TextAlign = ContentAlignment.MiddleRight;
      value.Text = "–";
      parent.Controls.Add(value);
    }

    private static void StyleCombo(ComboBox cb) => Modern.DarkCombo(cb);
  }
}
