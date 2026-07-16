using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
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

    private async void OnBrowse(object sender, RoutedEventArgs e) {
      try {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
          Title = Seedforger.UI.UiStrings.Get("dlg.choose_torrent"),
          AllowMultiple = false,
          FileTypeFilter = new[] {
            new FilePickerFileType("Torrent") { Patterns = new[] { "*.torrent" } },
          },
        });
        if (files != null) {
          foreach (var f in files) {
            var path = f.TryGetLocalPath();
            if (!string.IsNullOrEmpty(path)) { vm.LoadTorrent(path); break; }
          }
        }
      }
      catch (Exception) { /* picker cancelled/unavailable */ }
      await Task.CompletedTask;
    }

    // Header nav — wired to real screens in the next increments.
    private void OnGuided(object sender, RoutedEventArgs e) { }
    private void OnCampaigns(object sender, RoutedEventArgs e) { }
    private void OnTools(object sender, RoutedEventArgs e) { }
    private void OnSettings(object sender, RoutedEventArgs e) { }
    private void OnHelp(object sender, RoutedEventArgs e) { }
    private void OnAdvanced(object sender, RoutedEventArgs e) { }
  }
}
