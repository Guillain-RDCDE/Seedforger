using System;
using System.Drawing;
using System.Windows.Forms;

namespace Seedforger {

  /// <summary>
  /// A visual campaign builder — no JSON by hand. Fill the goal, believability
  /// profile and torrent folder, then Start (or Save/Load a campaign.json).
  /// Built in code so it stays self-contained and themed.
  /// </summary>
  internal sealed class CampaignForm : Form {

    private readonly ComboBox goalCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox goalValue = new TextBox();
    private readonly Label goalUnit = new Label { AutoSize = true };
    private readonly NumericUpDown deadlineDays = new NumericUpDown { Minimum = 0, Maximum = 3650, Value = 14 };
    private readonly ComboBox connectionCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox activeHoursCheck = new CheckBox { Text = P("Only seed during hours", "Seeder seulement pendant ces heures"), Checked = true, AutoSize = true };
    private readonly NumericUpDown hoursStart = new NumericUpDown { Minimum = 0, Maximum = 24, Value = 8 };
    private readonly NumericUpDown hoursEnd = new NumericUpDown { Minimum = 0, Maximum = 24, Value = 24 };
    private readonly CheckBox rotateCheck = new CheckBox { Text = P("Rotate client each start", "Changer de client à chaque départ"), Checked = true, AutoSize = true };
    private readonly TextBox torrentFolder = new TextBox();
    private readonly TextBox realFolder = new TextBox();
    private readonly NumericUpDown staggerMin = new NumericUpDown { Minimum = 0, Maximum = 600, Value = 3 };
    private readonly NumericUpDown staggerMax = new NumericUpDown { Minimum = 0, Maximum = 600, Value = 40 };
    private readonly NumericUpDown maxConcurrent = new NumericUpDown { Minimum = 1, Maximum = 100, Value = 6 };

    /// <summary>The campaign to run, set when the user clicks Start.</summary>
    internal Campaign Result { get; private set; }

    private static string P(string en, string fr) => Localization.Pick(en, fr);

    internal CampaignForm(Campaign preset = null) {
      Text = P("New campaign", "Nouvelle campagne");
      FormBorderStyle = FormBorderStyle.FixedDialog;
      MaximizeBox = false; MinimizeBox = false;
      StartPosition = FormStartPosition.CenterParent;
      ClientSize = new Size(470, 520);
      try { Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath); } catch { }

      goalCombo.Items.AddRange(new object[] { P("Reach an upload total (GB)", "Atteindre un total d'upload (Go)"), P("Reach a ratio", "Atteindre un ratio") });
      goalCombo.SelectedIndexChanged += (s, e) => UpdateGoalUnit();
      foreach (var p in ConnectionProfiles.All) connectionCombo.Items.Add(p.Name);

      var y = 16;
      AddRow(P("Goal", "Objectif"), goalCombo, ref y);
      AddRow(P("Target", "Cible"), Row(goalValue, 90, goalUnit), ref y);
      AddRow(P("Spread over (days)", "Étaler sur (jours)"), deadlineDays, ref y);
      AddRow(P("Connection", "Connexion"), connectionCombo, ref y);
      AddRow("", activeHoursCheck, ref y);
      AddRow(P("Active hours", "Heures actives"), Row(hoursStart, 60, new Label { Text = P("to", "à"), AutoSize = true, Padding = new Padding(6, 6, 6, 0) }, hoursEnd, 60), ref y);
      AddRow("", rotateCheck, ref y);
      AddRow(P("Torrent folder", "Dossier des torrents"), FolderRow(torrentFolder, false), ref y);
      AddRow(P("Real files (optional)", "Vrais fichiers (option)"), FolderRow(realFolder, false), ref y);
      AddRow(P("Stagger (min)", "Décalage (min)"), Row(staggerMin, 60, new Label { Text = P("to", "à"), AutoSize = true, Padding = new Padding(6, 6, 6, 0) }, staggerMax, 60), ref y);
      AddRow(P("Max at once", "Max simultanés"), maxConcurrent, ref y);

      var start = new Button { Text = P("Start campaign", "Lancer la campagne"), Width = 130, Height = 30 };
      var save = new Button { Text = P("Save…", "Enregistrer…"), Width = 80, Height = 30 };
      var load = new Button { Text = P("Load…", "Charger…"), Width = 80, Height = 30 };
      var cancel = new Button { Text = P("Cancel", "Annuler"), Width = 80, Height = 30, DialogResult = DialogResult.Cancel };
      var bar = new FlowLayoutPanel {
        FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(8),
      };
      bar.Controls.AddRange(new Control[] { start, cancel, save, load });
      Controls.Add(bar);

      start.Click += (s, e) => { if (Validate2()) { Result = Build(); DialogResult = DialogResult.OK; Close(); } };
      save.Click += (s, e) => DoSave();
      load.Click += (s, e) => DoLoad();
      CancelButton = cancel;

      Apply(preset ?? new Campaign());
      Theme.Apply(this);
      Localization.Apply(this);
    }

    // ---- layout helpers ----
    private readonly Panel body = new Panel { Dock = DockStyle.Top };

    private void AddRow(string label, Control control, ref int y) {
      if (body.Parent == null) { body.Height = 460; Controls.Add(body); }
      if (!string.IsNullOrEmpty(label))
        body.Controls.Add(new Label { Text = label, Left = 16, Top = y + 4, Width = 140, AutoSize = false });
      control.Left = 164; control.Top = y;
      if (control.Width < 60 || control is ComboBox || control is TextBox) control.Width = 280;
      body.Controls.Add(control);
      y += control.Height + 12;
    }

    private static Control Row(params object[] items) {
      var p = new FlowLayoutPanel { AutoSize = true, Height = 26, WrapContents = false, Margin = new Padding(0) };
      for (var i = 0; i < items.Length; i++) {
        var c = (Control) items[i];
        if (i + 1 < items.Length && items[i + 1] is int w) { c.Width = w; i++; }
        p.Controls.Add(c);
      }
      return p;
    }

    private Control FolderRow(TextBox tb, bool _) {
      tb.Width = 220;
      var browse = new Button { Text = "…", Width = 34, Height = 24 };
      browse.Click += (s, e) => {
        using (var dlg = new FolderBrowserDialog()) if (dlg.ShowDialog() == DialogResult.OK) tb.Text = dlg.SelectedPath;
      };
      return Row(tb, 220, browse, 34);
    }

    private void UpdateGoalUnit() => goalUnit.Text = goalCombo.SelectedIndex == 1 ? P("ratio", "ratio") : P("GB", "Go");

    private bool Validate2() {
      if (!System.IO.Directory.Exists(torrentFolder.Text)) {
        MessageBox.Show(P("Pick a folder that contains your .torrent files.", "Choisissez un dossier contenant vos fichiers .torrent."), "Seedforger");
        return false;
      }
      if (!double.TryParse(goalValue.Text.Replace(',', '.'),
            System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v) || v <= 0) {
        MessageBox.Show(P("Enter a valid target value.", "Entrez une valeur cible valide."), "Seedforger");
        return false;
      }
      return true;
    }

    private Campaign Build() {
      double.TryParse(goalValue.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any,
        System.Globalization.CultureInfo.InvariantCulture, out var v);
      var ratio = goalCombo.SelectedIndex == 1;
      return new Campaign {
        Goal = ratio ? "ratio" : "upload",
        TargetRatio = ratio ? v : 2.0,
        UploadGoalGB = ratio ? 100 : v,
        DeadlineHours = (double) deadlineDays.Value * 24,
        Connection = connectionCombo.SelectedItem?.ToString() ?? "",
        UseActiveHours = activeHoursCheck.Checked,
        ActiveHoursStart = (int) hoursStart.Value,
        ActiveHoursEnd = (int) hoursEnd.Value,
        RotateClient = rotateCheck.Checked,
        TorrentFolder = torrentFolder.Text,
        RealFileFolder = realFolder.Text,
        StaggerMinMinutes = (int) staggerMin.Value,
        StaggerMaxMinutes = (int) Math.Max(staggerMin.Value, staggerMax.Value),
        MaxConcurrent = (int) maxConcurrent.Value,
      };
    }

    private void Apply(Campaign c) {
      goalCombo.SelectedIndex = c.Goal == "ratio" ? 1 : 0;
      goalValue.Text = (c.Goal == "ratio" ? c.TargetRatio : c.UploadGoalGB).ToString(System.Globalization.CultureInfo.InvariantCulture);
      deadlineDays.Value = Clamp((decimal) (c.DeadlineHours / 24.0), 0, 3650);
      var idx = connectionCombo.Items.IndexOf(c.Connection);
      connectionCombo.SelectedIndex = idx >= 0 ? idx : (connectionCombo.Items.Count > 0 ? 0 : -1);
      activeHoursCheck.Checked = c.UseActiveHours;
      hoursStart.Value = Clamp(c.ActiveHoursStart, 0, 24);
      hoursEnd.Value = Clamp(c.ActiveHoursEnd, 0, 24);
      rotateCheck.Checked = c.RotateClient;
      torrentFolder.Text = c.TorrentFolder;
      realFolder.Text = c.RealFileFolder;
      staggerMin.Value = Clamp(c.StaggerMinMinutes, 0, 600);
      staggerMax.Value = Clamp(c.StaggerMaxMinutes, 0, 600);
      maxConcurrent.Value = Clamp(c.MaxConcurrent, 1, 100);
      UpdateGoalUnit();
    }

    private static decimal Clamp(decimal v, decimal lo, decimal hi) => Math.Min(hi, Math.Max(lo, v));

    private void DoSave() {
      if (!Validate2()) return;
      using (var dlg = new SaveFileDialog { Filter = P("Campaign (*.json)|*.json", "Campagne (*.json)|*.json"), FileName = "campaign.json" })
        if (dlg.ShowDialog() == DialogResult.OK) Build().Save(dlg.FileName);
    }

    private void DoLoad() {
      using (var dlg = new OpenFileDialog { Filter = P("Campaign (*.json)|*.json|All files (*.*)|*.*", "Campagne (*.json)|*.json|Tous les fichiers (*.*)|*.*") }) {
        if (dlg.ShowDialog() != DialogResult.OK) return;
        var c = Campaign.Load(dlg.FileName);
        if (c == null) { MessageBox.Show(P("Couldn't read that campaign file.", "Impossible de lire ce fichier de campagne."), "Seedforger"); return; }
        Apply(c);
      }
    }
  }
}
