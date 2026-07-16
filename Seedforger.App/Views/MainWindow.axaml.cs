using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Seedforger;
using Seedforger.App.ViewModels;

namespace Seedforger.App.Views {

  public partial class MainWindow : Window {
    private readonly MainViewModel vm = new MainViewModel();
    private ScrollViewer logScroller;

    public MainWindow() {
      InitializeComponent();
      DataContext = vm;
      logScroller = this.FindControl<ScrollViewer>("LogScroller");
      vm.PropertyChanged += (s, e) => {
        if (e.PropertyName == nameof(MainViewModel.ActivityText))
          logScroller?.ScrollToEnd();
      };
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private static string T(string key) => UI.UiStrings.Get(key);

    private async void OnBrowse(object sender, RoutedEventArgs e) => await BrowseTorrent();

    private async Task ServeRealFile() {
      try {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
          Title = T("dlg.serve_title"), AllowMultiple = false,
        });
        if (files != null)
          foreach (var f in files) {
            var path = f.TryGetLocalPath();
            if (!string.IsNullOrEmpty(path)) { vm.RealSeedFile = path; break; }
          }
      }
      catch { }
    }

    private async Task BrowseTorrent() {
      try {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
          Title = T("dlg.choose_torrent"),
          AllowMultiple = false,
          FileTypeFilter = new[] { new FilePickerFileType("Torrent") { Patterns = new[] { "*.torrent" } } },
        });
        if (files != null)
          foreach (var f in files) {
            var path = f.TryGetLocalPath();
            if (!string.IsNullOrEmpty(path)) { vm.LoadTorrent(path); break; }
          }
      }
      catch { /* cancelled */ }
    }

    // ---- header nav ----

    private void OnGuided(object sender, RoutedEventArgs e) {
      new GuideWindow(vm).ShowDialog(this);
    }

    private void OnCampaigns(object sender, RoutedEventArgs e) {
      _ = ShowInfo(T("nav.campaigns"),
        "Campaigns run many torrents at once. In this cross-platform build, use the headless CLI or the Windows GUI for multi-torrent campaigns — a native campaign view is on the roadmap.");
    }

    private void OnTools(object sender, RoutedEventArgs e) {
      var mf = new MenuFlyout();
      mf.Items.Add(Item(T("menu.load_torrent"), async () => await BrowseTorrent()));
      mf.Items.Add(Item(T("menu.test_announce"), () => vm.RunTestAnnounce()));
      mf.Items.Add(Item(T("menu.serve_real"), async () => await ServeRealFile()));
      mf.ShowAt((Control) sender);
    }

    private void OnSettings(object sender, RoutedEventArgs e) {
      var mf = new MenuFlyout();
      mf.Items.Add(Toggle(T("menu.realistic"), vm.Realistic, v => vm.Realistic = v));
      mf.Items.Add(Toggle(T("menu.swarm"), vm.SwarmAware, v => vm.SwarmAware = v));
      mf.Items.Add(Toggle(T("menu.randomize"), vm.RandomizeClient, v => vm.RandomizeClient = v));
      mf.Items.Add(new Separator());
      var lang = new MenuItem { Header = T("menu.language") };
      lang.Items.Add(Item("English", () => vm.SetLanguage(false)));
      lang.Items.Add(Item("Français", () => vm.SetLanguage(true)));
      mf.Items.Add(lang);
      mf.ShowAt((Control) sender);
    }

    private void OnHelp(object sender, RoutedEventArgs e) {
      var mf = new MenuFlyout();
      mf.Items.Add(Item(T("menu.about") + "  v" + AppInfo.Version, () =>
        _ = ShowInfo(AppInfo.Name, $"{AppInfo.Name} v{AppInfo.Version}\n\n{AppInfo.SiteUrl}")));
      mf.Items.Add(Item(T("menu.open_repo"), () => OpenUrl(AppInfo.SiteUrl)));
      mf.ShowAt((Control) sender);
    }

    private void OnAdvanced(object sender, RoutedEventArgs e) {
      var dlg = new AdvancedWindow(vm.Proxy);
      dlg.ShowDialog(this).ContinueWith(_ => {
        if (dlg.Result != null) vm.Proxy = dlg.Result.Value;
      }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    // ---- helpers ----

    private static MenuItem Item(string header, Action onClick) {
      var mi = new MenuItem { Header = header };
      mi.Click += (s, e) => onClick();
      return mi;
    }

    private static MenuItem Toggle(string header, bool isOn, Action<bool> set) {
      var mi = new MenuItem { Header = (isOn ? "✓  " : "     ") + header };
      mi.Click += (s, e) => set(!isOn);
      return mi;
    }

    private void OpenUrl(string url) {
      try { Launcher.LaunchUriAsync(new Uri(url)); } catch { }
    }

    private async Task ShowInfo(string title, string message) {
      var win = new Window {
        Title = title, Width = 420, SizeToContent = SizeToContent.Height,
        Background = Avalonia.Media.Brush.Parse("#1E2025"),
        WindowStartupLocation = WindowStartupLocation.CenterOwner, CanResize = false,
        Content = new StackPanel {
          Margin = new Avalonia.Thickness(20), Spacing = 16,
          Children = {
            new TextBlock { Text = message, Foreground = Avalonia.Media.Brush.Parse("#ECEEF2"), TextWrapping = Avalonia.Media.TextWrapping.Wrap },
          },
        },
      };
      await win.ShowDialog(this);
    }
  }
}
