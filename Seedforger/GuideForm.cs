using System;
using System.Collections.Generic;
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
  /// Fully localized (EN/FR) via <see cref="Localization.Pick"/>.
  /// </summary>
  internal sealed class GuideForm : Form {

    private enum Step { Welcome, HaveIt, Choose, Analyze, Connection, Ready }

    private static readonly Color Red = Color.FromArgb(0xE0, 0x3E, 0x3E);
    private static readonly Color Green = Color.FromArgb(0x1F, 0xA9, 0x6B);

    private readonly RM rm;
    private readonly Action<string> applyConnectionProfile;
    private RM Rm => rm;

    private Step step = Step.Welcome;
    private AnnounceProbe probe;
    private bool probing;
    // Re-applied after Theme.Apply, which flattens every label to the body colour.
    private readonly List<(Label lbl, Color col)> recolours = new List<(Label, Color)>();

    private readonly Label title = new Label { AutoSize = false, Font = new Font("Segoe UI Semibold", 13f, FontStyle.Bold), Dock = DockStyle.Top, Height = 34 };
    private readonly Label subtitle = new Label { AutoSize = false, Dock = DockStyle.Top, Height = 44 };
    private readonly Panel body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4, 6, 4, 6) };
    private readonly Button backBtn = new Button { Width = 90, Height = 30 };
    private readonly Button nextBtn = new Button { Width = 150, Height = 30, Name = "StartButton" };
    private readonly Button cancelBtn = new Button { Width = 90, Height = 30, DialogResult = DialogResult.Cancel };

    private readonly ComboBox connectionCombo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };

    private static string P(string en, string fr) => Localization.Pick(en, fr);

    internal GuideForm(RM rm, Action<string> applyConnectionProfile) {
      this.rm = rm;
      this.applyConnectionProfile = applyConnectionProfile;
      Text = P("Guided setup", "Assistant guidé");
      FormBorderStyle = FormBorderStyle.FixedDialog;
      MaximizeBox = false; MinimizeBox = false;
      StartPosition = FormStartPosition.CenterParent;
      ClientSize = new Size(540, 508);
      try { Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath); } catch { }

      backBtn.Text = P("Back", "Retour");
      cancelBtn.Text = P("Close", "Fermer");

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
        case Step.HaveIt: step = Step.Welcome; break;
        case Step.Choose: step = Step.HaveIt; break;
        case Step.Analyze: step = Step.Choose; break;
        case Step.Connection: step = Step.Analyze; break;
        case Step.Ready: step = Step.Connection; break;
      }
      Render();
    }

    private void GoNext() {
      switch (step) {
        case Step.Welcome: step = Step.HaveIt; break;
        case Step.HaveIt: step = Step.Choose; break;
        case Step.Choose:
          if (Rm == null || !Rm.HasTorrentLoaded) { Warn(P("Load a .torrent first.", "Chargez d'abord un .torrent.")); return; }
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
        applyConnectionProfile?.Invoke(connectionCombo.SelectedItem?.ToString() ?? "");
        Rm?.ConfigureForSeeding();
        AppOptions.RealisticSpeed = true; // soft ramp-up: speeds climb from zero like a real client
        AppOptions.SwarmAware = true;     // only claim upload in proportion to real demand
        Rm?.CampaignStart();
      }
      catch (Exception ex) { Warn(P("Couldn't start: ", "Impossible de démarrer : ") + ex.Message); return; }
      DialogResult = DialogResult.OK;
      Close();
    }

    private static void Warn(string msg) => MessageBox.Show(msg, "Seedforger");

    // ---- rendering ----

    private void Render() {
      recolours.Clear();
      body.Controls.Clear();
      backBtn.Enabled = step != Step.Welcome;
      nextBtn.Text = step == Step.Ready ? P("Start seeding now", "Démarrer le seed") : P("Next", "Suivant");
      nextBtn.Enabled = true;

      switch (step) {
        case Step.Welcome: RenderWelcome(); break;
        case Step.HaveIt: RenderHaveIt(); break;
        case Step.Choose: RenderChoose(); break;
        case Step.Analyze: RenderAnalyze(); break;
        case Step.Connection: RenderConnection(); break;
        case Step.Ready: RenderReady(); break;
      }
      Theme.Apply(this);
      // Theme.Apply flattens every label to the body text colour; restore the
      // accent/warning colours afterwards so they actually stand out.
      foreach (var (lbl, col) in recolours) if (lbl != null && !lbl.IsDisposed) lbl.ForeColor = col;
    }

    private Label Para(string text, int top, int height = 60) =>
      new Label { Text = text, Left = 2, Top = top, Width = body.Width > 20 ? body.Width - 12 : 480, Height = height, AutoSize = false };

    private void RenderWelcome() {
      title.Text = P("Build ratio, believably", "Gagner du ratio, de façon crédible");
      subtitle.Text = P("This guide sets you up step by step and checks each choice against the tracker.",
                        "Ce guide vous configure étape par étape et vérifie chaque choix auprès du tracker.");
      body.Controls.Add(Para(P(
        "Here's what we'll do together:\n\n" +
        "   1.  Pick a torrent you already have.\n" +
        "   2.  Ask the tracker — as a seeder — whether it will accept you.\n" +
        "   3.  Read the swarm: only torrents with people downloading can earn you upload.\n" +
        "   4.  Set a believable speed for your connection.\n" +
        "   5.  Start — and let it seed.\n\n" +
        "Reminder: this is an educational tool. Faking ratio breaks most private trackers' rules and can get you banned. Use it only where you're allowed to.",
        "Voici ce que nous allons faire ensemble :\n\n" +
        "   1.  Choisir un torrent que vous avez déjà.\n" +
        "   2.  Demander au tracker — en tant que seeder — s'il vous accepte.\n" +
        "   3.  Lire le swarm : seuls les torrents avec des gens qui téléchargent rapportent de l'upload.\n" +
        "   4.  Régler une vitesse crédible pour votre connexion.\n" +
        "   5.  Démarrer — et laisser seeder.\n\n" +
        "Rappel : c'est un outil éducatif. Falsifier son ratio enfreint les règles de la plupart des trackers privés et peut mener au bannissement. À n'utiliser que là où vous en avez le droit."), 0, 300));
    }

    private void RenderHaveIt() {
      title.Text = P("Before anything — do you have it?", "Avant tout — l'avez-vous vraiment ?");
      subtitle.Text = P("The one rule that keeps you safe. Read it.", "La seule règle qui vous protège. Lisez-la.");

      var q = new Label {
        Text = "?", Left = 0, Top = 2, Width = body.Width > 20 ? body.Width - 12 : 480, Height = 82,
        Font = new Font("Segoe UI", 62f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter,
      };
      body.Controls.Add(q);
      recolours.Add((q, Green));

      var lead = new Label {
        Left = 2, Top = 90, Width = body.Width > 20 ? body.Width - 12 : 480, Height = 44, AutoSize = false,
        Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold),
        Text = P("Ideally, you must have actually downloaded this file once — for real, through this tracker.",
                 "Idéalement, vous devez avoir réellement téléchargé ce fichier une fois — pour de vrai, via ce tracker."),
      };
      body.Controls.Add(lead);

      body.Controls.Add(Para(P(
        "Why it matters:\n\n" +
        "   •  When you really download a file, the tracker records that you completed it — it knows you legitimately have the data.\n\n" +
        "   •  The swarm contains monitoring peers (spies the tracker plants). They connect to you and request real pieces.\n\n" +
        "   •  Claim to seed a file you never downloaded and you have nothing to send back — you get caught, and it can mean a ban.\n\n" +
        "The guide checks the swarm for you; it can't check your drive. That part is on you.",
        "Pourquoi c'est important :\n\n" +
        "   •  Quand vous téléchargez vraiment un fichier, le tracker enregistre que vous l'avez complété — il sait que vous avez légitimement les données.\n\n" +
        "   •  Le swarm contient des pairs de surveillance (des espions placés par le tracker). Ils se connectent à vous et demandent de vrais morceaux.\n\n" +
        "   •  Prétendez seeder un fichier que vous n'avez jamais téléchargé, et vous n'avez rien à renvoyer — vous êtes repéré, et ça peut être un ban.\n\n" +
        "Le guide vérifie le swarm pour vous ; il ne peut pas vérifier votre disque. Cette partie-là dépend de vous."), 140, 232));
    }

    private void RenderChoose() {
      title.Text = P("1 · Choose your torrent", "1 · Choisissez votre torrent");
      subtitle.Text = P("Pick a .torrent you actually have — ideally one that's popular right now.",
                        "Choisissez un .torrent que vous avez vraiment — idéalement populaire en ce moment.");

      var browse = new Button { Text = P("Browse for a .torrent…", "Parcourir un .torrent…"), Left = 2, Top = 8, Width = 200, Height = 32, Name = "GhostButton" };
      var status = new Label { Left = 2, Top = 52, Width = 500, Height = 90, AutoSize = false };
      status.Text = Rm != null && Rm.HasTorrentLoaded
        ? P("Loaded:  ", "Chargé :  ") + Rm.TorrentDisplayName
        : P("No torrent loaded yet.", "Aucun torrent chargé pour l'instant.");

      browse.Click += (s, e) => {
        using (var dlg = new OpenFileDialog { Filter = P("Torrent file (*.torrent)|*.torrent", "Fichier torrent (*.torrent)|*.torrent"), Title = P("Choose a .torrent you have", "Choisissez un .torrent que vous avez") }) {
          if (dlg.ShowDialog() != DialogResult.OK) return;
          try {
            Rm.LoadTorrentFileInfo(dlg.FileName);
            probe = null; // a new torrent invalidates the previous analysis
            status.Text = P("Loaded:  ", "Chargé :  ") + Rm.TorrentDisplayName + P("\n\nGreat — hit Next to check it against the tracker.", "\n\nParfait — cliquez sur Suivant pour le vérifier auprès du tracker.");
          }
          catch (Exception ex) { status.Text = P("Couldn't read that .torrent: ", "Impossible de lire ce .torrent : ") + ex.Message; }
          Theme.Apply(this);
        }
      };

      body.Controls.Add(browse);
      body.Controls.Add(status);
      body.Controls.Add(Para(P(
        "Tip: an old, fully-seeded torrent has nobody to upload to — you'd gain nothing. We'll catch that in the next step.",
        "Astuce : un vieux torrent entièrement seedé n'a personne à qui envoyer — vous ne gagneriez rien. On le détectera à l'étape suivante."), 150, 60));
    }

    private void RenderAnalyze() {
      title.Text = P("2 · Ask the tracker", "2 · Interroger le tracker");
      subtitle.Text = P("We send one seeder announce and read the answer — nothing is faked yet.",
                        "On envoie une annonce de seeder et on lit la réponse — rien n'est encore falsifié.");
      nextBtn.Enabled = probe != null && probe.Accepted && probe.Leechers > 0;

      var analyzeBtn = new Button { Text = probing ? P("Checking…", "Vérification…") : P("Analyze this torrent", "Analyser ce torrent"), Left = 2, Top = 8, Width = 200, Height = 34, Name = "StartButton", Enabled = !probing };
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
      if (probing) return P("Talking to the tracker…", "Communication avec le tracker…");
      if (probe == null) return P(
        "Click “Analyze this torrent”.\n\nWe'll announce as a complete seeder (so the tracker sees you as having the whole file) and report exactly what it says back.",
        "Cliquez sur « Analyser ce torrent ».\n\nOn s'annonce comme seeder complet (le tracker vous voit avec le fichier entier) et on rapporte exactement sa réponse.");
      if (!string.IsNullOrEmpty(probe.Error))
        return P("⚠  Couldn't reach the tracker:\n", "⚠  Impossible de joindre le tracker :\n") + probe.Error + P("\n\nCheck your connection and try again.", "\n\nVérifiez votre connexion et réessayez.");
      if (!string.IsNullOrEmpty(probe.FailureReason)) {
        var isRatio = probe.FailureReason.ToLowerInvariant().Contains("ratio");
        return P("⛔  The tracker rejected the announce:\n\n     ", "⛔  Le tracker a rejeté l'annonce :\n\n     ") + probe.FailureReason + "\n\n" +
          (isRatio
            ? P("It's blocking you even as a seeder — this account can't announce at all until the ratio is raised another way (bonus points, freeleech). Faking upload by announce is a dead end here.",
                "Il vous bloque même en seeder — ce compte ne peut pas s'annoncer tant que le ratio n'est pas remonté autrement (points bonus, freeleech). Falsifier l'upload par annonce est une impasse ici.")
            : P("Make sure this torrent is registered to your account and the passkey is valid.",
                "Vérifiez que ce torrent est bien associé à votre compte et que la passkey est valide."));
      }
      if (probe.Leechers <= 0)
        return P("⚠  Accepted — but this torrent has ", "⚠  Accepté — mais ce torrent a ") + probe.Seeders + P(" seeders and 0 leechers.\n\n", " seeders et 0 leecher.\n\n") +
          P("Nobody is downloading it, so there's no one to upload to: you'd gain nothing (and claiming otherwise is exactly what gets flagged).\n\nGo Back and pick a torrent that people are downloading right now.",
            "Personne ne le télécharge, donc personne à qui envoyer : vous ne gagneriez rien (et prétendre le contraire, c'est exactement ce qui se fait repérer).\n\nRevenez en arrière et choisissez un torrent que des gens téléchargent en ce moment.");
      return P("✅  Perfect — this one will work.\n\n", "✅  Parfait — celui-ci va marcher.\n\n") +
        P("     Leechers (demand):  ", "     Leechers (demande) :  ") + probe.Leechers + "\n" +
        P("     Seeders (competition):  ", "     Seeders (concurrence) :  ") + probe.Seeders + "\n" +
        (probe.Interval > 0 ? P("     Announce interval:  ", "     Intervalle d'annonce :  ") + (probe.Interval / 60) + P(" min\n", " min\n") : "") +
        P("\nThe tracker accepted you as a seeder and there are people to feed. Hit Next.",
          "\nLe tracker vous a accepté comme seeder et il y a des gens à nourrir. Cliquez sur Suivant.");
    }

    private void RenderConnection() {
      title.Text = P("3 · Your connection", "3 · Votre connexion");
      subtitle.Text = P("This keeps your seeding speed physically believable.", "Ceci garde votre vitesse de seed physiquement crédible.");
      body.Controls.Add(new Label { Text = P("Connection profile", "Profil de connexion"), Left = 2, Top = 12, Width = 140, AutoSize = false });
      connectionCombo.Left = 150; connectionCombo.Top = 8;
      body.Controls.Add(connectionCombo);
      body.Controls.Add(Para(P(
        "Pick the line closest to yours. Seedforger caps and shapes your reported speed to match, and — with swarm-aware speeds on — only claims upload in proportion to the real demand it just measured.\n\n" +
        "We'll also report as a complete seeder and never claim downloads, so every byte helps your ratio.",
        "Choisissez la ligne la plus proche de la vôtre. Seedforger plafonne et modèle votre vitesse annoncée en conséquence, et — avec les vitesses selon la demande activées — n'annonce de l'upload qu'en proportion de la demande réelle mesurée.\n\n" +
        "On s'annonce aussi comme seeder complet, sans jamais prétendre télécharger, pour que chaque octet serve votre ratio."), 52, 160));
    }

    private void RenderReady() {
      title.Text = P("4 · Ready to seed", "4 · Prêt à seeder");
      subtitle.Text = P("Everything checks out. Here's the plan.", "Tout est bon. Voici le plan.");
      var name = Rm != null ? Rm.TorrentDisplayName : "";
      body.Controls.Add(Para(
        P("Torrent:   ", "Torrent :   ") + name + "\n" +
        P("Swarm:   ", "Swarm :   ") + (probe?.Leechers ?? 0) + P(" leechers · ", " leechers · ") + (probe?.Seeders ?? 0) + P(" seeders\n", " seeders\n") +
        P("Connection:   ", "Connexion :   ") + (connectionCombo.SelectedItem?.ToString() ?? "") + "\n\n" +
        P("Mode:   complete seeder (Finished 100%), download forced to 0, swarm-aware on.\n", "Mode :   seeder complet (Terminé 100 %), téléchargement forcé à 0, selon la demande.\n") +
        P("Speed ramps up gently from zero, like a real client — not a flat blast.",
          "La vitesse monte doucement depuis zéro, comme un vrai client — pas un débit plat."), 0, 120));

      var warn = new Label {
        Left = 2, Top = 132, Width = body.Width > 20 ? body.Width - 12 : 480, Height = 110, AutoSize = false,
        ForeColor = Color.FromArgb(0xE0, 0x3E, 0x3E),
        Text = P(
          "⚠  Only continue if you actually HAVE this file.\n\n" +
          "This swarm has real downloaders — and a private tracker seeds spies among them that will request real pieces. If you claim to seed a file you don't have, you can't deliver, and that's the surest way to get caught. Seed only what you truly possess. Don't run the same torrent in qBittorrent at the same time.",
          "⚠  Ne continuez que si vous AVEZ vraiment ce fichier.\n\n" +
          "Ce swarm a de vrais téléchargeurs — et un tracker privé y place des espions qui demanderont de vrais morceaux. Si vous prétendez seeder un fichier que vous n'avez pas, vous ne pouvez pas livrer, et c'est le moyen le plus sûr de se faire prendre. Ne seedez que ce que vous possédez vraiment. Ne lancez pas le même torrent dans qBittorrent en même temps."),
      };
      body.Controls.Add(warn);
      recolours.Add((warn, Red));
    }
  }
}
