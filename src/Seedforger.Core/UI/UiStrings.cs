using System;
using System.Collections.Generic;

namespace Seedforger.UI {

  /// <summary>
  /// English/French strings for the interface chrome. Kept as a small self-contained
  /// table so the menu bar, group boxes, status bar, tray and dialogs follow the
  /// language toggle at runtime (both the WinForms and the Avalonia front-ends).
  /// </summary>
  internal static class UiStrings {

    /// <summary>The interface language. Set from Settings at launch, switched from the menu.</summary>
    internal static Language Language { get; set; } = Language.English;

    internal static bool IsFrench => Language == Language.French;

    /// <summary>Parses the two-letter code stored in settings.json.</summary>
    internal static Language ParseLanguage(string code) =>
      string.Equals(code, "fr", StringComparison.OrdinalIgnoreCase) ? Language.French : Language.English;

    internal static string Code(Language lang) => lang == Language.French ? "fr" : "en";

    /// <summary>Picks a string for the current language — for dynamic text and
    /// long paragraphs that don't belong in the key table.</summary>
    internal static string Pick(string en, string fr) => IsFrench ? fr : en;

    private static readonly Dictionary<string, (string en, string fr)> Map = new Dictionary<string, (string, string)> {
      // menu bar
      ["menu.file"]         = ("File", "Fichier"),
      ["menu.run"]          = ("Run", "Exécution"),
      ["menu.tools"]        = ("Tools", "Outils"),
      ["menu.settings"]     = ("Settings", "Réglages"),
      ["menu.help"]         = ("Help", "Aide"),
      // menus — file
      ["menu.load_torrent"] = ("Load a .torrent…", "Charger un .torrent…"),
      ["menu.open_magnet"]  = ("Open a magnet link…", "Ouvrir un lien magnet…"),
      ["menu.exit"]         = ("Exit", "Quitter"),
      // menus — run
      ["menu.test_announce"]= ("Test announce (dry run)", "Tester l'annonce (à blanc)"),
      ["menu.announce_now"] = ("Announce now", "Annoncer maintenant"),
      ["menu.serve_real"]   = ("Serve a real file (advanced)…", "Servir un vrai fichier (avancé)…"),
      // menus — tools
      ["menu.guided"]       = ("Guided setup…", "Assistant guidé…"),
      ["menu.campaigns"]    = ("Campaigns…", "Campagnes…"),
      ["menu.stop_campaign"] = ("Stop the campaign", "Arrêter la campagne"),
      ["menu.live_graph"]   = ("Live graph…", "Graphe en direct…"),
      ["menu.advanced_settings"] = ("Advanced settings…", "Réglages avancés…"),
      // menus — settings
      ["menu.realistic"]    = ("Realistic speed (ramp-up)", "Vitesse réaliste (montée progressive)"),
      ["menu.swarm"]        = ("Swarm-aware speeds", "Vitesses selon la demande"),
      ["menu.randomize"]    = ("Randomize client on start", "Client aléatoire au démarrage"),
      ["menu.connection"]   = ("Connection profile", "Profil de connexion"),
      ["menu.active_hours"] = ("Active hours…", "Heures actives…"),
      ["menu.minimize_tray"]= ("Minimize to the notification area", "Réduire dans la zone de notification"),
      ["menu.close_minimizes"] = ("Close button minimizes instead of quitting",
                                  "La croix réduit au lieu de quitter"),
      ["menu.tray_balloon"] = ("Show the notification-area hint", "Afficher la bulle de notification"),
      ["menu.language"]     = ("Language", "Langue"),
      // menus — help
      ["menu.about"]        = ("About Seedforger…", "À propos de Seedforger…"),
      ["menu.open_repo"]    = ("Seedforger on GitHub", "Seedforger sur GitHub"),
      // group boxes
      ["grp.torrent"]       = ("Torrent", "Torrent"),
      ["grp.status"]        = ("Status", "État"),
      ["grp.log"]           = ("Log", "Journal"),
      // form labels
      ["lbl.file"]          = ("File:", "Fichier :"),
      ["lbl.client"]        = ("Client:", "Client :"),
      ["lbl.version"]       = ("Version:", "Version :"),
      ["lbl.mode"]          = ("Mode:", "Mode :"),
      ["lbl.upload"]        = ("Upload speed (kB/s):", "Vitesse d'envoi (ko/s) :"),
      ["no_torrent"]        = ("No torrent loaded", "Aucun torrent chargé"),
      ["browse"]            = ("Browse…", "Parcourir…"),
      ["advanced"]          = ("Advanced…", "Avancé…"),
      ["mode.seeder"]       = ("Seeder (100 % — recommended)", "Seeder (100 % — recommandé)"),
      ["mode.leecher"]      = ("Leecher (0 %)", "Leecher (0 %)"),
      ["start_seeding"]     = ("Start seeding", "Démarrer le seed"),
      ["stop"]              = ("Stop", "Arrêter"),
      ["btn.test"]          = ("Test announce", "Tester l'annonce"),
      // status rows
      ["lbl.ratio"]         = ("Ratio:", "Ratio :"),
      ["lbl.uploaded"]      = ("Uploaded:", "Envoyé :"),
      ["lbl.downloaded"]    = ("Downloaded:", "Téléchargé :"),
      ["lbl.up_speed"]      = ("Upload speed:", "Vitesse d'envoi :"),
      ["lbl.swarm"]         = ("Seeders / leechers:", "Seeders / leechers :"),
      ["lbl.elapsed"]       = ("Elapsed:", "Durée :"),
      ["lbl.state"]         = ("State:", "État :"),
      ["seeding"]           = ("Seeding", "Seed en cours"),
      ["idle"]              = ("Idle", "Inactif"),
      ["status.client"]     = ("Client: {0}", "Client : {0}"),
      ["status.next"]       = ("Next announce in {0}", "Prochaine annonce dans {0}"),
      ["status.campaign"]   = ("Campaign: {0}/{1} active, {2} uploaded", "Campagne : {0}/{1} actifs, {2} envoyés"),
      ["state.busy"]        = ("Working…", "En cours…"),
      ["state.rejected"]    = ("Rejected by the tracker", "Rejeté par le tracker"),
      // log lines
      ["log.loaded"]        = ("Loaded \"{0}\" ({1}, {2} tracker(s)).", "Chargé « {0} » ({1}, {2} tracker(s))."),
      ["log.starting"]      = ("Starting \"{0}\" as {1} at {2} kB/s ({3}).", "Démarrage de « {0} » en {1} à {2} ko/s ({3})."),
      ["log.profile"]       = ("Connection profile {0}: {1} kB/s up, {2} kB/s down.", "Profil de connexion {0} : {1} ko/s en envoi, {2} ko/s en réception."),
      ["log.auto_stopped"]  = ("The run", "L'exécution"),
      ["log.probing"]       = ("Dry run: announcing once as a complete seeder…", "Test à blanc : une annonce en seeder complet…"),
      ["log.real_file"]     = ("Real file armed for the next start: {0}", "Vrai fichier prêt pour le prochain démarrage : {0}"),
      ["log.campaign_done"] = ("goal reached — campaign complete.", "objectif atteint — campagne terminée."),
      // dry-run results
      ["probe.accepted"]    = ("Accepted by the tracker — there are people to upload to.", "Accepté par le tracker — il y a des gens à qui envoyer."),
      ["probe.accepted_empty"] = ("Accepted by the tracker, but nobody is downloading: you would gain nothing.", "Accepté par le tracker, mais personne ne télécharge : vous ne gagneriez rien."),
      ["probe.rejected"]    = ("The tracker rejected the announce:", "Le tracker a rejeté l'annonce :"),
      ["probe.error"]       = ("Could not reach the tracker:", "Impossible de joindre le tracker :"),
      ["probe.swarm"]       = ("Seeders: {0}   Leechers: {1}   Interval: {2} s", "Seeders : {0}   Leechers : {1}   Intervalle : {2} s"),
      // tooltips
      ["tip.client"]        = ("The torrent application you pretend to be (e.g. qBittorrent). This is what the tracker sees.",
                               "L'application torrent dont vous prenez l'identité (ex. qBittorrent). C'est ce que voit le tracker."),
      ["tip.advanced"]      = ("Custom fingerprint and proxy", "Empreinte personnalisée et proxy"),
      ["tip.upload"]        = ("The upload speed to report, in kB/s. Keep it believable for your line.",
                               "La vitesse d'envoi annoncée, en ko/s. Restez crédible pour votre ligne."),
      ["tip.ratio"]         = ("Ratio = uploaded ÷ downloaded.\nA seeder downloads nothing, so the ratio is infinite — shown as “—”.\nIt becomes a real number only if you simulate some download.",
                               "Ratio = envoyé ÷ téléchargé.\nUn seeder ne télécharge rien, donc le ratio est infini — affiché « — ».\nIl devient un vrai nombre seulement si vous simulez du téléchargement."),
      ["tip.drop"]          = ("You can also drop a .torrent file anywhere on this window.",
                               "Vous pouvez aussi déposer un fichier .torrent n'importe où sur cette fenêtre."),
      // tray
      ["tray.restore"]      = ("Restore", "Restaurer"),
      ["tray.quit"]         = ("Quit", "Quitter"),
      ["tray.balloon_text"] = ("Still running — tucked away here. Double-click to reopen.",
                               "Toujours actif — rangé ici. Double-cliquez pour rouvrir."),
      // dialogs / prompts
      ["dlg.choose_torrent"]= ("Choose a .torrent", "Choisir un .torrent"),
      ["dlg.torrent_filter"]= ("Torrent file (*.torrent)|*.torrent", "Fichier torrent (*.torrent)|*.torrent"),
      ["dlg.read_error"]    = ("Couldn't read that .torrent: ", "Impossible de lire ce .torrent : "),
      ["dlg.no_torrent"]    = ("Load a .torrent first (File → Load a .torrent…).",
                               "Chargez d'abord un .torrent (Fichier → Charger un .torrent…)."),
      ["dlg.upload_bad"]    = ("Enter the upload speed as a whole number of kB/s (e.g. 1024).",
                               "Entrez la vitesse d'envoi en nombre entier de ko/s (ex. 1024)."),
      ["dlg.magnet_title"]  = ("Open a magnet link", "Ouvrir un lien magnet"),
      ["dlg.magnet_label"]  = ("Paste a magnet link:", "Collez un lien magnet :"),
      ["dlg.magnet_bad"]    = ("That is not a valid magnet link (it needs an xt=urn:btih: hash).", "Ce n'est pas un lien magnet valide (il lui faut un hash xt=urn:btih:)."),
      ["dlg.magnet_size"]   = ("A magnet carries no size. Total size of the content, in MB:", "Un magnet ne porte pas la taille. Taille totale du contenu, en Mo :"),
      ["dlg.magnet_size_bad"] = ("Enter the size as a positive number of MB.", "Entrez la taille en Mo, nombre positif."),
      ["dlg.serve_title"]   = ("Pick the downloaded file that matches this torrent", "Choisissez le fichier téléchargé correspondant à ce torrent"),
      ["dlg.hours_title"]   = ("Active hours", "Heures actives"),
      ["dlg.hours_label"]   = ("Seed only between these hours (0-24), e.g. 8-24 or 22-6.\nLeave empty for 24/7:",
                               "Seed uniquement entre ces heures (0-24), ex. 8-24 ou 22-6.\nLaissez vide pour 24/7 :"),
      ["dlg.hours_bad"]     = ("Use a format like 8-24 or 22-6.", "Utilisez un format comme 8-24 ou 22-6."),
      ["dlg.update_title"]  = ("update available", "mise à jour disponible"),
      ["dlg.update_text"]   = ("A new version {0} is available — you have v{1}.\n\nOpen the download page?",
                               "Une nouvelle version {0} est disponible — vous avez la v{1}.\n\nOuvrir la page de téléchargement ?"),
      ["dlg.about_text"]    = ("{0} v{1}\n\nFake your BitTorrent upload/download stats on any tracker.\nEducational and security-research tool — use only where you are permitted to.\n\n{2}",
                               "{0} v{1}\n\nSimule vos statistiques d'envoi/téléchargement BitTorrent sur n'importe quel tracker.\nOutil éducatif et de recherche en sécurité — à n'utiliser que là où vous en avez le droit.\n\n{2}"),
    };

    internal static string Get(string key) {
      if (Map.TryGetValue(key, out var pair))
        return IsFrench ? pair.fr : pair.en;
      return key;
    }
  }
}
