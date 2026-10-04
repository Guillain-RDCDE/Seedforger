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
  /// portable CampaignEngine (multi-torrent, cross-platform). A plain labelled
  /// form in the theme's own controls.</summary>
  public sealed class CampaignWindow : Window {

    private readonly MainViewModel vm;

    private readonly ComboBox goal = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBox target = new TextBox { Text = "2" };
    private readonly ComboBox connection = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBox torrentFolder = new TextBox();
    private readonly TextBox realFolder = new TextBox();
    private readonly CheckBox activeHours = new CheckBox { IsChecked = true };
    private readonly NumericUpDown hoursStart = Num(8, 0, 24);
    private readonly NumericUpDown hoursEnd = Num(24, 0, 24);
    private readonly NumericUpDown staggerMin = Num(3, 0, 600);
    private readonly NumericUpDown staggerMax = Num(40, 0, 600);
    private readonly NumericUpDown maxConcurrent = Num(6, 1, 100);
    private readonly CheckBox rotate = new CheckBox { IsChecked = true };
    private readonly NumericUpDown deadlineDays = Num(14, 0, 3650);

    private static string P(string en, string fr) => AppOptions.Language == Language.French ? fr : en;

    public CampaignWindow(MainViewModel vm) {
      this.vm = vm;
      Title = P("New campaign", "Nouvelle campagne");
      Width = 520; SizeToContent = SizeToContent.Height; CanResize = false;
      WindowStartupLocation = WindowStartupLocation.CenterOwner;

      activeHours.Content = P("Only seed during these hours", "Seeder seulement pendant ces heures");
      rotate.Content = P("Rotate the client on each start", "Changer de client à chaque départ");

      goal.ItemsSource = new[] { P("Reach an upload total (GB)", "Atteindre un total d'upload (Go)"), P("Reach a ratio", "Atteindre un ratio") };
      goal.SelectedIndex = 0;
      foreach (var p in ConnectionProfiles.All) connection.Items.Add(p.Name);
      if (connection.Items.Count > 0) connection.SelectedIndex = Math.Min(4, connection.Items.Count - 1);

      var start = new Button { Content = P("Start campaign", "Lancer la campagne"), MinWidth = 140 };
      start.Classes.Add("accent");
      var stop = new Button { Content = P("Stop campaign", "Arrêter la campagne"), MinWidth = 140 };
      var cancel = new Button { Content = P("Close", "Fermer"), MinWidth = 90 };
      start.Click += (s, e) => { if (Validate()) { vm.RunCampaign(Build()); Close(); } };
      stop.Click += (s, e) => { vm.StopCampaign(); };
      cancel.Click += (s, e) => Close();

      Content = new ScrollViewer {
        MaxHeight = 680,
        Content = new StackPanel {
          Margin = new Thickness(20), Spacing = 8,
          Children = {
            Row(P("Goal", "Objectif"), goal),
            Row(P("Target (GB or ratio)", "Cible (Go ou ratio)"), target),
            Row(P("Spread over (days)", "Étaler sur (jours)"), deadlineDays),
            Row(P("Connection", "Connexion"), connection),
            Row(P("Torrent folder", "Dossier des torrents"), FolderRow(torrentFolder)),
            Row(P("Real files (optional)", "Vrais fichiers (option)"), FolderRow(realFolder)),
            Row("", activeHours),
            Row(P("Active hours", "Heures actives"), Inline(hoursStart, new TextBlock { Text = P("to", "à"), VerticalAlignment = VerticalAlignment.Center }, hoursEnd)),
            Row(P("Stagger (min)", "Décalage (min)"), Inline(staggerMin, new TextBlock { Text = P("to", "à"), VerticalAlignment = VerticalAlignment.Center }, staggerMax)),
            Row(P("Max at once", "Max simultanés"), maxConcurrent),
            Row("", rotate),
            new StackPanel {
              Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right,
              Margin = new Thickness(0, 14, 0, 0), Children = { cancel, stop, start },
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
      var browse = new Button { Content = "…", MinWidth = 40 };
      browse.Click += async (s, e) => {
        try {
          var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = false });
          if (folders != null)
            foreach (var f in folders) { var p = f.TryGetLocalPath(); if (!string.IsNullOrEmpty(p)) { tb.Text = p; break; } }
        }
        catch { }
      };
      return new Grid { ColumnDefinitions = new ColumnDefinitions("*,8,Auto"), Children = { Col(tb, 0), Col(browse, 2) } };
    }

    private static Control Col(Control c, int col) { Grid.SetColumn(c, col); return c; }

    /// <summary>A classic form row: a fixed-width label on the left, the field on the right.</summary>
    private static Control Row(string label, Control field) {
      var g = new Grid { ColumnDefinitions = new ColumnDefinitions("170,*") };
      g.Children.Add(Col(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) }, 0));
      field.VerticalAlignment = VerticalAlignment.Center;
      g.Children.Add(Col(field, 1));
      return g;
    }

    private static Control Inline(params Control[] items) {
      var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
      foreach (var c in items) { c.VerticalAlignment = VerticalAlignment.Center; sp.Children.Add(c); }
      return sp;
    }

    private static NumericUpDown Num(int val, int min, int max) =>
      new NumericUpDown { Value = val, Minimum = min, Maximum = max, Width = 110, HorizontalAlignment = HorizontalAlignment.Left };
  }
}
