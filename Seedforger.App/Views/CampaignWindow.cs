using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Seedforger;
using Seedforger.App.ViewModels;

namespace Seedforger.App.Views {

  /// <summary>Native campaign builder — fills a Campaign and runs it via the
  /// portable CampaignEngine (multi-torrent, cross-platform).</summary>
  public sealed class CampaignWindow : Window {

    private readonly MainViewModel vm;

    private readonly ComboBox goal = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBox target = Field("2");
    private readonly ComboBox connection = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBox torrentFolder = Field("");
    private readonly TextBox realFolder = Field("");
    private readonly CheckBox activeHours = new CheckBox { Content = "Only seed during hours", IsChecked = true, Foreground = Brush.Parse("#ECEEF2") };
    private readonly NumericUpDown hoursStart = Num(8, 0, 24);
    private readonly NumericUpDown hoursEnd = Num(24, 0, 24);
    private readonly NumericUpDown staggerMin = Num(3, 0, 600);
    private readonly NumericUpDown staggerMax = Num(40, 0, 600);
    private readonly NumericUpDown maxConcurrent = Num(6, 1, 100);
    private readonly CheckBox rotate = new CheckBox { Content = "Rotate client each start", IsChecked = true, Foreground = Brush.Parse("#ECEEF2") };
    private readonly NumericUpDown deadlineDays = Num(14, 0, 3650);

    private static string P(string en, string fr) => AppOptions.Language == Language.French ? fr : en;

    public CampaignWindow(MainViewModel vm) {
      this.vm = vm;
      Title = P("New campaign", "Nouvelle campagne");
      Width = 470; SizeToContent = SizeToContent.Height; CanResize = false;
      WindowStartupLocation = WindowStartupLocation.CenterOwner;
      Background = Brush.Parse("#15161A");

      goal.ItemsSource = new[] { P("Reach an upload total (GB)", "Atteindre un total d'upload (Go)"), P("Reach a ratio", "Atteindre un ratio") };
      goal.SelectedIndex = 0;
      foreach (var p in ConnectionProfiles.All) connection.Items.Add(p.Name);
      if (connection.Items.Count > 0) connection.SelectedIndex = Math.Min(4, connection.Items.Count - 1);

      var start = Pill(P("Start campaign", "Lancer la campagne"), "#22C55E");
      var stop = Pill(P("Stop campaign", "Arrêter la campagne"), "#F05050");
      var cancel = Pill(P("Close", "Fermer"), "#24262C");
      start.Click += (s, e) => { if (Validate()) { vm.RunCampaign(Build()); Close(); } };
      stop.Click += (s, e) => { vm.StopCampaign(); };
      cancel.Click += (s, e) => Close();

      Content = new ScrollViewer {
        MaxHeight = 640,
        Content = new StackPanel {
          Margin = new Thickness(20), Spacing = 8,
          Children = {
            Label(P("Goal", "Objectif")), goal,
            Label(P("Target (GB or ratio)", "Cible (Go ou ratio)")), target,
            Label(P("Spread over (days)", "Étaler sur (jours)")), deadlineDays,
            Label(P("Connection", "Connexion")), connection,
            Label(P("Torrent folder", "Dossier des torrents")), FolderRow(torrentFolder),
            Label(P("Real files folder (optional)", "Dossier des vrais fichiers (option)")), FolderRow(realFolder),
            activeHours,
            Row(Label(P("Active hours", "Heures actives")), hoursStart, Label("→"), hoursEnd),
            Row(Label(P("Stagger min/max", "Décalage min/max")), staggerMin, staggerMax),
            Row(Label(P("Max at once", "Max simultanés")), maxConcurrent),
            rotate,
            new StackPanel {
              Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right,
              Margin = new Thickness(0, 12, 0, 0), Children = { cancel, stop, start },
            },
          },
        },
      };
    }

    private bool Validate() {
      if (!System.IO.Directory.Exists(torrentFolder.Text ?? "")) return false;
      return double.TryParse((target.Text ?? "").Replace(',', '.'),
        System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v) && v > 0;
    }

    private Campaign Build() {
      double.TryParse((target.Text ?? "").Replace(',', '.'), System.Globalization.NumberStyles.Any,
        System.Globalization.CultureInfo.InvariantCulture, out var v);
      var ratio = goal.SelectedIndex == 1;
      return new Campaign {
        Goal = ratio ? "ratio" : "upload",
        TargetRatio = ratio ? v : 2.0,
        UploadGoalGB = ratio ? 100 : v,
        DeadlineHours = (double) deadlineDays.Value * 24,
        Connection = connection.SelectedItem?.ToString() ?? "",
        UseActiveHours = activeHours.IsChecked == true,
        ActiveHoursStart = (int) hoursStart.Value,
        ActiveHoursEnd = (int) hoursEnd.Value,
        RotateClient = rotate.IsChecked == true,
        TorrentFolder = torrentFolder.Text ?? "",
        RealFileFolder = realFolder.Text ?? "",
        StaggerMinMinutes = (int) staggerMin.Value,
        StaggerMaxMinutes = (int) Math.Max(staggerMin.Value ?? 0, staggerMax.Value ?? 0),
        MaxConcurrent = (int) maxConcurrent.Value,
      };
    }

    private Control FolderRow(TextBox tb) {
      var browse = new Button { Content = "…", Width = 40 };
      browse.Click += async (s, e) => {
        try {
          var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = false });
          if (folders != null)
            foreach (var f in folders) { var p = f.TryGetLocalPath(); if (!string.IsNullOrEmpty(p)) { tb.Text = p; break; } }
        }
        catch { }
      };
      return new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,40"), Children = { Col(tb, 0), Col(browse, 2) } };
    }

    private static Control Col(Control c, int col) { Grid.SetColumn(c, col); return c; }

    private static Control Row(params Control[] items) {
      var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
      foreach (var c in items) { c.VerticalAlignment = VerticalAlignment.Center; sp.Children.Add(c); }
      return sp;
    }

    private static TextBlock Label(string t) => new TextBlock { Text = t, Foreground = Brush.Parse("#8A909C"), FontSize = 12, Margin = new Thickness(0, 4, 0, 0) };
    private static TextBox Field(string v) => new TextBox { Text = v, Background = Brush.Parse("#16171B"), Foreground = Brush.Parse("#ECEEF2"), BorderBrush = Brush.Parse("#2C2F36"), CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 7) };
    private static NumericUpDown Num(int val, int min, int max) => new NumericUpDown { Value = val, Minimum = min, Maximum = max, Width = 90, Foreground = Brush.Parse("#ECEEF2") };
    private static Button Pill(string text, string bg) => new Button { Content = text, Background = Brush.Parse(bg), Foreground = Brushes.White, CornerRadius = new CornerRadius(9), Padding = new Thickness(14, 8), FontWeight = FontWeight.SemiBold };
  }
}
