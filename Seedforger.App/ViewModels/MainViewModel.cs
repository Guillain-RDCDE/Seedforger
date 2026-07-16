using System;
using System.Collections.ObjectModel;
using System.Text;
using Avalonia.Media;
using Avalonia.Threading;
using Seedforger;
using Seedforger.BitTorrent;

namespace Seedforger.App.ViewModels {

  /// <summary>
  /// Drives the headless <see cref="SeedEngine"/> from the Avalonia UI: holds the
  /// user's choices, starts/stops seeding, and polls live stats. The same portable
  /// engine as the CLI — no WinForms, so this GUI runs on Windows/Linux/macOS.
  /// </summary>
  public sealed class MainViewModel : ViewModelBase {

    public LocProxy L { get; } = new LocProxy();

    private readonly DispatcherTimer poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly StringBuilder logBuffer = new StringBuilder();
    private Torrent torrent;
    private SeedEngine engine;

    public ObservableCollection<string> Families { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> Versions { get; } = new ObservableCollection<string>();
    public ObservableCollection<string> Modes { get; } = new ObservableCollection<string>();

    public RelayCommand StartCommand { get; }
    public RelayCommand StopCommand { get; }

    public MainViewModel() {
      TryLoadSettings();
      foreach (var f in TorrentClientFactory.GetFamilies()) Families.Add(f);
      selectedFamily = Families.Count > 0 ? Families[0] : null;
      RefreshVersions();
      RebuildModes();

      StartCommand = new RelayCommand(Start, () => !IsRunning && torrent != null);
      StopCommand = new RelayCommand(Stop, () => IsRunning);

      poll.Tick += (s, e) => RefreshLive();
      poll.Start();
      RefreshLive();
    }

    private static void TryLoadSettings() {
      try {
        var s = Settings.Current;
        AppOptions.RealisticSpeed = s.RealisticSpeed;
        AppOptions.SwarmAware = s.SwarmAware;
        AppOptions.RandomizeClientOnStart = s.RandomizeClientOnStart;
        AppOptions.Language = string.Equals(s.Language, "fr", StringComparison.OrdinalIgnoreCase) ? Language.French : Language.English;
        Bandwidth.GlobalUpKBps = s.GlobalUpstreamKBps;
      }
      catch { }
    }

    // ---- bindable state ----

    private string torrentDisplay = Seedforger.UI.UiStrings.Get("no_torrent");
    public string TorrentDisplay { get => torrentDisplay; set => Set(ref torrentDisplay, value); }

    private string selectedFamily;
    public string SelectedFamily {
      get => selectedFamily;
      set { if (Set(ref selectedFamily, value)) RefreshVersions(); }
    }

    private string selectedVersion;
    public string SelectedVersion { get => selectedVersion; set => Set(ref selectedVersion, value); }

    private int selectedModeIndex;
    public int SelectedModeIndex { get => selectedModeIndex; set => Set(ref selectedModeIndex, value); }

    private string uploadText = "1024";
    public string UploadText { get => uploadText; set => Set(ref uploadText, value); }

    private bool isRunning;
    public bool IsRunning {
      get => isRunning;
      private set {
        if (Set(ref isRunning, value)) {
          Raise(nameof(NotRunning));
          StartCommand?.RaiseCanExecuteChanged();
          StopCommand?.RaiseCanExecuteChanged();
        }
      }
    }
    public bool NotRunning => !IsRunning;

    private string ratioText = "—";
    public string RatioText { get => ratioText; set => Set(ref ratioText, value); }

    private string uploadedText = "0 bytes";
    public string UploadedText { get => uploadedText; set => Set(ref uploadedText, value); }

    private string swarmText = "–  /  –";
    public string SwarmText { get => swarmText; set => Set(ref swarmText, value); }

    private string statusText = Seedforger.UI.UiStrings.Get("idle");
    public string StatusText { get => statusText; set => Set(ref statusText, value); }

    private IBrush statusColor = new SolidColorBrush(Color.Parse("#8A909C"));
    public IBrush StatusColor { get => statusColor; set => Set(ref statusColor, value); }

    private string activityText = "";
    public string ActivityText { get => activityText; set => Set(ref activityText, value); }

    // ---- actions ----

    public void LoadTorrent(string path) {
      try {
        torrent = new Torrent(path);
        TorrentDisplay = string.IsNullOrEmpty(torrent.Name) ? System.IO.Path.GetFileName(path) : torrent.Name;
        StartCommand.RaiseCanExecuteChanged();
      }
      catch (Exception ex) { AppendLog("Couldn't read that .torrent: " + ex.Message); }
    }

    private void Start() {
      if (torrent == null || IsRunning) return;
      var client = TorrentClientFactory.GetClient((SelectedFamily + " " + SelectedVersion).Trim());
      var finished = SelectedModeIndex == 0 ? 100 : 0;
      int.TryParse((UploadText ?? "").Trim(), out var up);
      var down = finished >= 100 ? 0 : 0;
      engine = new SeedEngine(torrent, client, new ProxyInfo(), up, down, finished) { Log = AppendLog };
      SecureDns.Log = AppendLog;
      engine.Start();
      IsRunning = true;
    }

    private void Stop() {
      try { engine?.Stop(); } catch { }
      IsRunning = false;
    }

    private void RefreshLive() {
      if (engine == null) return;
      var up = Math.Max(0, engine.UploadedBytes);
      var down = Math.Max(0, engine.DownloadedBytes);
      RatioText = down > 0 ? ((double) up / down).ToString("0.00") : "—";
      UploadedText = FormatSize(up);
      var s = engine.SeederCount; var l = engine.LeecherCount;
      SwarmText = (s < 0 ? "–" : s.ToString()) + "  /  " + (l < 0 ? "–" : l.ToString());
      var running = engine.IsRunning && IsRunning;
      StatusText = running ? Seedforger.UI.UiStrings.Get("seeding") : Seedforger.UI.UiStrings.Get("idle");
      StatusColor = new SolidColorBrush(Color.Parse(running ? "#22C55E" : "#8A909C"));
    }

    private void RefreshVersions() {
      Versions.Clear();
      if (SelectedFamily == null) return;
      var vers = TorrentClientFactory.GetVersions(SelectedFamily);
      if (vers != null) foreach (var v in vers) Versions.Add(v);
      SelectedVersion = Versions.Count > 0 ? Versions[0] : null;
    }

    public void RebuildModes() {
      var idx = SelectedModeIndex;
      Modes.Clear();
      Modes.Add(Seedforger.UI.UiStrings.Get("mode.seeder"));
      Modes.Add(Seedforger.UI.UiStrings.Get("mode.leecher"));
      SelectedModeIndex = idx < 0 ? 0 : idx;
    }

    private void AppendLog(string line) {
      void Do() {
        logBuffer.Append(line).Append('\n');
        if (logBuffer.Length > 200000) logBuffer.Remove(0, logBuffer.Length - 150000);
        ActivityText = logBuffer.ToString();
      }
      if (Dispatcher.UIThread.CheckAccess()) Do();
      else Dispatcher.UIThread.Post(Do);
    }

    private static string FormatSize(long bytes) {
      string[] u = { "bytes", "KB", "MB", "GB", "TB" };
      double v = bytes; var i = 0;
      while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
      return (i == 0 ? bytes.ToString() : v.ToString("0.00")) + " " + u[i];
    }
  }
}
