using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Seedforger;
using Seedforger.UI;
using Seedforger.App.ViewModels;

namespace Seedforger.App.Views {

  /// <summary>
  /// A condensed guided setup: the safety rule, pick a torrent, ask the tracker
  /// (a real dry-run probe), then start as a complete seeder with the believable
  /// defaults on. Plain themed controls.
  /// </summary>
  public sealed class GuideWindow : Window {

    private readonly MainViewModel vm;
    private readonly TextBlock status = new TextBlock { TextWrapping = TextWrapping.Wrap, MinHeight = 60 };
    private readonly Button analyze;
    private readonly Button start;

    private static string P(string en, string fr) => UiStrings.Pick(en, fr);

    public GuideWindow(MainViewModel vm) {
      this.vm = vm;
      Title = P("Guided setup", "Assistant guidé");
      Width = 540; SizeToContent = SizeToContent.Height; CanResize = false;
      WindowStartupLocation = WindowStartupLocation.CenterOwner;

      var rule = new TextBlock {
        TextWrapping = TextWrapping.Wrap, FontWeight = FontWeight.SemiBold,
        Text = P("Only seed a file you actually HAVE — downloaded once, for real, through this tracker. The swarm has monitoring peers that request real pieces; claiming a file you don't have is how you get caught.",
                 "Ne seedez qu'un fichier que vous AVEZ vraiment — téléchargé une fois, pour de vrai, via ce tracker. Le swarm contient des pairs de surveillance qui demandent de vrais morceaux ; prétendre avoir un fichier qu'on n'a pas, c'est se faire prendre."),
      };

      var browse = Wide(P("Browse for a .torrent…", "Parcourir un .torrent…"));
      analyze = Wide(P("Analyze (ask the tracker)", "Analyser (interroger le tracker)"));
      start = Wide(P("Start seeding", "Démarrer le seed"), accent: true);
      start.IsEnabled = false;

      browse.Click += async (s, e) => await Browse();
      analyze.Click += (s, e) => Analyze();
      start.Click += (s, e) => { vm.StartSeedingSafely(); Close(); };

      status.Text = vm.HasTorrent
        ? P("Loaded: ", "Chargé : ") + vm.TorrentDisplay
        : P("No torrent loaded yet.", "Aucun torrent chargé pour l'instant.");

      Content = new StackPanel {
        Margin = new Thickness(22), Spacing = 14,
        Children = {
          new TextBlock { Text = P("Build ratio, believably", "Gagner du ratio, de façon crédible"), FontSize = 17, FontWeight = FontWeight.Bold },
          rule, browse, status, analyze, start,
        },
      };
    }

    private async Task Browse() {
      try {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
          Title = P("Choose a .torrent you have", "Choisissez un .torrent que vous avez"),
          AllowMultiple = false,
          FileTypeFilter = new[] { new FilePickerFileType("Torrent") { Patterns = new[] { "*.torrent" } } },
        });
        var path = MainWindow.FirstLocalPath(files);
        if (path == null) return;
        vm.LoadTorrent(path);
        status.Text = P("Loaded: ", "Chargé : ") + vm.TorrentDisplay;
        start.IsEnabled = false;
      }
      catch (Exception ex) { status.Text = "Error: " + ex.Message; }
    }

    private void Analyze() {
      if (!vm.HasTorrent) { status.Text = P("Load a .torrent first.", "Chargez d'abord un .torrent."); return; }
      analyze.IsEnabled = false;
      status.Text = P("Talking to the tracker…", "Communication avec le tracker…");
      Task.Run(() => vm.Probe()).ContinueWith(t => Dispatcher.UIThread.Post(() => {
        analyze.IsEnabled = true;
        var p = t.IsFaulted ? null : t.Result;
        if (p == null) { status.Text = "Error: " + (t.Exception?.GetBaseException().Message ?? "unknown"); return; }
        status.Text = MainViewModel.DescribeProbe(p);
        start.IsEnabled = p.Worthwhile;
      }));
    }

    private static Button Wide(string text, bool accent = false) {
      var b = new Button {
        Content = text,
        HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center,
        Padding = new Thickness(12, 8),
      };
      if (accent) b.Classes.Add("accent");
      return b;
    }
  }
}
