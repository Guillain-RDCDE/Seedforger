using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Seedforger.UI {

  /// <summary>
  /// The main window, in a classic Windows layout: a menu bar, labelled group
  /// boxes with standard controls in the system font and colours, a log, and a
  /// status bar. Everything is laid out with table layouts so it scales cleanly
  /// with the font and the DPI instead of relying on hand-placed pixels.
  ///
  /// It owns the proven RM engine as a hidden child (so the battle-tested
  /// announce logic is reused untouched) and is also the campaign host:
  /// multi-torrent runs live in hidden engines owned by this window.
  /// </summary>
  internal sealed class MainForm : Form, ICampaignHost {

    private readonly RM engine = new RM();
    private readonly Timer poll = new Timer { Interval = 500 };
    private readonly ToolTip tips = new ToolTip { AutoPopDelay = 15000, InitialDelay = 400, ReshowDelay = 100 };
    private readonly List<Control> campaignHosts = new List<Control>();
    private CampaignRunner campaignRunner;

    // ---- menu bar ----
    private readonly MenuStrip menu = new MenuStrip();
    private ToolStripMenuItem startItem, stopItem, langEnItem, langFrItem;

    // ---- torrent group ----
    private readonly GroupBox torrentGroup = new GroupBox();
    private readonly TextBox torrentBox = new TextBox { ReadOnly = true };
    private readonly Button browseBtn = new Button();
    private readonly ComboBox familyBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox versionBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button advancedBtn = new Button();
    private readonly ComboBox modeBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox uploadBox = new TextBox { Text = "1024", TextAlign = HorizontalAlignment.Right };
    private readonly Button startBtn = new Button();
    private readonly Button stopBtn = new Button();
    private readonly Button testBtn = new Button();

    // ---- status group ----
    private readonly GroupBox statusGroup = new GroupBox();
    private readonly Label ratioValue = new Label();
    private readonly Label upValue = new Label();
    private readonly Label downValue = new Label();
    private readonly Label speedValue = new Label();
    private readonly Label swarmValue = new Label();
    private readonly Label elapsedValue = new Label();
    private readonly Label stateValue = new Label();

    // ---- log + status bar ----
    private readonly GroupBox logGroup = new GroupBox();
    private readonly RichTextBox log = new RichTextBox();
    private readonly StatusStrip statusBar = new StatusStrip();
    private readonly ToolStripStatusLabel stateStatus = new ToolStripStatusLabel();
    private readonly ToolStripStatusLabel clientStatus = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ToolStripStatusLabel versionStatus = new ToolStripStatusLabel();

    private static readonly Color RunningGreen = Color.FromArgb(0x1B, 0x7F, 0x3B);

    // Live up-speed (bytes/s) and elapsed are derived from the poll, not the engine.
    private long prevUpBytes;
    private DateTime prevTick = DateTime.UtcNow;
    private DateTime? startedAt;

    private GraphForm graphForm;

    // i18n: everything whose caption or tooltip follows the language toggle at runtime.
    private readonly List<(Control c, string key)> loc = new List<(Control, string)>();
    private readonly List<(ToolStripItem i, string key)> menuLoc = new List<(ToolStripItem, string)>();
    private readonly List<(Control c, string key)> tipReg = new List<(Control, string)>();

    // Tray (minimize/close to notification area).
    private readonly NotifyIcon tray = new NotifyIcon();
    private bool reallyExit;
    private bool trayHintShown;
    private bool minimizingFromClose;

    private static string T(string key) => UiStrings.Get(key);
    private TC Reg<TC>(TC c, string key) where TC : Control { c.Text = T(key); loc.Add((c, key)); return c; }
    private void RegTip(Control c, string key) { tips.SetToolTip(c, T(key)); tipReg.Add((c, key)); }

    internal MainForm() {
      Text = AppInfo.Title;
      // Scale by font so the layout follows the system DPI and font size.
      AutoScaleMode = AutoScaleMode.Font;
      AutoScaleDimensions = new SizeF(7F, 15F);
      Font = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
      ClientSize = new Size(800, 600);
      MinimumSize = new Size(740, 560);
      StartPosition = FormStartPosition.CenterScreen;
      try { Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath); } catch { }

      LoadPersistedSettings();
      SuspendLayout();
      BuildContent();   // Fill — added first so the docked bars win the edges
      BuildMenu();
      BuildStatusBar();
      ResumeLayout(true);
      HostEngine();
      WireEngine();
      SetupTray();
      SetupDragDrop(this);

      poll.Tick += (s, e) => Refresh_();
      poll.Start();
      Refresh_();
      StartUpdateCheck();
    }

    // ---- window behaviour: tray on minimize, taskbar on close ----

    private void SetupTray() {
      tray.Icon = Icon ?? SystemIcons.Application;
      tray.Text = AppInfo.Title;
      var trayMenu = new ContextMenuStrip();
      var restore = new ToolStripMenuItem(T("tray.restore"), null, (s, e) => RestoreFromTray());
      var quit = new ToolStripMenuItem(T("tray.quit"), null, (s, e) => { reallyExit = true; Close(); });
      // Menu items are ToolStripItems (not Controls), so relabel them when the menu opens.
      trayMenu.Opening += (s, e) => { restore.Text = T("tray.restore"); quit.Text = T("tray.quit"); };
      trayMenu.Items.Add(restore); trayMenu.Items.Add(quit);
      tray.ContextMenuStrip = trayMenu;
      tray.DoubleClick += (s, e) => RestoreFromTray();

      Resize += (s, e) => {
        if (WindowState == FormWindowState.Minimized && Settings.Current.MinimizeToTray && !minimizingFromClose)
          HideToTray();
      };
      FormClosing += (s, e) => {
        // A user-initiated close (X button, Alt+F4) shouldn't kill a run in
        // progress, so it minimizes to the taskbar instead — where the window
        // stays plainly visible, unlike a tray icon Windows 11 hides in the
        // overflow. Quit for real from File → Exit or the tray icon. Never fight
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
        AppOptions.DarkMode = false; // the classic interface uses the system look
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
        LoadTorrent(dlg.FileName);
      }
    }

    private void LoadTorrent(string path) {
      try { engine.LoadTorrentFileInfo(path); torrentBox.Text = engine.TorrentDisplayName; }
      catch (Exception ex) {
        MessageBox.Show(this, T("dlg.read_error") + ex.Message, AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
      }
    }

    private void Start() {
      if (!engine.HasTorrentLoaded) {
        MessageBox.Show(this, T("dlg.no_torrent"), AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
        return;
      }
      if (!int.TryParse(uploadBox.Text.Trim(), out var up) || up <= 0) {
        MessageBox.Show(this, T("dlg.upload_bad"), AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
        uploadBox.Focus(); uploadBox.SelectAll();
        return;
      }
      PushClient();
      engine.SetFinishedPercent(modeBox.SelectedIndex == 0 ? 100 : 0);
      if (modeBox.SelectedIndex == 0) engine.SetDownloadKBps(0);
      engine.SetUploadKBps(up);
      engine.CampaignStart();
    }

    private void Refresh_() {
      var running = engine.IsRunning;
      startBtn.Enabled = !running;
      stopBtn.Enabled = running;
      if (startItem != null) startItem.Enabled = !running;
      if (stopItem != null) stopItem.Enabled = running;

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
      stateValue.ForeColor = running ? RunningGreen : SystemColors.ControlText;

      stateStatus.Text = stateValue.Text;
      var client = ((familyBox.SelectedItem?.ToString() ?? "") + " " + (versionBox.SelectedItem?.ToString() ?? "")).Trim();
      clientStatus.Text = string.Format(T("status.client"), client);
    }

    private void AppendLog(string line) {
      log.SelectionStart = log.TextLength;
      log.AppendText(line + "\n");
      log.ScrollToCaret();
    }

    // ---- menu bar ----

    private ToolStripMenuItem Item(string key, EventHandler onClick = null, Keys shortcut = Keys.None) {
      var it = new ToolStripMenuItem(T(key));
      menuLoc.Add((it, key));
      if (onClick != null) it.Click += onClick;
      if (shortcut != Keys.None) it.ShortcutKeys = shortcut;
      return it;
    }

    private ToolStripMenuItem Check(string key, bool initial, Action<bool> onToggle) {
      var it = Item(key);
      it.CheckOnClick = true;
      it.Checked = initial;
      // CheckOnClick flips Checked before the Click event is raised.
      it.Click += (s, e) => onToggle(it.Checked);
      return it;
    }

    private void BuildMenu() {
      menu.RenderMode = ToolStripRenderMode.System;
      menu.Dock = DockStyle.Top;

      // File
      var file = Item("menu.file");
      file.DropDownItems.Add(Item("menu.load_torrent", (s, e) => Browse(), Keys.Control | Keys.O));
      file.DropDownItems.Add(Item("menu.open_magnet", (s, e) => OpenMagnet(), Keys.Control | Keys.M));
      file.DropDownItems.Add(new ToolStripSeparator());
      file.DropDownItems.Add(Item("menu.exit", (s, e) => { reallyExit = true; Close(); }));

      // Run
      var run = Item("menu.run");
      startItem = Item("start_seeding", (s, e) => Start(), Keys.F5);
      stopItem = Item("stop", (s, e) => engine.CampaignStop(), Keys.F6);
      run.DropDownItems.Add(startItem);
      run.DropDownItems.Add(stopItem);
      run.DropDownItems.Add(new ToolStripSeparator());
      run.DropDownItems.Add(Item("menu.test_announce", (s, e) => engine.TestAnnounce(), Keys.F7));
      run.DropDownItems.Add(Item("menu.serve_real", (s, e) => ServeReal()));

      // Tools
      var tools = Item("menu.tools");
      tools.DropDownItems.Add(Item("menu.guided", (s, e) => OpenGuided()));
      tools.DropDownItems.Add(Item("menu.campaigns", (s, e) => OpenCampaigns()));
      tools.DropDownItems.Add(Item("menu.live_graph", (s, e) => ShowGraph()));
      tools.DropDownItems.Add(new ToolStripSeparator());
      tools.DropDownItems.Add(Item("menu.advanced_settings", (s, e) => engine.ShowAdvanced()));

      // Settings
      var settings = Item("menu.settings");
      settings.DropDownItems.Add(Check("menu.realistic", AppOptions.RealisticSpeed, v => {
        AppOptions.RealisticSpeed = v; Settings.Current.RealisticSpeed = v; Settings.Current.Save();
      }));
      settings.DropDownItems.Add(Check("menu.swarm", AppOptions.SwarmAware, v => {
        AppOptions.SwarmAware = v; Settings.Current.SwarmAware = v; Settings.Current.Save();
      }));
      settings.DropDownItems.Add(Check("menu.randomize", AppOptions.RandomizeClientOnStart, v => {
        AppOptions.RandomizeClientOnStart = v; Settings.Current.RandomizeClientOnStart = v; Settings.Current.Save();
      }));
      settings.DropDownItems.Add(new ToolStripSeparator());
      var conn = Item("menu.connection");
      foreach (var p in ConnectionProfiles.All) {
        var name = p.Name;
        conn.DropDownItems.Add(new ToolStripMenuItem(name, null, (s, e) => ApplyProfileToEngine(name)));
      }
      settings.DropDownItems.Add(conn);
      settings.DropDownItems.Add(Item("menu.active_hours", (s, e) => SetActiveHours()));
      settings.DropDownItems.Add(new ToolStripSeparator());
      // Window behaviour: the minimize button can go to the notification area, the
      // close button minimizes to the taskbar.
      settings.DropDownItems.Add(Check("menu.minimize_tray", Settings.Current.MinimizeToTray, v => {
        Settings.Current.MinimizeToTray = v; Settings.Current.Save();
      }));
      settings.DropDownItems.Add(Check("menu.close_minimizes", Settings.Current.CloseToTray, v => {
        Settings.Current.CloseToTray = v; Settings.Current.Save();
      }));
      settings.DropDownItems.Add(Check("menu.tray_balloon", Settings.Current.BallonTip, v => {
        Settings.Current.BallonTip = v; Settings.Current.Save();
      }));
      settings.DropDownItems.Add(new ToolStripSeparator());
      var lang = Item("menu.language");
      langEnItem = new ToolStripMenuItem("English", null, (s, e) => SetLanguage(Language.English)) { Checked = AppOptions.Language == Language.English };
      langFrItem = new ToolStripMenuItem("Français", null, (s, e) => SetLanguage(Language.French)) { Checked = AppOptions.Language == Language.French };
      lang.DropDownItems.Add(langEnItem); lang.DropDownItems.Add(langFrItem);
      settings.DropDownItems.Add(lang);

      // Help
      var help = Item("menu.help");
      help.DropDownItems.Add(Item("menu.about", (s, e) => ShowAbout(), Keys.F1));
      help.DropDownItems.Add(Item("menu.open_repo", (s, e) => OpenUrl(AppInfo.SiteUrl)));

      menu.Items.AddRange(new ToolStripItem[] { file, run, tools, settings, help });
      Controls.Add(menu);
      MainMenuStrip = menu;
    }

    private void BuildStatusBar() {
      statusBar.RenderMode = ToolStripRenderMode.System;
      statusBar.SizingGrip = true;
      stateStatus.BorderSides = ToolStripStatusLabelBorderSides.Right;
      versionStatus.Text = "v" + AppInfo.Version;
      statusBar.Items.AddRange(new ToolStripItem[] { stateStatus, clientStatus, versionStatus });
      Controls.Add(statusBar);
    }

    private void ShowAbout() {
      MessageBox.Show(this, string.Format(T("dlg.about_text"), AppInfo.Name, AppInfo.Version, AppInfo.SiteUrl),
        T("menu.about").TrimEnd('…'), MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // ---- header actions ----

    private void OpenGuided() {
      using (var g = new GuideForm(engine, ApplyProfileToEngine)) g.ShowDialog(this);
      torrentBox.Text = string.IsNullOrEmpty(engine.TorrentDisplayName) ? T("no_torrent") : engine.TorrentDisplayName;
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

    // ---- action helpers ----

    private void OpenMagnet() {
      using (var p = new Prompt(T("dlg.magnet_title"), T("dlg.magnet_label"), "")) {
        if (p.ShowDialog(this) != DialogResult.OK) return;
        var uri = (p.Result ?? "").Trim();
        if (uri.Length == 0) return;
        try { engine.LoadMagnet(uri); torrentBox.Text = engine.TorrentDisplayName; }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
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
        if (prompt.ShowDialog(this) != DialogResult.OK) return;
        var text = (prompt.Result ?? "").Trim();
        if (text.Length == 0) { AppOptions.ActiveHoursEnabled = false; }
        else {
          var parts = text.Split('-');
          if (parts.Length != 2 || !int.TryParse(parts[0].Trim(), out var a) || !int.TryParse(parts[1].Trim(), out var b)
              || a < 0 || a > 24 || b < 0 || b > 24) {
            MessageBox.Show(this, T("dlg.hours_bad"), AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Information); return;
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

    /// <summary>Re-applies every registered caption/tooltip in the current language.</summary>
    private void ApplyLanguage() {
      foreach (var (c, k) in loc) c.Text = T(k);
      foreach (var (i, k) in menuLoc) i.Text = T(k);
      foreach (var (c, k) in tipReg) tips.SetToolTip(c, T(k));
      if (langEnItem != null) langEnItem.Checked = AppOptions.Language == Language.English;
      if (langFrItem != null) langFrItem.Checked = AppOptions.Language == Language.French;
      tray.Text = AppInfo.Title;
      // The torrent field shows a placeholder only when nothing is loaded.
      if (string.IsNullOrEmpty(engine.TorrentDisplayName)) torrentBox.Text = T("no_torrent");
      // Rebuild the mode combo (localized items), preserving the selection.
      var idx = modeBox.SelectedIndex;
      modeBox.Items.Clear();
      modeBox.Items.AddRange(new object[] { T("mode.seeder"), T("mode.leecher") });
      modeBox.SelectedIndex = idx < 0 ? 0 : idx;
      Refresh_(); // state labels (Seeding/Idle)
    }

    /// <summary>Applies a connection profile to the main engine (upload/download
    /// caps with a small jitter, plus the global upstream budget) and reflects the
    /// cap in the upload field.</summary>
    private void ApplyProfileToEngine(string name) {
      var prof = ApplyProfileTo(engine, name);
      if (prof != null) uploadBox.Text = prof.UpKBps.ToString();
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

    // ---- drag & drop: a .torrent dropped anywhere on the window loads it ----

    private void SetupDragDrop(Control root) {
      if (root is ToolStrip || root is RM) return; // the hidden engine keeps its own handlers
      root.AllowDrop = true;
      root.DragEnter += (s, e) => {
        if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
      };
      root.DragDrop += (s, e) => {
        if (!(e.Data?.GetData(DataFormats.FileDrop) is string[] files)) return;
        foreach (var f in files)
          if (f.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase)) { LoadTorrent(f); break; }
      };
      foreach (Control child in root.Controls) SetupDragDrop(child);
    }

    // ---- content layout ----

    private static Label FieldLabel(string text) => new Label {
      Text = text, AutoSize = true, Anchor = AnchorStyles.Left, TextAlign = ContentAlignment.MiddleLeft,
      Margin = new Padding(3, 0, 8, 0),
    };

    private void BuildContent() {
      // Log: fills whatever the fixed top area leaves (so it grows with the window).
      var logHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 2, 10, 8) };
      Reg(logGroup, "grp.log");
      logGroup.Dock = DockStyle.Fill;
      logGroup.Padding = new Padding(8, 4, 8, 8);
      log.Dock = DockStyle.Fill;
      log.ReadOnly = true;
      log.BackColor = SystemColors.Window;
      log.ForeColor = SystemColors.WindowText;
      log.Font = new Font("Consolas", 9f, FontStyle.Regular, GraphicsUnit.Point);
      log.WordWrap = true;
      log.ScrollBars = RichTextBoxScrollBars.Vertical;
      log.DetectUrls = false;
      logGroup.Controls.Add(log);
      logHost.Controls.Add(logGroup);
      Controls.Add(logHost);

      // Top: the two group boxes side by side, then the action buttons.
      var top = new TableLayoutPanel {
        Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
        ColumnCount = 2, RowCount = 2, Padding = new Padding(10, 8, 10, 0),
      };
      top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58f));
      top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42f));
      top.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      top.RowStyles.Add(new RowStyle(SizeType.AutoSize));

      top.Controls.Add(BuildTorrentGroup(), 0, 0);
      top.Controls.Add(BuildStatusGroup(), 1, 0);

      var buttons = new FlowLayoutPanel {
        AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.LeftToRight,
        WrapContents = false, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 0, 4),
      };
      Reg(startBtn, "start_seeding"); startBtn.Size = new Size(140, 30); startBtn.Margin = new Padding(3, 3, 6, 3);
      Reg(stopBtn, "stop"); stopBtn.Size = new Size(110, 30); stopBtn.Margin = new Padding(3, 3, 18, 3);
      Reg(testBtn, "btn.test"); testBtn.Size = new Size(140, 30);
      startBtn.Click += (s, e) => Start();
      stopBtn.Click += (s, e) => engine.CampaignStop();
      testBtn.Click += (s, e) => engine.TestAnnounce();
      RegTip(testBtn, "menu.test_announce");
      buttons.Controls.Add(startBtn); buttons.Controls.Add(stopBtn); buttons.Controls.Add(testBtn);
      top.Controls.Add(buttons, 0, 1);
      top.SetColumnSpan(buttons, 2);

      Controls.Add(top);
      AcceptButton = startBtn;
    }

    private GroupBox BuildTorrentGroup() {
      Reg(torrentGroup, "grp.torrent");
      torrentGroup.Dock = DockStyle.Fill;
      torrentGroup.AutoSize = true;
      torrentGroup.Margin = new Padding(0, 0, 6, 0);
      torrentGroup.Padding = new Padding(8, 4, 8, 8);

      var grid = new TableLayoutPanel {
        Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
        ColumnCount = 3, RowCount = 4,
      };
      grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
      grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
      grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
      for (var i = 0; i < 4; i++) grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

      // File
      grid.Controls.Add(Reg(FieldLabel(""), "lbl.file"), 0, 0);
      torrentBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
      torrentBox.Text = T("no_torrent");
      RegTip(torrentBox, "tip.drop");
      grid.Controls.Add(torrentBox, 1, 0);
      Reg(browseBtn, "browse"); browseBtn.Size = new Size(100, 26); browseBtn.Anchor = AnchorStyles.Left;
      browseBtn.Click += (s, e) => Browse();
      grid.Controls.Add(browseBtn, 2, 0);

      // Client + version
      grid.Controls.Add(Reg(FieldLabel(""), "lbl.client"), 0, 1);
      var clientRow = new TableLayoutPanel {
        AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Anchor = AnchorStyles.Left | AnchorStyles.Right,
        ColumnCount = 3, RowCount = 1, Margin = new Padding(0),
      };
      clientRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
      clientRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
      clientRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120f));
      clientRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));
      familyBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
      versionBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
      RegTip(familyBox, "tip.client");
      clientRow.Controls.Add(familyBox, 0, 0);
      clientRow.Controls.Add(Reg(FieldLabel(""), "lbl.version"), 1, 0);
      clientRow.Controls.Add(versionBox, 2, 0);
      grid.Controls.Add(clientRow, 1, 1);
      Reg(advancedBtn, "advanced"); advancedBtn.Size = new Size(100, 26); advancedBtn.Anchor = AnchorStyles.Left;
      advancedBtn.Click += (s, e) => engine.ShowAdvanced();
      RegTip(advancedBtn, "tip.advanced");
      grid.Controls.Add(advancedBtn, 2, 1);

      // Mode
      grid.Controls.Add(Reg(FieldLabel(""), "lbl.mode"), 0, 2);
      modeBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
      grid.Controls.Add(modeBox, 1, 2);

      // Upload speed
      grid.Controls.Add(Reg(FieldLabel(""), "lbl.upload"), 0, 3);
      uploadBox.Width = 110; uploadBox.Anchor = AnchorStyles.Left;
      RegTip(uploadBox, "tip.upload");
      grid.Controls.Add(uploadBox, 1, 3);

      torrentGroup.Controls.Add(grid);
      return torrentGroup;
    }

    private GroupBox BuildStatusGroup() {
      Reg(statusGroup, "grp.status");
      statusGroup.Dock = DockStyle.Fill;
      statusGroup.AutoSize = true;
      statusGroup.Margin = new Padding(6, 0, 0, 0);
      statusGroup.Padding = new Padding(8, 4, 8, 8);

      var grid = new TableLayoutPanel {
        Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
        ColumnCount = 2, RowCount = 7,
      };
      grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
      grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
      for (var i = 0; i < 7; i++) grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

      var ratioLabel = Reg(FieldLabel(""), "lbl.ratio");
      ratioValue.Font = new Font("Segoe UI", 14f, FontStyle.Bold, GraphicsUnit.Point);
      ratioValue.AutoSize = true; ratioValue.Anchor = AnchorStyles.Left; ratioValue.Text = "—";
      ratioValue.Margin = new Padding(3, 0, 3, 2);
      RegTip(ratioLabel, "tip.ratio"); RegTip(ratioValue, "tip.ratio");
      grid.Controls.Add(ratioLabel, 0, 0);
      grid.Controls.Add(ratioValue, 1, 0);

      AddStatusRow(grid, 1, "lbl.uploaded", upValue);
      AddStatusRow(grid, 2, "lbl.downloaded", downValue);
      AddStatusRow(grid, 3, "lbl.up_speed", speedValue);
      AddStatusRow(grid, 4, "lbl.swarm", swarmValue);
      AddStatusRow(grid, 5, "lbl.elapsed", elapsedValue);
      AddStatusRow(grid, 6, "lbl.state", stateValue);

      statusGroup.Controls.Add(grid);
      return statusGroup;
    }

    private void AddStatusRow(TableLayoutPanel grid, int row, string labelKey, Label value) {
      grid.Controls.Add(Reg(FieldLabel(""), labelKey), 0, row);
      value.AutoSize = true; value.Anchor = AnchorStyles.Left; value.TextAlign = ContentAlignment.MiddleLeft;
      value.Font = new Font("Segoe UI", 9f, FontStyle.Bold, GraphicsUnit.Point);
      value.Margin = new Padding(3, 3, 3, 3);
      value.Text = "–";
      grid.Controls.Add(value, 1, row);
    }
  }
}
