using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Seedforger.BitTorrent;

namespace Seedforger.UI {

  /// <summary>
  /// The main window, in a classic Windows layout: a menu bar, labelled group
  /// boxes with standard controls in the system font and colours, a log, and a
  /// status bar. Laid out with table layouts so it scales with the font and DPI.
  ///
  /// It is a thin front-end over the same <see cref="SeedEngine"/> the CLI and
  /// the cross-platform GUI use: the form holds the user's choices, builds one
  /// engine per run, polls its live read-outs twice a second and shows its log.
  /// </summary>
  internal sealed class MainForm : Form, IMainHost {

    private const int PollMilliseconds = 500;

    // ---- state ----
    private Torrent torrent;
    private SeedEngine engine;
    private CampaignEngine campaign;
    private readonly RunPreferences prefs = RunPreferences.FromSettings(Settings.Current);
    private string realFile;
    private bool busy; // a Start/Stop is in flight on the thread pool

    private readonly Timer poll = new Timer { Interval = PollMilliseconds };
    private readonly ToolTip tips = new ToolTip { AutoPopDelay = 15000, InitialDelay = 400, ReshowDelay = 100 };

    // ---- menu bar ----
    private readonly MenuStrip menu = new MenuStrip();
    private ToolStripMenuItem startItem, stopItem, announceItem, stopCampaignItem, langEnItem, langFrItem;

    // ---- torrent group ----
    private readonly GroupBox torrentGroup = new GroupBox();
    private readonly TextBox torrentBox = new TextBox { ReadOnly = true };
    private readonly Button browseBtn = new Button();
    private readonly ComboBox familyBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox versionBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Button advancedBtn = new Button();
    private readonly ComboBox modeBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox uploadBox = new TextBox { TextAlign = HorizontalAlignment.Right };
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
    private readonly ToolStripStatusLabel nextStatus = new ToolStripStatusLabel();
    private readonly ToolStripStatusLabel versionStatus = new ToolStripStatusLabel();

    private static readonly Color RunningGreen = Color.FromArgb(0x1B, 0x7F, 0x3B);
    private static readonly Color RejectedRed = Color.FromArgb(0xB4, 0x23, 0x18);

    // Live up-speed (bytes/s) is derived from the poll delta.
    private long prevUpBytes;
    private DateTime prevTick = DateTime.UtcNow;

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
      AutoScaleMode = AutoScaleMode.Font;
      AutoScaleDimensions = new SizeF(7F, 15F);
      Font = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
      ClientSize = new Size(860, 620);
      MinimumSize = new Size(800, 580);
      StartPosition = FormStartPosition.CenterScreen;
      try { Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath); } catch (Exception) { }

      LoadPersistedSettings();
      SuspendLayout();
      BuildContent();   // Fill — added first so the docked bars win the edges
      BuildMenu();
      BuildStatusBar();
      ResumeLayout(true);
      FillClientPickers();
      SetupTray();
      SetupDragDrop(this);

      poll.Tick += (s, e) => Refresh_();
      poll.Start();
      Refresh_();
      StartUpdateCheck();
    }

    // ---- settings in and out ----

    private static void LoadPersistedSettings() {
      var s = Settings.Current;
      StealthOptions.Shared.LoadFrom(s);
      UpstreamBudget.Shared.TotalKBps = s.GlobalUpstreamKBps;
      UiStrings.Language = UiStrings.ParseLanguage(s.Language);
    }

    private void FillClientPickers() {
      foreach (var f in TorrentClientFactory.GetFamilies()) familyBox.Items.Add(f);
      var s = Settings.Current;
      var fam = familyBox.Items.IndexOf(s.Client);
      familyBox.SelectedIndex = fam >= 0 ? fam : 0;
      RefreshVersions();
      var ver = versionBox.Items.IndexOf(s.ClientVersion);
      if (ver >= 0) versionBox.SelectedIndex = ver;
      familyBox.SelectedIndexChanged += (o, e) => { RefreshVersions(); RememberClient(); };
      versionBox.SelectedIndexChanged += (o, e) => RememberClient();
      modeBox.Items.AddRange(new object[] { T("mode.seeder"), T("mode.leecher") });
      modeBox.SelectedIndex = 0;
      uploadBox.Text = s.UploadRate.ToString();
    }

    private void RememberClient() => Settings.Current.Update(s => {
      s.Client = familyBox.SelectedItem?.ToString() ?? s.Client;
      s.ClientVersion = versionBox.SelectedItem?.ToString() ?? s.ClientVersion;
    });

    private void RefreshVersions() {
      versionBox.Items.Clear();
      var fam = familyBox.SelectedItem?.ToString();
      if (fam == null) return;
      foreach (var v in TorrentClientFactory.GetVersions(fam)) versionBox.Items.Add(v);
      if (versionBox.Items.Count > 0) versionBox.SelectedIndex = 0;
    }

    private TorrentClient SelectedClient() =>
      TorrentClientFactory.Resolve(familyBox.SelectedItem?.ToString(), versionBox.SelectedItem?.ToString());

    // Silent update-check at launch: only prompts if a newer release exists.
    private void StartUpdateCheck() {
      UpdateChecker.CheckInBackground((tag, url) => {
        try {
          BeginInvoke((Action) (() => {
            var r = MessageBox.Show(this, string.Format(T("dlg.update_text"), tag, AppInfo.Version),
              AppInfo.Name + " — " + T("dlg.update_title"), MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (r == DialogResult.Yes) OpenUrl(url);
          }));
        }
        catch (Exception) { /* form gone */ }
      });
    }

    // ---- IMainHost (the guided setup drives the window through this) ----

    public Torrent CurrentTorrent => torrent;

    public string TorrentDisplayName => torrent?.Name ?? "";

    public bool LoadTorrent(string path) {
      try {
        torrent = new Torrent(path);
        torrentBox.Text = torrent.Name;
        AppendLog(string.Format(T("log.loaded"), torrent.Name, Format.Bytes((long) torrent.totalLength), torrent.AnnounceList?.Count ?? 1));
        return true;
      }
      catch (Exception ex) {
        MessageBox.Show(this, T("dlg.read_error") + ex.Message, AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
      }
    }

    public AnnounceProbe Probe() => AnnounceProbe.Run(torrent, SelectedClient(), prefs.Proxy, LogFromAnyThread);

    public void StartSeedingSafely() {
      modeBox.SelectedIndex = 0;
      StealthOptions.Shared.RealisticSpeed = true;
      StealthOptions.Shared.SwarmAware = true;
      StealthOptions.Shared.SaveTo(Settings.Current);
      Settings.Current.Save();
      Start();
    }

    /// <summary>Applies a connection profile: the upload field, the shared upstream
    /// budget and the persisted value.</summary>
    public void ApplyConnectionProfile(string name) {
      var prof = ConnectionProfiles.Find(name);
      if (prof == null) return;
      uploadBox.Text = prof.UpKBps.ToString();
      prefs.LeechDownloadKBps = prof.DownKBps;
      UpstreamBudget.Shared.TotalKBps = prof.UpKBps;
      Settings.Current.Update(s => { s.GlobalUpstreamKBps = prof.UpKBps; s.UploadRate = prof.UpKBps; prefs.SaveTo(s); });
      AppendLog(string.Format(T("log.profile"), prof.Name, prof.UpKBps, prof.DownKBps));
    }

    // ---- the run ----

    private SeedOptions BuildOptions() {
      int.TryParse(uploadBox.Text.Trim(), out var up);
      var seeder = modeBox.SelectedIndex == 0;
      var o = new SeedOptions {
        Torrent = torrent,
        Client = Settings.Current.RandomizeClientOnStart ? TorrentClientFactory.PickRandomModern(null) : SelectedClient(),
        UploadKBps = up,
        DownloadKBps = seeder ? 0 : prefs.LeechDownloadKBps,
        FinishedPercent = seeder ? 100 : 0,
        RealFile = realFile,
        Stealth = StealthOptions.Shared,
        Budget = UpstreamBudget.Shared,
        Log = LogFromAnyThread,
      };
      prefs.ApplyTo(o);
      return o;
    }

    private void Start() {
      if (busy || (engine != null && engine.IsRunning)) return;
      if (torrent == null) {
        MessageBox.Show(this, T("dlg.no_torrent"), AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
        return;
      }
      if (!int.TryParse(uploadBox.Text.Trim(), out var up) || up <= 0) {
        MessageBox.Show(this, T("dlg.upload_bad"), AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
        uploadBox.Focus(); uploadBox.SelectAll();
        return;
      }
      Settings.Current.Update(s => s.UploadRate = up);

      SeedOptions o;
      try { o = BuildOptions(); }
      catch (Exception ex) { MessageBox.Show(this, ex.Message, AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

      engine?.Dispose();
      engine = new SeedEngine(o);
      engine.AutoStopped += (e, reason) => LogFromAnyThread(T("log.auto_stopped") + " " + reason);
      foreach (var w in Stealth.BelievabilityWarnings(o.UploadKBps, o.DownloadKBps)) AppendLog("⚠ " + w);
      AppendLog(string.Format(T("log.starting"), torrent.Name, o.Client.Name, o.UploadKBps, o.Proxy));
      prevUpBytes = 0;
      RunOffUi(engine.StartAsync);
    }

    private void StopRun() {
      var e = engine;
      if (e == null || !e.IsRunning || busy) return;
      RunOffUi(e.StopAsync);
    }

    /// <summary>Runs a blocking engine call on the thread pool, keeping the buttons
    /// consistent meanwhile and reporting a failure in the log.</summary>
    private void RunOffUi(Func<Task> work) {
      busy = true;
      Refresh_();
      Task.Run(work).ContinueWith(t => {
        if (t.IsFaulted) LogFromAnyThread("error: " + (t.Exception?.GetBaseException().Message ?? "unknown"));
        try { BeginInvoke((Action) (() => { busy = false; Refresh_(); })); } catch (Exception) { }
      });
    }

    private void TestAnnounce() {
      if (torrent == null) {
        MessageBox.Show(this, T("dlg.no_torrent"), AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
        return;
      }
      testBtn.Enabled = false;
      AppendLog(T("log.probing"));
      var t = torrent; var client = SelectedClient(); var proxy = prefs.Proxy;
      Task.Run(() => AnnounceProbe.Run(t, client, proxy, LogFromAnyThread)).ContinueWith(task => {
        try {
          BeginInvoke((Action) (() => {
            testBtn.Enabled = true;
            var p = task.IsFaulted ? null : task.Result;
            var text = p == null ? "error" : DescribeProbe(p);
            AppendLog(text);
            MessageBox.Show(this, text, T("btn.test"), MessageBoxButtons.OK,
              p != null && p.Accepted ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
          }));
        }
        catch (Exception) { }
      });
    }

    internal static string DescribeProbe(AnnounceProbe p) {
      if (!string.IsNullOrEmpty(p.Error)) return T("probe.error") + " " + p.Error;
      if (!string.IsNullOrEmpty(p.FailureReason)) return T("probe.rejected") + " " + p.FailureReason;
      var swarm = string.Format(T("probe.swarm"), p.Seeders, p.Leechers, p.Interval);
      return (p.Leechers > 0 ? T("probe.accepted") : T("probe.accepted_empty")) + "\n" + swarm;
    }

    // ---- live read-outs ----

    private void Refresh_() {
      var e = engine;
      var running = e != null && e.IsRunning;
      startBtn.Enabled = !running && !busy;
      stopBtn.Enabled = running && !busy;
      if (startItem != null) startItem.Enabled = startBtn.Enabled;
      if (stopItem != null) stopItem.Enabled = stopBtn.Enabled;
      if (announceItem != null) announceItem.Enabled = running;
      if (stopCampaignItem != null) stopCampaignItem.Enabled = campaign != null && campaign.Running;

      var up = e?.UploadedBytes ?? 0;
      var down = e?.DownloadedBytes ?? 0;
      ratioValue.Text = Format.Ratio(up, down);
      upValue.Text = Format.Bytes(up);
      downValue.Text = Format.Bytes(down);

      var now = DateTime.UtcNow;
      var dt = (now - prevTick).TotalSeconds;
      if (running && dt > 0 && up >= prevUpBytes) speedValue.Text = Format.Rate((long) ((up - prevUpBytes) / dt));
      else if (!running) speedValue.Text = "–";
      prevUpBytes = up; prevTick = now;

      elapsedValue.Text = e != null && e.State != SeedState.Idle ? Format.Duration(e.Elapsed) : "–";
      var seed = e?.SeederCount ?? -1; var leech = e?.LeecherCount ?? -1;
      swarmValue.Text = (seed < 0 ? "–" : seed.ToString()) + " / " + (leech < 0 ? "–" : leech.ToString());

      string state; Color colour;
      if (busy) { state = T("state.busy"); colour = SystemColors.GrayText; }
      else if (running && e.Rejected) { state = T("state.rejected"); colour = RejectedRed; }
      else if (running) { state = T("seeding"); colour = RunningGreen; }
      else { state = T("idle"); colour = SystemColors.ControlText; }
      stateValue.Text = state; stateValue.ForeColor = colour;
      stateStatus.Text = state;

      var client = running ? e.ClientName : ((familyBox.SelectedItem?.ToString() ?? "") + " " + (versionBox.SelectedItem?.ToString() ?? "")).Trim();
      clientStatus.Text = string.Format(T("status.client"), client) + (prefs.Proxy.Enabled ? "   ·   " + prefs.Proxy : "");
      nextStatus.Text = running ? string.Format(T("status.next"), Format.Duration(e.UntilNextAnnounce)) : "";
      if (campaign != null && campaign.Running)
        nextStatus.Text = string.Format(T("status.campaign"), campaign.ActiveCount, campaign.TorrentCount, Format.Bytes(campaign.TotalUploaded));
    }

    // ---- logging ----

    private void LogFromAnyThread(string line) {
      if (IsDisposed) return;
      if (InvokeRequired) { try { BeginInvoke((Action) (() => AppendLog(line))); } catch (Exception) { } }
      else AppendLog(line);
    }

    private void AppendLog(string line) {
      if (string.IsNullOrEmpty(line)) return;
      log.SelectionStart = log.TextLength;
      log.AppendText("[" + DateTime.Now.ToString("T") + "] " + line.TrimEnd() + "\n");
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
      it.Click += (s, e) => onToggle(it.Checked); // CheckOnClick flips Checked before Click
      return it;
    }

    private void BuildMenu() {
      menu.RenderMode = ToolStripRenderMode.System;
      menu.Dock = DockStyle.Top;

      var file = Item("menu.file");
      file.DropDownItems.Add(Item("menu.load_torrent", (s, e) => Browse(), Keys.Control | Keys.O));
      file.DropDownItems.Add(Item("menu.open_magnet", (s, e) => OpenMagnet(), Keys.Control | Keys.M));
      file.DropDownItems.Add(new ToolStripSeparator());
      file.DropDownItems.Add(Item("menu.exit", (s, e) => { reallyExit = true; Close(); }));

      var run = Item("menu.run");
      startItem = Item("start_seeding", (s, e) => Start(), Keys.F5);
      stopItem = Item("stop", (s, e) => StopRun(), Keys.F6);
      announceItem = Item("menu.announce_now", (s, e) => engine?.AnnounceNow(), Keys.F8);
      run.DropDownItems.Add(startItem);
      run.DropDownItems.Add(stopItem);
      run.DropDownItems.Add(announceItem);
      run.DropDownItems.Add(new ToolStripSeparator());
      run.DropDownItems.Add(Item("menu.test_announce", (s, e) => TestAnnounce(), Keys.F7));
      run.DropDownItems.Add(Item("menu.serve_real", (s, e) => ServeReal()));

      var tools = Item("menu.tools");
      tools.DropDownItems.Add(Item("menu.guided", (s, e) => OpenGuided()));
      tools.DropDownItems.Add(Item("menu.campaigns", (s, e) => OpenCampaigns()));
      stopCampaignItem = Item("menu.stop_campaign", (s, e) => StopCampaign());
      tools.DropDownItems.Add(stopCampaignItem);
      tools.DropDownItems.Add(Item("menu.live_graph", (s, e) => ShowGraph()));
      tools.DropDownItems.Add(new ToolStripSeparator());
      tools.DropDownItems.Add(Item("menu.advanced_settings", (s, e) => OpenAdvanced()));

      var settings = Item("menu.settings");
      var stealth = StealthOptions.Shared;
      settings.DropDownItems.Add(Check("menu.realistic", stealth.RealisticSpeed, v => { stealth.RealisticSpeed = v; SaveStealth(); }));
      settings.DropDownItems.Add(Check("menu.swarm", stealth.SwarmAware, v => { stealth.SwarmAware = v; SaveStealth(); }));
      settings.DropDownItems.Add(Check("menu.randomize", Settings.Current.RandomizeClientOnStart,
        v => Settings.Current.Update(s => s.RandomizeClientOnStart = v)));
      settings.DropDownItems.Add(new ToolStripSeparator());
      var conn = Item("menu.connection");
      foreach (var p in ConnectionProfiles.All) {
        var name = p.Name;
        conn.DropDownItems.Add(new ToolStripMenuItem(name, null, (s, e) => ApplyConnectionProfile(name)));
      }
      settings.DropDownItems.Add(conn);
      settings.DropDownItems.Add(Item("menu.active_hours", (s, e) => SetActiveHours()));
      settings.DropDownItems.Add(new ToolStripSeparator());
      settings.DropDownItems.Add(Check("menu.minimize_tray", Settings.Current.MinimizeToTray, v => Settings.Current.Update(s => s.MinimizeToTray = v)));
      settings.DropDownItems.Add(Check("menu.close_minimizes", Settings.Current.CloseToTray, v => Settings.Current.Update(s => s.CloseToTray = v)));
      settings.DropDownItems.Add(Check("menu.tray_balloon", Settings.Current.BalloonTip, v => Settings.Current.Update(s => s.BalloonTip = v)));
      settings.DropDownItems.Add(new ToolStripSeparator());
      var lang = Item("menu.language");
      langEnItem = new ToolStripMenuItem("English", null, (s, e) => SetLanguage(Language.English)) { Checked = !UiStrings.IsFrench };
      langFrItem = new ToolStripMenuItem("Français", null, (s, e) => SetLanguage(Language.French)) { Checked = UiStrings.IsFrench };
      lang.DropDownItems.Add(langEnItem); lang.DropDownItems.Add(langFrItem);
      settings.DropDownItems.Add(lang);

      var help = Item("menu.help");
      help.DropDownItems.Add(Item("menu.about", (s, e) => ShowAbout(), Keys.F1));
      help.DropDownItems.Add(Item("menu.open_repo", (s, e) => OpenUrl(AppInfo.SiteUrl)));

      menu.Items.AddRange(new ToolStripItem[] { file, run, tools, settings, help });
      Controls.Add(menu);
      MainMenuStrip = menu;
    }

    private static void SaveStealth() => Settings.Current.Update(s => StealthOptions.Shared.SaveTo(s));

    private void BuildStatusBar() {
      statusBar.RenderMode = ToolStripRenderMode.System;
      statusBar.SizingGrip = true;
      stateStatus.BorderSides = ToolStripStatusLabelBorderSides.Right;
      nextStatus.BorderSides = ToolStripStatusLabelBorderSides.Left;
      versionStatus.Text = "v" + AppInfo.Version;
      statusBar.Items.AddRange(new ToolStripItem[] { stateStatus, clientStatus, nextStatus, versionStatus });
      Controls.Add(statusBar);
    }

    private void ShowAbout() =>
      MessageBox.Show(this, string.Format(T("dlg.about_text"), AppInfo.Name, AppInfo.Version, AppInfo.SiteUrl),
        T("menu.about").TrimEnd('…'), MessageBoxButtons.OK, MessageBoxIcon.Information);

    // ---- actions ----

    private void Browse() {
      using var dlg = new OpenFileDialog { Filter = T("dlg.torrent_filter"), Title = T("dlg.choose_torrent") };
      if (dlg.ShowDialog(this) == DialogResult.OK) LoadTorrent(dlg.FileName);
    }

    private void OpenMagnet() {
      using var p = new Prompt(T("dlg.magnet_title"), T("dlg.magnet_label"), "");
      if (p.ShowDialog(this) != DialogResult.OK) return;
      var info = Magnet.Parse((p.Result ?? "").Trim());
      if (info == null) { MessageBox.Show(this, T("dlg.magnet_bad"), AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
      using var sizePrompt = new Prompt(T("dlg.magnet_title"), T("dlg.magnet_size"), "700");
      if (sizePrompt.ShowDialog(this) != DialogResult.OK) return;
      if (!double.TryParse((sizePrompt.Result ?? "").Trim().Replace(',', '.'), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var mb) || mb <= 0) {
        MessageBox.Show(this, T("dlg.magnet_size_bad"), AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning); return;
      }
      try {
        torrent = Torrent.FromMagnet(info, (ulong) (mb * 1024 * 1024));
        torrentBox.Text = torrent.Name;
        AppendLog(string.Format(T("log.loaded"), torrent.Name, Format.Bytes((long) torrent.totalLength), 1));
      }
      catch (Exception ex) { MessageBox.Show(this, ex.Message, AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private void ServeReal() {
      using var dlg = new OpenFileDialog { Title = T("dlg.serve_title") };
      if (dlg.ShowDialog(this) != DialogResult.OK) return;
      realFile = dlg.FileName;
      AppendLog(string.Format(T("log.real_file"), realFile));
    }

    private void OpenGuided() {
      using (var g = new GuideForm(this)) g.ShowDialog(this);
      torrentBox.Text = torrent == null ? T("no_torrent") : torrent.Name;
    }

    private void OpenAdvanced() {
      using var dlg = new AdvancedForm(prefs);
      if (dlg.ShowDialog(this) != DialogResult.OK) return;
      Settings.Current.Update(s => prefs.SaveTo(s));
      Refresh_();
    }

    private void OpenCampaigns() {
      using var wizard = new CampaignForm();
      if (wizard.ShowDialog(this) != DialogResult.OK || wizard.Result == null) return;
      campaign?.Stop();
      campaign = new CampaignEngine(wizard.Result, line => LogFromAnyThread("[campaign] " + line), prefs.Proxy, StealthOptions.Shared);
      campaign.Completed += done => LogFromAnyThread("[campaign] " + T("log.campaign_done"));
      var started = campaign;
      Task.Run(() => started.Start());
    }

    private void StopCampaign() { campaign?.Stop(); Refresh_(); }

    private void ShowGraph() {
      if (graphForm == null || graphForm.IsDisposed) { graphForm = new GraphForm(() => engine); graphForm.Show(this); }
      else graphForm.Activate();
    }

    private void SetActiveHours() {
      var st = StealthOptions.Shared;
      var current = st.ActiveHoursEnabled ? $"{st.ActiveHoursStart}-{st.ActiveHoursEnd}" : "";
      using var prompt = new Prompt(T("dlg.hours_title"), T("dlg.hours_label"), current);
      if (prompt.ShowDialog(this) != DialogResult.OK) return;
      var text = (prompt.Result ?? "").Trim();
      if (text.Length == 0) st.ActiveHoursEnabled = false;
      else {
        var parts = text.Split('-');
        if (parts.Length != 2 || !int.TryParse(parts[0].Trim(), out var a) || !int.TryParse(parts[1].Trim(), out var b)
            || a < 0 || a > 24 || b < 0 || b > 24) {
          MessageBox.Show(this, T("dlg.hours_bad"), AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Information); return;
        }
        st.ActiveHoursEnabled = true; st.ActiveHoursStart = a; st.ActiveHoursEnd = b;
      }
      SaveStealth();
    }

    private void SetLanguage(Language lang) {
      UiStrings.Language = lang;
      Settings.Current.Update(s => s.Language = UiStrings.Code(lang));
      ApplyLanguage();
    }

    /// <summary>Re-applies every registered caption/tooltip in the current language.</summary>
    private void ApplyLanguage() {
      foreach (var (c, k) in loc) c.Text = T(k);
      foreach (var (i, k) in menuLoc) i.Text = T(k);
      foreach (var (c, k) in tipReg) tips.SetToolTip(c, T(k));
      if (langEnItem != null) langEnItem.Checked = !UiStrings.IsFrench;
      if (langFrItem != null) langFrItem.Checked = UiStrings.IsFrench;
      tray.Text = AppInfo.Title;
      if (torrent == null) torrentBox.Text = T("no_torrent");
      var idx = modeBox.SelectedIndex;
      modeBox.Items.Clear();
      modeBox.Items.AddRange(new object[] { T("mode.seeder"), T("mode.leecher") });
      modeBox.SelectedIndex = idx < 0 ? 0 : idx;
      Refresh_();
    }

    private static void OpenUrl(string url) {
      try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); } catch (Exception) { }
    }

    // ---- window behaviour: tray on minimize, taskbar on close ----

    private void SetupTray() {
      tray.Icon = Icon ?? SystemIcons.Application;
      tray.Text = AppInfo.Title;
      var trayMenu = new ContextMenuStrip();
      var restore = new ToolStripMenuItem(T("tray.restore"), null, (s, e) => RestoreFromTray());
      var quit = new ToolStripMenuItem(T("tray.quit"), null, (s, e) => { reallyExit = true; Close(); });
      trayMenu.Opening += (s, e) => { restore.Text = T("tray.restore"); quit.Text = T("tray.quit"); };
      trayMenu.Items.Add(restore); trayMenu.Items.Add(quit);
      tray.ContextMenuStrip = trayMenu;
      tray.DoubleClick += (s, e) => RestoreFromTray();

      Resize += (s, e) => {
        if (WindowState == FormWindowState.Minimized && Settings.Current.MinimizeToTray && !minimizingFromClose) HideToTray();
      };
      FormClosing += (s, e) => {
        // A user-initiated close shouldn't kill a run in progress: it minimizes to
        // the taskbar instead. Quit for real from File → Exit or the tray icon.
        var systemClose = e.CloseReason == CloseReason.WindowsShutDown
          || e.CloseReason == CloseReason.TaskManagerClosing
          || e.CloseReason == CloseReason.ApplicationExitCall;
        if (!reallyExit && !systemClose && Settings.Current.CloseToTray) {
          e.Cancel = true;
          try { MinimizeToTaskbar(); } catch (Exception) { }
          return;
        }
        poll.Stop();
        try { campaign?.Stop(); } catch (Exception) { }
        try { engine?.Stop(); } catch (Exception) { }
        tray.Visible = false;
      };
    }

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
      if (!trayHintShown || Settings.Current.BalloonTip) {
        trayHintShown = true;
        try { tray.ShowBalloonTip(4000, AppInfo.Name, T("tray.balloon_text"), ToolTipIcon.Info); } catch (Exception) { }
      }
    }

    private void RestoreFromTray() {
      Show();
      WindowState = FormWindowState.Normal;
      tray.Visible = false;
      Activate();
    }

    // ---- drag & drop: a .torrent dropped anywhere on the window loads it ----

    private void SetupDragDrop(Control root) {
      if (root is ToolStrip) return;
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

    private static Label FieldLabel() => new Label {
      AutoSize = true, Anchor = AnchorStyles.Left, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(3, 0, 8, 0),
    };

    private void BuildContent() {
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

      var top = new TableLayoutPanel {
        Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
        ColumnCount = 2, RowCount = 2, Padding = new Padding(10, 8, 10, 0),
      };
      top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62f));
      top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38f));
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
      stopBtn.Click += (s, e) => StopRun();
      testBtn.Click += (s, e) => TestAnnounce();
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
        Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 3, RowCount = 5,
      };
      grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
      grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
      grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
      for (var i = 0; i < 5; i++) grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

      grid.Controls.Add(Reg(FieldLabel(), "lbl.file"), 0, 0);
      torrentBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
      torrentBox.Text = T("no_torrent");
      RegTip(torrentBox, "tip.drop");
      grid.Controls.Add(torrentBox, 1, 0);
      Reg(browseBtn, "browse"); browseBtn.Size = new Size(100, 26); browseBtn.Anchor = AnchorStyles.Left;
      browseBtn.Click += (s, e) => Browse();
      grid.Controls.Add(browseBtn, 2, 0);

      grid.Controls.Add(Reg(FieldLabel(), "lbl.client"), 0, 1);
      familyBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
      RegTip(familyBox, "tip.client");
      grid.Controls.Add(familyBox, 1, 1);
      Reg(advancedBtn, "advanced"); advancedBtn.Size = new Size(100, 26); advancedBtn.Anchor = AnchorStyles.Left;
      advancedBtn.Click += (s, e) => OpenAdvanced();
      RegTip(advancedBtn, "tip.advanced");
      grid.Controls.Add(advancedBtn, 2, 1);

      grid.Controls.Add(Reg(FieldLabel(), "lbl.version"), 0, 2);
      versionBox.Width = 180; versionBox.Anchor = AnchorStyles.Left;
      grid.Controls.Add(versionBox, 1, 2);

      grid.Controls.Add(Reg(FieldLabel(), "lbl.mode"), 0, 3);
      modeBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
      grid.Controls.Add(modeBox, 1, 3);

      grid.Controls.Add(Reg(FieldLabel(), "lbl.upload"), 0, 4);
      uploadBox.Width = 110; uploadBox.Anchor = AnchorStyles.Left;
      RegTip(uploadBox, "tip.upload");
      grid.Controls.Add(uploadBox, 1, 4);

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
        Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2, RowCount = 7,
      };
      grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
      grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
      for (var i = 0; i < 7; i++) grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

      var ratioLabel = Reg(FieldLabel(), "lbl.ratio");
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
      grid.Controls.Add(Reg(FieldLabel(), labelKey), 0, row);
      value.AutoSize = true; value.Anchor = AnchorStyles.Left; value.TextAlign = ContentAlignment.MiddleLeft;
      value.Font = new Font("Segoe UI", 9f, FontStyle.Bold, GraphicsUnit.Point);
      value.Margin = new Padding(3, 3, 3, 3);
      value.Text = "–";
      grid.Controls.Add(value, 1, row);
    }
  }
}
