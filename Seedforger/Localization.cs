using System.Collections.Generic;
using System.Windows.Forms;

namespace Seedforger {

  internal enum Language { English, French }

  /// <summary>
  /// Lightweight runtime localization. Walks the control/menu tree and swaps
  /// known English UI strings for their French translation (and back). Combo-box
  /// items are left untouched on purpose - some are compared by value in logic.
  /// </summary>
  internal static class Localization {

    private static readonly Dictionary<string, string> EnToFr = new Dictionary<string, string> {
      // Menus
      ["File"] = "Fichier",
      ["All Seedforgers "] = "Tous les Seedforgers ",
      ["Settings"] = "Réglages",
      ["Help"] = "Aide",
      ["New"] = "Nouveau",
      ["Rename"] = "Renommer",
      ["Close"] = "Fermer",
      ["Start"] = "Démarrer",
      ["Stop"] = "Arrêter",
      ["Manual update"] = "Mise à jour manuelle",
      ["Exit"] = "Quitter",
      ["Update"] = "Mettre à jour",
      ["Set upload speed to"] = "Définir la vitesse d'upload",
      ["Set download speed to"] = "Définir la vitesse de download",
      ["Load session"] = "Charger une session",
      ["Load session and Start"] = "Charger une session et démarrer",
      ["Save current session"] = "Enregistrer la session",
      ["Stop and Save current session"] = "Arrêter et enregistrer la session",
      ["Minimize to tray"] = "Réduire dans la zone de notification",
      ["Show baloon"] = "Afficher les info-bulles",
      ["Close to tray"] = "Fermer vers la zone de notification",
      ["Save settings from current tab"] = "Enregistrer les réglages de l'onglet",
      ["Web site"] = "Site web",
      ["About"] = "À propos",
      ["Show"] = "Afficher",
      // Menu items added in code
      ["Dark mode"] = "Mode sombre",
      ["Realistic speed (ramp-up)"] = "Vitesse réaliste (montée progressive)",
      ["Randomize client on start"] = "Client aléatoire au démarrage",
      ["Connection profile"] = "Profil de connexion",
      ["Active hours…"] = "Heures actives…",
      ["Open magnet…"] = "Ouvrir un magnet…",
      ["Load folder of .torrents…"] = "Charger un dossier de .torrents…",
      ["Test announce (dry-run)"] = "Tester l'annonce (à blanc)",
      ["Live graph…"] = "Graphe en direct…",
      ["Language"] = "Langue",
      // Status bar
      ["Local IP:"] = "IP locale :",
      ["Tabs opened:"] = "Onglets ouverts :",
      ["Update in:"] = "MàJ dans :",
      ["Ratio:"] = "Ratio :",
      ["Time:"] = "Temps :",
      // Groups / labels / buttons (RM)
      ["Torrent"] = "Torrent",
      ["Options"] = "Options",
      ["File:"] = "Fichier :",
      ["Tracker:"] = "Tracker :",
      ["HASH:\r\n"] = "HASH :\r\n",
      ["Size:"] = "Taille :",
      ["Browse..."] = "Parcourir...",
      ["Upload Speed (kB/s):"] = "Vitesse d'upload (ko/s) :",
      ["Download Speed (kB/s):"] = "Vitesse de download (ko/s) :",
      ["Update Interval (s):"] = "Intervalle de mise à jour (s) :",
      ["Finished (%):"] = "Terminé (%) :",
      ["STOP:"] = "ARRÊT :",
      ["+ Random values:"] = "+ Valeurs aléatoires :",
      ["Min:"] = "Min :",
      ["Max:"] = "Max :",
      ["Use TCP listener"] = "Écouteur TCP",
      ["Request Scrap"] = "Requête scrape",
      ["Ignore 'failure reason'"] = "Ignorer 'failure reason'",
      ["START"] = "DÉMARRER",
      ["STOP"] = "ARRÊTER",
      ["Manual Update"] = "Mise à jour manuelle",
      ["Set default values"] = "Valeurs par défaut",
      ["Client:"] = "Client :",
      ["Custom Client Simulation"] = "Simulation de client personnalisée",
      ["Client Key:"] = "Clé client :",
      ["Peer ID:"] = "Peer ID :",
      ["Number of peers:"] = "Nombre de pairs :",
      ["Port:"] = "Port :",
      ["Always get new values"] = "Toujours générer de nouvelles valeurs",
      ["Generation status:"] = "État de génération :",
      ["On Next Update Get Random Speeds"] = "Vitesses aléatoires à la prochaine MàJ",
      ["Proxy Server Settings"] = "Réglages du serveur proxy",
      ["Proxy Host:"] = "Hôte proxy :",
      ["Proxy Pass:"] = "Mot de passe proxy :",
      ["Proxy Port:"] = "Port proxy :",
      ["Proxy Type:"] = "Type de proxy :",
      ["Proxy User:"] = "Utilisateur proxy :",
      ["Remaning:"] = "Restant :",
    };

    private static readonly Dictionary<string, string> FrToEn = BuildReverse();

    private static Dictionary<string, string> BuildReverse() {
      var d = new Dictionary<string, string>();
      foreach (var kv in EnToFr) d[kv.Value] = kv.Key; // indexer avoids duplicate-key throws
      return d;
    }

    internal static Language Parse(string code) =>
      string.Equals(code, "fr", System.StringComparison.OrdinalIgnoreCase) ? Language.French : Language.English;

    internal static string Code(Language lang) => lang == Language.French ? "fr" : "en";

    /// <summary>Translates a control subtree to the current AppOptions.Language.</summary>
    internal static void Apply(Control root) {
      var fr = AppOptions.Language == Language.French;
      Walk(root, fr);
      root.Invalidate(true);
    }

    private static void Walk(Control c, bool fr) {
      foreach (Control child in c.Controls) {
        if (child is Label || child is Button || child is CheckBox || child is RadioButton || child is GroupBox)
          child.Text = Tr(child.Text, fr);
        if (child is ToolStrip ts)
          TranslateItems(ts.Items, fr);
        if (child.ContextMenuStrip != null)
          TranslateItems(child.ContextMenuStrip.Items, fr);
        if (child.HasChildren)
          Walk(child, fr);
      }
    }

    private static void TranslateItems(ToolStripItemCollection items, bool fr) {
      foreach (ToolStripItem item in items) {
        item.Text = Tr(item.Text, fr);
        if (item is ToolStripMenuItem mi && mi.HasDropDownItems)
          TranslateItems(mi.DropDownItems, fr);
      }
    }

    private static string Tr(string text, bool fr) {
      if (string.IsNullOrEmpty(text)) return text;
      if (fr) return EnToFr.TryGetValue(text, out var f) ? f : text;
      return FrToEn.TryGetValue(text, out var e) ? e : text;
    }
  }
}
