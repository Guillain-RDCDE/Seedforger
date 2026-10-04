using System;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Threading;
using Seedforger;
using Seedforger.BitTorrent;
using Seedforger.UI;

namespace Seedforger.App.ViewModels {

  /// <summary>
  /// Drives the shared <see cref="SeedEngine"/> from the Avalonia UI: holds the
  /// user's choices, starts/stops one engine per run, and polls its live
  /// read-outs. The same engine as the Windows window, the CLI and the daemon.
  /// </summary>
  public sealed class MainViewModel : ViewModelBase {

    private const int PollMilliseconds = 500;

    public LocProxy L { get; } = new LocProxy();

    private readonly DispatcherTimer poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(PollMilliseconds) };
    private readonly StringBuilder logBuffer = new StringBuilder();
    private Torrent torrent;
    private SeedEngine engine;
    private CampaignEngine campaignEngine;
    private bool busy;

    private static readonly IBrush IdleBrush = new SolidColorBrush(Color.Parse("#6B7280"));
    private static readonly IBrush RunningBrush = new SolidColorBrush(Color.Parse("#15803D"));
    private static readonly IBrush RejectedBrush = new SolidColorBrush(Color.Parse("#B42318"));

    public ObservableCollection<string> Families { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> Versions { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> Modes { get; } = new ObservableCollection<string>();

    public RelayCommand StartCommand { get; }
    public RelayCommand StopCommand { get; }

    /// <summary>Proxy, fingerprint overrides, interval, stop rule (the Advanced dialog).</summary>
    internal RunPreferences Preferences { get; private set; }

    /// <summary>Optional real file to serve genuine pieces from (Run → Serve a real file).</summary>
    internal string RealSeedFile { get; set; }

    public MainViewModel() {
      Encodings.Register();
      var s = Settings.Current;
      StealthOptions.Shared.LoadFrom(s);
      UpstreamBudget.Shared.TotalKBps = s.GlobalUpstreamKBps;
      UiStrings.Language = UiStrings.ParseLanguage(s.Language);
      Preferences = RunPreferences.FromSettings(s);

      foreach (var f in TorrentClientFactory.GetFamilies()) Families.Add(f);
      selectedFamily = Families.Contains(s.Client) ? s.Client : Families.Count > 0 ? Families[0] : null;
      RefreshVersions();
      if (Versions.Contains(s.ClientVersion)) selectedVersion = s.ClientVersion;
      uploadText = s.UploadRate.ToString();
      RebuildModes();

      StartCommand = new RelayCommand(Start, () => !IsRunning && !busy && torrent != null);
      StopCommand = new RelayCommand(Stop, () => IsRunning && !busy);

      poll.Tick += (o, e) => RefreshLive();
      poll.Start();
      TorrentDisplay = UiStrings.Get("no_torrent");
      RefreshLive(force: true);
    }

    // ---- bindable state ----

    private string torrentDisplay;
    public string TorrentDisplay { get => torrentDisplay; set => Set(ref torrentDisplay, value); }

    private string selectedFamily;
    public string SelectedFamily {
      get => selectedFamily;
      set {
        if (!Set(ref selectedFamily, value)) return;
        RefreshVersions();
        Settings.Current.Update(s => s.Client = value ?? s.Client);
        Raise(nameof(ClientSummary));
      }
    }

    private string selectedVersion;
    public string SelectedVersion {
      get => selectedVersion;
      set {
        if (!Set(ref selectedVersion, value)) return;
        if (value != null) Settings.Current.Update(s => s.ClientVersion = value);
        Raise(nameof(ClientSummary));
      }
    }

    /// <summary>"Client: qBittorrent 5.2.4" for the status bar.</summary>
    public string ClientSummary {
      get {
        var running = engine != null && engine.IsRunning;
        var name = running ? engine.ClientName : ((SelectedFamily ?? "") + " " + (SelectedVersion ?? "")).Trim();
        var proxy = Preferences.Proxy.Enabled ? "   ·   " + Preferences.Proxy : "";
        return string.Format(UiStrings.Get("status.client"), name) + proxy;
      }
    }

    public string VersionText => "v" + AppInfo.Version;

    private int selectedModeIndex;
    public int SelectedModeIndex { get => selectedModeIndex; set => Set(ref selectedModeIndex, value); }

    private string uploadText;
    public string UploadText { get => uploadText; set => Set(ref uploadText, value); }

    public bool IsRunning => engine != null && engine.IsRunning;
    public bool NotRunning => !IsRunning;

    private string ratioText = "—";
    public string RatioText { get => ratioText; set => Set(ref ratioText, value); }

    private string uploadedText = "0 bytes";
    public string UploadedText { get => uploadedText; set => Set(ref uploadedText, value); }

    private string downloadedText = "0 bytes";
    public string DownloadedText { get => downloadedText; set => Set(ref downloadedText, value); }

    private string swarmText = "– / –";
    public string SwarmText { get => swarmText; set => Set(ref swarmText, value); }

    private string elapsedText = "–";
    public string ElapsedText { get => elapsedText; set => Set(ref elapsedText, value); }

    private string nextAnnounceText = "";
    public string NextAnnounceText { get => nextAnnounceText; set => Set(ref nextAnnounceText, value); }

    private string statusText = "";
    public string StatusText { get => statusText; set => Set(ref statusText, value); }

    private IBrush statusColor = IdleBrush;
    public IBrush StatusColor { get => statusColor; set => Set(ref statusColor, value); }

    private string activityText = "";
    public string ActivityText { get => activityText; set => Set(ref activityText, value); }

    public bool HasTorrent => torrent != null;
    internal Torrent CurrentTorrent => torrent;

    // ---- actions ----

    public void LoadTorrent(string path) {
      try {
        torrent = new Torrent(path);
        TorrentDisplay = torrent.Name;
        AppendLog(string.Format(UiStrings.Get("log.loaded"), torrent.Name, Format.Bytes((long) torrent.totalLength), torrent.AnnounceList?.Count ?? 1));
        StartCommand.RaiseCanExecuteChanged();
      }
      catch (Exception ex) { AppendLog(UiStrings.Get("dlg.read_error") + ex.Message); }
    }

    private TorrentClient SelectedClient() => TorrentClientFactory.Resolve(SelectedFamily, SelectedVersion);

    private SeedOptions BuildOptions() {
      int.TryParse((UploadText ?? "").Trim(), out var up);
      var seeder = SelectedModeIndex == 0;
      var o = new SeedOptions {
        Torrent = torrent,
        Client = Settings.Current.RandomizeClientOnStart ? TorrentClientFactory.PickRandomModern(null) : SelectedClient(),
        UploadKBps = up,
        DownloadKBps = seeder ? 0 : Preferences.LeechDownloadKBps,
        FinishedPercent = seeder ? 100 : 0,
        RealFile = RealSeedFile,
        Stealth = StealthOptions.Shared,
        Budget = UpstreamBudget.Shared,
        Log = AppendLog,
      };
      Preferences.ApplyTo(o);
      return o;
    }

    private void Start() {
      if (torrent == null || IsRunning || busy) return;
      if (!int.TryParse((UploadText ?? "").Trim(), out var up) || up <= 0) { AppendLog(UiStrings.Get("dlg.upload_bad")); return; }
      Settings.Current.Update(s => s.UploadRate = up);
      SecureDns.Log = AppendLog;
      SeedOptions o;
      try { o = BuildOptions(); } catch (Exception ex) { AppendLog("error: " + ex.Message); return; }
      engine?.Dispose();
      engine = new SeedEngine(o);
      engine.AutoStopped += (e, reason) => AppendLog(UiStrings.Get("log.auto_stopped") + " " + reason);
      foreach (var w in Stealth.BelievabilityWarnings(o.UploadKBps, o.DownloadKBps)) AppendLog("⚠ " + w);
      AppendLog(string.Format(UiStrings.Get("log.starting"), torrent.Name, o.Client.Name, o.UploadKBps, o.Proxy));
      RunOffUi(engine.StartAsync);
    }

    private void Stop() {
      var e = engine;
      if (e == null || !e.IsRunning || busy) return;
      RunOffUi(e.StopAsync);
    }

    /// <summary>Starts as a complete seeder with the believable defaults on (guided setup).</summary>
    public void StartSeedingSafely() {
      SelectedModeIndex = 0;
      StealthOptions.Shared.RealisticSpeed = true;
      StealthOptions.Shared.SwarmAware = true;
      Settings.Current.Update(s => StealthOptions.Shared.SaveTo(s));
      Start();
    }

    private void RunOffUi(Func<Task> work) {
      busy = true;
      RaiseRunState();
      Task.Run(work).ContinueWith(t => {
        if (t.IsFaulted) AppendLog("error: " + (t.Exception?.GetBaseException().Message ?? "unknown"));
        Dispatcher.UIThread.Post(() => { busy = false; RaiseRunState(); RefreshLive(force: true); });
      });
    }

    private void RaiseRunState() {
      Raise(nameof(IsRunning));
      Raise(nameof(NotRunning));
      StartCommand?.RaiseCanExecuteChanged();
      StopCommand?.RaiseCanExecuteChanged();
    }

    /// <summary>One dry-run seeder announce (blocking; call off the UI thread).</summary>
    internal AnnounceProbe Probe() => AnnounceProbe.Run(torrent, SelectedClient(), Preferences.Proxy, AppendLog);

    /// <summary>Dry-run announce from the menu: logs the tracker's verdict.</summary>
    public void RunTestAnnounce() {
      if (torrent == null) { AppendLog(UiStrings.Get("dlg.no_torrent")); return; }
      AppendLog(UiStrings.Get("log.probing"));
      SecureDns.Log = AppendLog;
      Task.Run(() => Probe()).ContinueWith(t => {
        var p = t.IsFaulted ? null : t.Result;
        AppendLog(p == null ? "error" : DescribeProbe(p));
      });
    }

    internal static string DescribeProbe(AnnounceProbe p) {
      if (!string.IsNullOrEmpty(p.Error)) return UiStrings.Get("probe.error") + " " + p.Error;
      if (!string.IsNullOrEmpty(p.FailureReason)) return UiStrings.Get("probe.rejected") + " " + p.FailureReason;
      var swarm = string.Format(UiStrings.Get("probe.swarm"), p.Seeders, p.Leechers, p.Interval);
      return (p.Leechers > 0 ? UiStrings.Get("probe.accepted") : UiStrings.Get("probe.accepted_empty")) + " " + swarm;
    }

    public void AnnounceNow() => engine?.AnnounceNow();

    /// <summary>Applies a connection profile: the upload field, the shared upstream budget and the persisted value.</summary>
    public void ApplyConnectionProfile(string name) {
      var prof = ConnectionProfiles.Find(name);
      if (prof == null) return;
      UploadText = prof.UpKBps.ToString();
      Preferences.LeechDownloadKBps = prof.DownKBps;
      UpstreamBudget.Shared.TotalKBps = prof.UpKBps;
      Settings.Current.Update(s => { s.GlobalUpstreamKBps = prof.UpKBps; s.UploadRate = prof.UpKBps; Preferences.SaveTo(s); });
      AppendLog(string.Format(UiStrings.Get("log.profile"), prof.Name, prof.UpKBps, prof.DownKBps));
    }

    public void SavePreferences() {
      Settings.Current.Update(s => Preferences.SaveTo(s));
      Raise(nameof(ClientSummary));
    }

    // ---- campaigns ----

    internal void RunCampaign(Campaign c) {
      campaignEngine?.Stop();
      SecureDns.Log = AppendLog;
      campaignEngine = new CampaignEngine(c, line => AppendLog("[campaign] " + line), Preferences.Proxy, StealthOptions.Shared);
      campaignEngine.Completed += done => AppendLog("[campaign] " + UiStrings.Get("log.campaign_done"));
      var started = campaignEngine;
      Task.Run(() => started.Start());
    }

    internal void StopCampaign() {
      var c = campaignEngine;
      Task.Run(() => c?.Stop());
    }

    public bool CampaignRunning => campaignEngine != null && campaignEngine.Running;

    // ---- language ----

    public void SetLanguage(bool french) {
      UiStrings.Language = french ? Language.French : Language.English;
      Settings.Current.Update(s => s.Language = UiStrings.Code(UiStrings.Language));
      RebuildModes();
      if (torrent == null) TorrentDisplay = UiStrings.Get("no_torrent");
      RefreshLive(force: true);
      Raise(nameof(L)); // refresh every {Binding L[...]}
      Raise(nameof(ClientSummary));
      Raise(nameof(IsEnglish));
      Raise(nameof(IsFrench));
    }

    // Check-box menu items bind to these; picking one switches, re-clicking the
    // active one is a no-op (the re-raise keeps its check mark in place).
    public bool IsEnglish {
      get => !UiStrings.IsFrench;
      set { if (value) SetLanguage(false); else { Raise(nameof(IsEnglish)); Raise(nameof(IsFrench)); } }
    }
    public bool IsFrench {
      get => UiStrings.IsFrench;
      set { if (value) SetLanguage(true); else { Raise(nameof(IsEnglish)); Raise(nameof(IsFrench)); } }
    }

    // ---- settings toggles ----

    public bool Realistic {
      get => StealthOptions.Shared.RealisticSpeed;
      set { StealthOptions.Shared.RealisticSpeed = value; SaveStealth(); Raise(nameof(Realistic)); }
    }
    public bool SwarmAware {
      get => StealthOptions.Shared.SwarmAware;
      set { StealthOptions.Shared.SwarmAware = value; SaveStealth(); Raise(nameof(SwarmAware)); }
    }
    public bool RandomizeClient {
      get => Settings.Current.RandomizeClientOnStart;
      set { Settings.Current.Update(s => s.RandomizeClientOnStart = value); Raise(nameof(RandomizeClient)); }
    }
    private static void SaveStealth() => Settings.Current.Update(s => StealthOptions.Shared.SaveTo(s));

    // ---- live read-out ----

    private void RefreshLive(bool force = false) {
      var e = engine;
      if (e == null && !force) return;
      var up = e?.UploadedBytes ?? 0;
      var down = e?.DownloadedBytes ?? 0;
      RatioText = Format.Ratio(up, down);
      UploadedText = Format.Bytes(up);
      DownloadedText = Format.Bytes(down);
      var s = e?.SeederCount ?? -1; var l = e?.LeecherCount ?? -1;
      SwarmText = (s < 0 ? "–" : s.ToString()) + " / " + (l < 0 ? "–" : l.ToString());
      var running = e != null && e.IsRunning;
      ElapsedText = e != null && e.State != SeedState.Idle ? Format.Duration(e.Elapsed) : "–";
      NextAnnounceText = running ? string.Format(UiStrings.Get("status.next"), Format.Duration(e.UntilNextAnnounce)) : "";
      if (campaignEngine != null && campaignEngine.Running)
        NextAnnounceText = string.Format(UiStrings.Get("status.campaign"), campaignEngine.ActiveCount, campaignEngine.TorrentCount, Format.Bytes(campaignEngine.TotalUploaded));
      if (busy) { StatusText = UiStrings.Get("state.busy"); StatusColor = IdleBrush; }
      else if (running && e.Rejected) { StatusText = UiStrings.Get("state.rejected"); StatusColor = RejectedBrush; }
      else if (running) { StatusText = UiStrings.Get("seeding"); StatusColor = RunningBrush; }
      else { StatusText = UiStrings.Get("idle"); StatusColor = IdleBrush; }
      if (e != null && !running && e.State == SeedState.Stopped) RaiseRunState();
    }

    private void RefreshVersions() {
      Versions.Clear();
      if (SelectedFamily == null) return;
      foreach (var v in TorrentClientFactory.GetVersions(SelectedFamily)) Versions.Add(v);
      SelectedVersion = Versions.Count > 0 ? Versions[0] : null;
    }

    public void RebuildModes() {
      var idx = SelectedModeIndex;
      Modes.Clear();
      Modes.Add(UiStrings.Get("mode.seeder"));
      Modes.Add(UiStrings.Get("mode.leecher"));
      SelectedModeIndex = idx < 0 ? 0 : idx;
    }

    private void AppendLog(string line) {
      if (string.IsNullOrEmpty(line)) return;
      void Do() {
        logBuffer.Append('[').Append(DateTime.Now.ToString("T")).Append("] ").Append(line.TrimEnd()).Append('\n');
        if (logBuffer.Length > 200_000) logBuffer.Remove(0, logBuffer.Length - 150_000);
        ActivityText = logBuffer.ToString();
      }
      if (Dispatcher.UIThread.CheckAccess()) Do();
      else Dispatcher.UIThread.Post(Do);
    }
  }
}
