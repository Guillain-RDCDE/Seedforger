using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
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
      // Drop a .torrent anywhere on the window to load it.
      AddHandler(DragDrop.DropEvent, OnDrop);
      AddHandler(DragDrop.DragOverEvent, (s, e) => {
        e.DragEffects = e.Data.Contains(DataFormats.Files) ? DragDropEffects.Copy : DragDropEffects.None;
      });
      DragDrop.SetAllowDrop(this, true);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private static string T(string key) => UI.UiStrings.Get(key);

    private void OnDrop(object sender, DragEventArgs e) {
      try {
        var files = e.Data.GetFiles();
        if (files == null) return;
        foreach (var f in files) {
          var path = f.TryGetLocalPath();
          if (!string.IsNullOrEmpty(path) && path.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase)) {
            vm.LoadTorrent(path);
            break;
          }
        }
      }
      catch { }
    }

    // ---- File ----

    private async void OnBrowse(object sender, RoutedEventArgs e) => await BrowseTorrent();

    private void OnExit(object sender, RoutedEventArgs e) => Close();

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

    // ---- Run ----

    private void OnTestAnnounce(object sender, RoutedEventArgs e) => vm.RunTestAnnounce();

    private async void OnServeReal(object sender, RoutedEventArgs e) {
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

    // ---- Tools ----

    private void OnGuided(object sender, RoutedEventArgs e) => new GuideWindow(vm).ShowDialog(this);

    private void OnCampaigns(object sender, RoutedEventArgs e) => new CampaignWindow(vm).ShowDialog(this);

    private void OnAdvanced(object sender, RoutedEventArgs e) {
      var dlg = new AdvancedWindow(vm.Proxy);
      dlg.ShowDialog(this).ContinueWith(_ => {
        if (dlg.Result != null) vm.Proxy = dlg.Result.Value;
      }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    // ---- Help ----

    private async void OnAbout(object sender, RoutedEventArgs e) =>
      await ShowInfo(AppInfo.Name, string.Format(T("dlg.about_text"), AppInfo.Name, AppInfo.Version, AppInfo.SiteUrl));

    private void OnRepo(object sender, RoutedEventArgs e) => OpenUrl(AppInfo.SiteUrl);

    // ---- helpers ----

    private void OpenUrl(string url) {
      try { Launcher.LaunchUriAsync(new Uri(url)); } catch { }
    }

    private async Task ShowInfo(string title, string message) {
      var ok = new Button { Content = "OK", MinWidth = 90, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
      var win = new Window {
        Title = title, Width = 460, SizeToContent = SizeToContent.Height,
        WindowStartupLocation = WindowStartupLocation.CenterOwner, CanResize = false,
        Content = new StackPanel {
          Margin = new Avalonia.Thickness(20), Spacing = 16,
          Children = {
            new SelectableTextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
            ok,
          },
        },
      };
      ok.Click += (s, e) => win.Close();
      await win.ShowDialog(this);
    }
  }
}
