using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Seedforger;
using Seedforger.App.ViewModels;

namespace Seedforger.App.Views {

  /// <summary>
  /// A condensed guided setup: the safety rule, pick a torrent, ask the tracker
  /// (dry-run), then start. Drives the same MainViewModel / SeedEngine.
  /// </summary>
  public sealed class GuideWindow : Window {

    private readonly MainViewModel vm;
    private readonly TextBlock status = new TextBlock {
      Foreground = Brush.Parse("#ECEEF2"), TextWrapping = TextWrapping.Wrap, MinHeight = 60,
    };
    private readonly Button analyze;
    private readonly Button start;

    private static string P(string en, string fr) => AppOptions.Language == Language.French ? fr : en;

    public GuideWindow(MainViewModel vm) {
      this.vm = vm;
      Title = P("Guided setup", "Assistant guidé");
      Width = 520; SizeToContent = SizeToContent.Height; CanResize = false;
      WindowStartupLocation = WindowStartupLocation.CenterOwner;
      Background = Brush.Parse("#15161A");

      var rule = new TextBlock {
        Foreground = Brush.Parse("#ECEEF2"), TextWrapping = TextWrapping.Wrap, FontWeight = FontWeight.SemiBold,
        Text = P("Only seed a file you actually HAVE — downloaded once, for real, through this tracker. The swarm has monitoring peers that request real pieces; claiming a file you don't have is how you get caught.",
                 "Ne seedez qu'un fichier que vous AVEZ vraiment — téléchargé une fois, pour de vrai, via ce tracker. Le swarm contient des pairs de surveillance qui demandent de vrais morceaux ; prétendre avoir un fichier qu'on n'a pas, c'est se faire prendre."),
      };

      var browse = Pill(P("Browse for a .torrent…", "Parcourir un .torrent…"), "#4C8DF6");
      analyze = Pill(P("Analyze (ask the tracker)", "Analyser (interroger le tracker)"), "#24262C");
      start = Pill(P("Start seeding", "Démarrer le seed"), "#22C55E");
      start.IsEnabled = false;

      browse.Click += async (s, e) => await Browse();
      analyze.Click += (s, e) => Analyze();
      start.Click += (s, e) => { vm.StartSeeding(); Close(); };

      status.Text = vm.HasTorrent
        ? P("Loaded: ", "Chargé : ") + vm.TorrentDisplay
        : P("No torrent loaded yet.", "Aucun torrent chargé pour l'instant.");

      Content = new StackPanel {
        Margin = new Thickness(22), Spacing = 14,
        Children = {
          new TextBlock { Text = P("Build ratio, believably", "Gagner du ratio, de façon crédible"),
                          Foreground = Brush.Parse("#ECEEF2"), FontSize = 16, FontWeight = FontWeight.Bold },
          rule,
          browse,
          status,
          analyze,
          start,
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
        if (files != null)
          foreach (var f in files) {
            var path = f.TryGetLocalPath();
            if (!string.IsNullOrEmpty(path)) {
              vm.LoadTorrent(path);
              status.Text = P("Loaded: ", "Chargé : ") + vm.TorrentDisplay;
              start.IsEnabled = false;
              break;
            }
          }
      }
      catch { }
    }

    private void Analyze() {
      var engine = vm.CreateEngine();
      if (engine == null) { status.Text = P("Load a .torrent first.", "Chargez d'abord un .torrent."); return; }
      analyze.IsEnabled = false;
      status.Text = P("Talking to the tracker…", "Communication avec le tracker…");
      Task.Run(() => {
        try {
          engine.Start();
          Thread.Sleep(150);
          engine.Stop();
          Dispatcher.UIThread.Post(() => {
            analyze.IsEnabled = true;
            if (engine.SeederCount >= 0 && engine.LeecherCount > 0) {
              status.Text = string.Format(P("Accepted — {0} leechers to feed, {1} seeders. Good to go.",
                                             "Accepté — {0} leechers à nourrir, {1} seeders. C'est bon."),
                                          engine.LeecherCount, engine.SeederCount);
              start.IsEnabled = true;
            }
            else if (engine.SeederCount >= 0) {
              status.Text = P("Accepted, but nobody is downloading it — you'd gain nothing. Pick a busier torrent.",
                              "Accepté, mais personne ne le télécharge — vous ne gagneriez rien. Choisissez un torrent plus actif.");
            }
            else {
              status.Text = P("The tracker didn't accept the announce (see the main log).",
                              "Le tracker n'a pas accepté l'annonce (voir le log principal).");
            }
          });
        }
        catch (Exception ex) { Dispatcher.UIThread.Post(() => { analyze.IsEnabled = true; status.Text = "Error: " + ex.Message; }); }
      });
    }

    private static Button Pill(string text, string bg) => new Button {
      Content = text, Background = Brush.Parse(bg), Foreground = Brushes.White,
      HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center,
      CornerRadius = new CornerRadius(9), Padding = new Thickness(12, 10), FontWeight = FontWeight.SemiBold,
    };
  }
}
