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
      FillConnectionProfiles();
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
      catch (Exception) { /* nothing usable was dropped */ }
    }

    /// <summary>The first picker result that is a real local path, or null.</summary>
    internal static string FirstLocalPath(System.Collections.Generic.IEnumerable<IStorageItem> items) {
      if (items == null) return null;
      foreach (var f in items) {
        var path = f.TryGetLocalPath();
        if (!string.IsNullOrEmpty(path)) return path;
      }
      return null;
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
        var path = FirstLocalPath(files);
        if (path != null) vm.LoadTorrent(path);
      }
      catch (Exception) { /* cancelled */ }
    }

    // ---- Run ----

    private void OnTestAnnounce(object sender, RoutedEventArgs e) => vm.RunTestAnnounce();

    private void OnEnglish(object sender, RoutedEventArgs e) => vm.SetLanguage(false);
    private void OnFrench(object sender, RoutedEventArgs e) => vm.SetLanguage(true);

    private async void OnServeReal(object sender, RoutedEventArgs e) {
      try {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
          Title = T("dlg.serve_title"), AllowMultiple = false,
        });
        var path = FirstLocalPath(files);
        if (path != null) vm.RealSeedFile = path;
      }
      catch (Exception) { /* cancelled */ }
    }

    // ---- Tools ----

    private void OnGuided(object sender, RoutedEventArgs e) => new GuideWindow(vm).ShowDialog(this);

    private void OnCampaigns(object sender, RoutedEventArgs e) => new CampaignWindow(vm).ShowDialog(this);

    private async void OnAdvanced(object sender, RoutedEventArgs e) {
      var dlg = new AdvancedWindow(vm.Preferences);
      await dlg.ShowDialog(this);
      if (dlg.Saved) vm.SavePreferences();
    }

    private void OnAnnounceNow(object sender, RoutedEventArgs e) => vm.AnnounceNow();

    private void OnStopCampaign(object sender, RoutedEventArgs e) => vm.StopCampaign();

    private void OnConnectionProfile(object sender, RoutedEventArgs e) {
      if (sender is MenuItem mi && mi.Header is string name) vm.ApplyConnectionProfile(name);
    }

    /// <summary>Fills the connection-profile submenu once the menu exists.</summary>
    private void FillConnectionProfiles() {
      var menu = this.FindControl<MenuItem>("ConnectionMenu");
      if (menu == null) return;
      foreach (var p in ConnectionProfiles.All) {
        var item = new MenuItem { Header = p.Name };
        item.Click += OnConnectionProfile;
        menu.Items.Add(item);
      }
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
