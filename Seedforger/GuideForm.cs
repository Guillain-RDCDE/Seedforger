using System;
using System.Drawing;
using System.Windows.Forms;

namespace Seedforger {

  /// <summary>The structured result of a dry-run seeder announce.</summary>
  internal sealed class AnnounceProbe {
    public bool GotResponse;
    public bool Accepted;
    public string FailureReason;   // set when the tracker rejected the announce
    public string Error;           // set on a transport/decoding failure
    public int Seeders = -1;       // "complete"
    public int Leechers = -1;      // "incomplete"
    public int Interval = -1;
  }

  /// <summary>
  /// A step-by-step "newbie mode": load a torrent, ask the tracker whether it will
  /// work (as a seeder), read the swarm, and loop until a usable torrent is found —
  /// then set believable defaults and (optionally) start. It automates exactly the
  /// manual verification a first-timer would otherwise have to reason through.
  /// </summary>
  internal sealed class GuideForm : Form {

    private enum Step { Welcome, Choose, Analyze, Connection, Ready }

    private readonly MainForm owner;
    private RM Rm => owner.CurrentRM;

    private Step step = Step.Welcome;
    private AnnounceProbe probe;
    private bool probing;
    private Label emphasise; // re-coloured after Theme.Apply, which flattens label colours

    private readonly Label title = new Label { AutoSize = false, Font = new Font("Segoe UI Semibold", 13f, FontStyle.Bold), Dock = DockStyle.Top, Height = 34 };
    private readonly Label subtitle = new Label { AutoSize = false, Dock = DockStyle.Top, Height = 44 };
    private readonly Panel body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4, 6, 4, 6) };
    private readonly Button backBtn = new Button { Text = "Back", Width = 90, Height = 30 };
    private readonly Button nextBtn = new Button { Text = "Next", Width = 150, Height = 30, Name = "StartButton" };
    private readonly Button cancelBtn = new Button { Text = "Close", Width = 90, Height = 30, DialogResult = DialogResult.Cancel };

    private readonly ComboBox connectionCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };

    internal GuideForm(MainForm owner) {
      this.owner = owner;
      Text = "Guided setup";
      FormBorderStyle = FormBorderStyle.FixedDialog;
      MaximizeBox = false; MinimizeBox = false;
      StartPosition = FormStartPosition.CenterParent;
      ClientSize = new Size(540, 470);
      try { Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath); } catch { }

      var header = new Panel { Dock = DockStyle.Top, Height = 82, Padding = new Padding(18, 12, 18, 0) };
      header.Controls.Add(subtitle);
      header.Controls.Add(title);

      var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(18, 0, 18, 0) };
      host.Controls.Add(body);

      var bar = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Bottom, Height = 50, Padding = new Padding(12, 8, 12, 8) };
      bar.Controls.AddRange(new Control[] { nextBtn, cancelBtn, backBtn });

      Controls.Add(host);
      Controls.Add(bar);
      Controls.Add(header);

      foreach (var p in ConnectionProfiles.All) connectionCombo.Items.Add(p.Name);
      if (connectionCombo.Items.Count > 0) connectionCombo.SelectedIndex = Math.Min(4, connectionCombo.Items.Count - 1);

      backBtn.Click += (s, e) => GoBack();
      nextBtn.Click += (s, e) => GoNext();
      CancelButton = cancelBtn;

      Render();
      Theme.Apply(this);
      Localization.Apply(this);
    }

    // ---- navigation ----

    private void GoBack() {
      switch (step) {
        case Step.Choose: step = Step.Welcome; break;
        case Step.Analyze: step = Step.Choose; break;
        case Step.Connection: step = Step.Analyze; break;
        case Step.Ready: step = Step.Connection; break;
      }
      Render();
    }

    private void GoNext() {
      switch (step) {
        case Step.Welcome: step = Step.Choose; break;
        case Step.Choose:
          if (Rm == null || !Rm.HasTorrentLoaded) { Warn("Load a .torrent first."); return; }
          step = Step.Analyze; break;
        case Step.Analyze:
          if (probe == null || !probe.Accepted || probe.Leechers <= 0) return; // gated
          step = Step.Connection; break;
        case Step.Connection: step = Step.Ready; break;
        case Step.Ready:
          Finish();
          return;
      }
      Render();
    }

    private void Finish() {
      try {
        // Apply the connection profile first (it sets an upload cap AND a download
        // rate), then lock in seeder mode so download is forced back to 0 — a
        // newbie must never end up uploading AND downloading.
        owner.ApplyConnectionProfileByName(connectionCombo.SelectedItem?.ToString() ?? "");
        Rm?.ConfigureForSeeding();
        AppOptions.RealisticSpeed = true; // soft ramp-up: speeds climb from zero like a real client
        AppOptions.SwarmAware = true;     // only claim upload in proportion to real demand
        Rm?.CampaignStart();
      }
      catch (Exception ex) { Warn("Couldn't start: " + ex.Message); return; }
      DialogResult = DialogResult.OK;
      Close();
    }

    private static void Warn(string msg) => MessageBox.Show(msg, "Seedforger");

    // ---- rendering ----

    private void Render() {
      emphasise = null;
      body.Controls.Clear();
      backBtn.Enabled = step != Step.Welcome;
      nextBtn.Text = step == Step.Ready ? "Start seeding now" : "Next";
      nextBtn.Enabled = true;

      switch (step) {
        case Step.Welcome: RenderWelcome(); break;
        case Step.Choose: RenderChoose(); break;
        case Step.Analyze: RenderAnalyze(); break;
        case Step.Connection: RenderConnection(); break;
        case Step.Ready: RenderReady(); break;
      }
      Theme.Apply(this);
      // Theme.Apply flattens every label to the body text colour; restore the
      // warning's red afterwards so it actually reads as a warning.
      if (emphasise != null) emphasise.ForeColor = Color.FromArgb(0xE0, 0x3E, 0x3E);
    }

    private Label Para(string text, int top, int height = 60) =>
      new Label { Text = text, Left = 2, Top = top, Width = body.Width > 20 ? body.Width - 12 : 480, Height = height, AutoSize = false };

    private void RenderWelcome() {
      title.Text = "Build ratio, believably";
      subtitle.Text = "This guide sets you up step by step and checks each choice against the tracker.";
      body.Controls.Add(Para(
        "Here's what we'll do together:\n\n" +
        "   1.  Pick a torrent you already have.\n" +
        "   2.  Ask the tracker — as a seeder — whether it will accept you.\n" +
        "   3.  Read the swarm: only torrents with people downloading can earn you upload.\n" +
        "   4.  Set a believable speed for your connection.\n" +
        "   5.  Start — and let it seed.\n\n" +
        "Reminder: this is an educational tool. Faking ratio breaks most private trackers' rules and can get you banned. Use it only where you're allowed to.", 0, 300));
    }

    private void RenderChoose() {
      title.Text = "1 · Choose your torrent";
      subtitle.Text = "Pick a .torrent you actually have — ideally one that's popular right now.";

      var browse = new Button { Text = "Browse for a .torrent…", Left = 2, Top = 8, Width = 200, Height = 32, Name = "GhostButton" };
      var status = new Label { Left = 2, Top = 52, Width = 500, Height = 90, AutoSize = false };
      status.Text = Rm != null && Rm.HasTorrentLoaded
        ? "Loaded:  " + Rm.TorrentDisplayName
        : "No torrent loaded yet.";

      browse.Click += (s, e) => {
        using (var dlg = new OpenFileDialog { Filter = "Torrent file (*.torrent)|*.torrent", Title = "Choose a .torrent you have" }) {
          if (dlg.ShowDialog() != DialogResult.OK) return;
          try {
            Rm.LoadTorrentFileInfo(dlg.FileName);
            probe = null; // a new torrent invalidates the previous analysis
            status.Text = "Loaded:  " + Rm.TorrentDisplayName + "\n\nGreat — hit Next to check it against the tracker.";
          }
          catch (Exception ex) { status.Text = "Couldn't read that .torrent: " + ex.Message; }
          Theme.Apply(this);
        }
      };

      body.Controls.Add(browse);
      body.Controls.Add(status);
      body.Controls.Add(Para("Tip: an old, fully-seeded torrent has nobody to upload to — you'd gain nothing. We'll catch that in the next step.", 150, 60));
    }

    private void RenderAnalyze() {
      title.Text = "2 · Ask the tracker";
      subtitle.Text = "We send one seeder announce and read the answer — nothing is faked yet.";
      nextBtn.Enabled = probe != null && probe.Accepted && probe.Leechers > 0;

      var analyzeBtn = new Button { Text = probing ? "Checking…" : "Analyze this torrent", Left = 2, Top = 8, Width = 200, Height = 34, Name = "StartButton", Enabled = !probing };
      var result = new Label { Left = 2, Top = 54, Width = 500, Height = 230, AutoSize = false };
      result.Text = DescribeProbe();

      analyzeBtn.Click += (s, e) => {
        if (probing || Rm == null) return;
        probing = true;
        Render();
        Rm.ProbeAsSeeder(p => {
          probe = p;
          probing = false;
          if (step == Step.Analyze) Render();
        });
      };

      body.Controls.Add(analyzeBtn);
      body.Controls.Add(result);
    }

    private string DescribeProbe() {
      if (probing) return "Talking to the tracker…";
      if (probe == null) return "Click “Analyze this torrent”.\n\nWe'll announce as a complete seeder (so the tracker sees you as having the whole file) and report exactly what it says back.";
      if (!string.IsNullOrEmpty(probe.Error))
        return "⚠  Couldn't reach the tracker:\n" + probe.Error + "\n\nCheck your connection and try again.";
      if (!string.IsNullOrEmpty(probe.FailureReason)) {
        var isRatio = probe.FailureReason.ToLowerInvariant().Contains("ratio");
        return "⛔  The tracker rejected the announce:\n\n     " + probe.FailureReason + "\n\n" +
          (isRatio
            ? "It's blocking you even as a seeder — this account can't announce at all until the ratio is raised another way (bonus points, freeleech). Faking upload by announce is a dead end here."
            : "Make sure this torrent is registered to your account and the passkey is valid.");
      }
      if (probe.Leechers <= 0)
        return "⚠  Accepted — but this torrent has " + probe.Seeders + " seeders and 0 leechers.\n\n" +
          "Nobody is downloading it, so there's no one to upload to: you'd gain nothing (and claiming otherwise is exactly what gets flagged).\n\n" +
          "Go Back and pick a torrent that people are downloading right now.";
      return "✅  Perfect — this one will work.\n\n" +
        "     Leechers (demand):  " + probe.Leechers + "\n" +
        "     Seeders (competition):  " + probe.Seeders + "\n" +
        (probe.Interval > 0 ? "     Announce interval:  " + (probe.Interval / 60) + " min\n" : "") +
        "\nThe tracker accepted you as a seeder and there are people to feed. Hit Next.";
    }

    private void RenderConnection() {
      title.Text = "3 · Your connection";
      subtitle.Text = "This keeps your seeding speed physically believable.";
      body.Controls.Add(new Label { Text = "Connection profile", Left = 2, Top = 12, Width = 140, AutoSize = false });
      connectionCombo.Left = 150; connectionCombo.Top = 8;
      body.Controls.Add(connectionCombo);
      body.Controls.Add(Para(
        "Pick the line closest to yours. Seedforger caps and shapes your reported speed to match, and — with swarm-aware speeds on — only claims upload in proportion to the real demand it just measured.\n\n" +
        "We'll also report as a complete seeder and never claim downloads, so every byte helps your ratio.", 52, 160));
    }

    private void RenderReady() {
      title.Text = "4 · Ready to seed";
      subtitle.Text = "Everything checks out. Here's the plan.";
      var name = Rm != null ? Rm.TorrentDisplayName : "";
      body.Controls.Add(Para(
        "Torrent:   " + name + "\n" +
        "Swarm:   " + (probe?.Leechers ?? 0) + " leechers · " + (probe?.Seeders ?? 0) + " seeders\n" +
        "Connection:   " + (connectionCombo.SelectedItem?.ToString() ?? "") + "\n\n" +
        "Mode:   complete seeder (Finished 100%), download forced to 0, swarm-aware on.\n" +
        "Speed ramps up gently from zero, like a real client — not a flat blast.", 0, 120));

      var warn = new Label {
        Left = 2, Top = 132, Width = body.Width > 20 ? body.Width - 12 : 480, Height = 110, AutoSize = false,
        ForeColor = Color.FromArgb(0xE0, 0x3E, 0x3E),
        Text = "⚠  Only continue if you actually HAVE this file.\n\n" +
          "This swarm has real downloaders — and a private tracker seeds spies among them that will request real pieces. If you claim to seed a file you don't have, you can't deliver, and that's the surest way to get caught. Seed only what you truly possess. Don't run the same torrent in qBittorrent at the same time.",
      };
      body.Controls.Add(warn);
      emphasise = warn;
    }
  }
}
