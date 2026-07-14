using System.Collections.Generic;

namespace Seedforger.UI {

  /// <summary>
  /// English/French strings for the interface chrome. Kept as a small self-contained
  /// table (the from-scratch UI doesn't use the legacy resource-name map) so the
  /// header, cards, menus and tray follow the language toggle at runtime.
  /// </summary>
  internal static class UiStrings {

    private static readonly Dictionary<string, (string en, string fr)> Map = new Dictionary<string, (string, string)> {
      // header
      ["subtitle"]          = ("believable torrent stats — no bytes moved", "des stats torrent crédibles — aucun octet transféré"),
      ["nav.guided"]        = ("Guided", "Assistant"),
      ["nav.campaigns"]     = ("Campaigns", "Campagnes"),
      ["nav.tools"]         = ("Tools", "Outils"),
      // cards
      ["card.torrent"]      = ("TORRENT", "TORRENT"),
      ["no_torrent"]        = ("No torrent loaded", "Aucun torrent chargé"),
      ["browse"]            = ("Browse", "Parcourir"),
      ["card.client"]       = ("WHICH CLIENT TO IMPERSONATE", "QUEL CLIENT IMITER"),
      ["client"]            = ("Client", "Client"),
      ["version"]           = ("Version", "Version"),
      ["advanced"]          = ("Advanced…", "Avancé…"),
      ["card.speed"]        = ("SPEED & MODE", "VITESSE & MODE"),
      ["upload_kbs"]        = ("Upload kB/s", "Envoi ko/s"),
      ["mode"]              = ("Mode", "Mode"),
      ["mode.seeder"]       = ("Seeder (100% — recommended)", "Seeder (100 % — recommandé)"),
      ["mode.leecher"]      = ("Leecher (0%)", "Leecher (0 %)"),
      ["start_seeding"]     = ("Start seeding", "Démarrer le seed"),
      ["stop"]              = ("Stop", "Arrêter"),
      // live panel
      ["live"]              = ("LIVE", "EN DIRECT"),
      ["ratio"]             = ("RATIO", "RATIO"),
      ["uploaded"]          = ("UPLOADED", "ENVOYÉ"),
      ["swarm"]             = ("SEEDERS / LEECHERS", "SEEDERS / LEECHERS"),
      ["status"]            = ("STATUS", "ÉTAT"),
      ["seeding"]           = ("Seeding", "Actif (seed)"),
      ["idle"]              = ("Idle", "Inactif"),
      ["activity"]          = ("ACTIVITY", "ACTIVITÉ"),
      // tooltips
      ["tip.help"]          = ("About & links", "À propos & liens"),
      ["tip.settings"]      = ("Settings", "Réglages"),
      ["tip.tools"]         = ("Magnet, dry-run, live graph…", "Magnet, test à blanc, graphe…"),
      ["tip.campaigns"]     = ("Run many torrents on a schedule", "Lancer plusieurs torrents planifiés"),
      ["tip.guided"]        = ("Step-by-step newbie mode", "Mode débutant pas à pas"),
      ["tip.advanced"]      = ("Custom fingerprint & proxy", "Empreinte personnalisée & proxy"),
      ["tip.client"]        = ("The torrent app you pretend to be (e.g. qBittorrent). The tracker sees this client.",
                               "L'appli torrent dont tu prends l'identité (ex. qBittorrent). C'est ce que voit le tracker."),
      ["tip.ratio"]         = ("Ratio = uploaded ÷ downloaded.\nA seeder downloads nothing, so the ratio is infinite — shown as “—”.\nIt becomes a real number only if you simulate some download.",
                               "Ratio = envoyé ÷ téléchargé.\nUn seeder ne télécharge rien, donc le ratio est infini — affiché « — ».\nIl devient un vrai nombre seulement si vous simulez du téléchargement."),
      // menus — tools
      ["menu.open_magnet"]  = ("Open magnet…", "Ouvrir un magnet…"),
      ["menu.load_torrent"] = ("Load a .torrent…", "Charger un .torrent…"),
      ["menu.test_announce"]= ("Test announce (dry-run)", "Tester l'annonce (à blanc)"),
      ["menu.serve_real"]   = ("Serve a real file (advanced)…", "Servir un vrai fichier (avancé)…"),
      ["menu.live_graph"]   = ("Live graph…", "Graphe en direct…"),
      // menus — settings
      ["menu.realistic"]    = ("Realistic speed (ramp-up)", "Vitesse réaliste (montée progressive)"),
      ["menu.swarm"]        = ("Swarm-aware speeds", "Vitesses selon la demande"),
      ["menu.randomize"]    = ("Randomize client on start", "Client aléatoire au démarrage"),
      ["menu.connection"]   = ("Connection profile", "Profil de connexion"),
      ["menu.active_hours"] = ("Active hours…", "Heures actives…"),
      ["menu.minimize_tray"]= ("Minimize to tray", "Réduire dans la zone de notification"),
      ["menu.close_tray"]   = ("Close to tray", "Fermer dans la zone de notification"),
      ["menu.tray_balloon"] = ("Show tray notification", "Afficher la bulle de notification"),
      ["menu.language"]     = ("Language", "Langue"),
      // menus — help
      ["menu.about"]        = ("About Seedforger", "À propos de Seedforger"),
      ["menu.open_repo"]    = ("Open the GitHub repo", "Ouvrir le dépôt GitHub"),
      // tray
      ["tray.restore"]      = ("Restore", "Restaurer"),
      ["tray.quit"]         = ("Quit", "Quitter"),
      ["tray.balloon_text"] = ("Still running — tucked away here. Double-click to reopen.",
                               "Toujours actif — rangé ici. Double-cliquez pour rouvrir."),
      // dialogs / prompts
      ["dlg.choose_torrent"]= ("Choose a .torrent", "Choisir un .torrent"),
      ["dlg.torrent_filter"]= ("Torrent file (*.torrent)|*.torrent", "Fichier torrent (*.torrent)|*.torrent"),
      ["dlg.read_error"]    = ("Couldn't read that .torrent: ", "Impossible de lire ce .torrent : "),
      ["dlg.magnet_title"]  = ("Open magnet", "Ouvrir un magnet"),
      ["dlg.magnet_label"]  = ("Paste a magnet link:", "Collez un lien magnet :"),
      ["dlg.serve_title"]   = ("Pick the downloaded file that matches this torrent", "Choisissez le fichier téléchargé correspondant à ce torrent"),
      ["dlg.hours_title"]   = ("Active hours", "Heures actives"),
      ["dlg.hours_label"]   = ("Seed only between these hours (0-24), e.g. 8-24 or 22-6.\nLeave empty for 24/7:",
                               "Seed uniquement entre ces heures (0-24), ex. 8-24 ou 22-6.\nLaissez vide pour 24/7 :"),
      ["dlg.hours_bad"]     = ("Use a format like 8-24 or 22-6.", "Utilisez un format comme 8-24 ou 22-6."),
      ["dlg.update_title"]  = ("update available", "mise à jour disponible"),
      ["dlg.update_text"]   = ("A new version {0} is available — you have v{1}.\n\nOpen the download page?",
                               "Une nouvelle version {0} est disponible — vous avez la v{1}.\n\nOuvrir la page de téléchargement ?"),
    };

    internal static string Get(string key) {
      if (Map.TryGetValue(key, out var pair))
        return AppOptions.Language == Language.French ? pair.fr : pair.en;
      return key;
    }
  }
}
