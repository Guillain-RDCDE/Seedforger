using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Seedforger.BitTorrent;

namespace Seedforger {

  /// <summary>
  /// Portable, WinForms-free campaign orchestrator: brings a folder of torrents
  /// online in a staggered, human way over <see cref="SeedEngine"/> instances,
  /// splits the upstream budget toward real demand, paces to the deadline and
  /// stops at the goal. Policy comes from the pure <see cref="CampaignPlanner"/>.
  /// Runs on Windows, Linux and macOS — used by the Avalonia GUI (and scriptable).
  /// </summary>
  internal sealed class CampaignEngine {

    private sealed class Slot { public string Path; public int OffsetMin; public SeedEngine Engine; public bool Started; }

    private readonly Campaign campaign;
    private readonly Action<string> log;
    private readonly System.Timers.Timer timer;
    private readonly List<Slot> slots = new List<Slot>();
    private readonly Random rand = new Random();
    private DateTime startTime;
    private long goalBytes;
    private int globalUpKBps;
    private bool done;

    public bool Running { get; private set; }

    internal CampaignEngine(Campaign campaign, Action<string> log) {
      this.campaign = campaign;
      this.log = log;
      timer = new System.Timers.Timer(15000) { AutoReset = true };
      timer.Elapsed += (s, e) => { try { Tick(); } catch (Exception ex) { log?.Invoke("campaign error: " + ex.Message); } };
    }

    public void Start() {
      if (!Directory.Exists(campaign.TorrentFolder)) { log?.Invoke("Campaign folder not found: " + campaign.TorrentFolder); return; }
      var files = Directory.GetFiles(campaign.TorrentFolder, "*.torrent");
      if (files.Length == 0) { log?.Invoke("No .torrent files in " + campaign.TorrentFolder); return; }

      AppOptions.RandomizeClientOnStart = campaign.RotateClient;
      AppOptions.ActiveHoursEnabled = campaign.UseActiveHours;
      AppOptions.ActiveHoursStart = campaign.ActiveHoursStart;
      AppOptions.ActiveHoursEnd = campaign.ActiveHoursEnd;

      globalUpKBps = ProfileUpKBps(campaign.Connection);
      Bandwidth.GlobalUpKBps = globalUpKBps;

      var seed = (int) (DateTime.Now.Ticks & 0x7fffffff);
      var offsets = CampaignPlanner.StaggerOffsets(files.Length, campaign.StaggerMinMinutes, campaign.StaggerMaxMinutes, seed);
      for (var i = 0; i < files.Length; i++) slots.Add(new Slot { Path = files[i], OffsetMin = offsets[i] });

      goalBytes = campaign.Goal == "upload" ? (long) (campaign.UploadGoalGB * 1024 * 1024 * 1024) : long.MaxValue;
      startTime = DateTime.Now;
      Running = true; done = false;
      log?.Invoke($"campaign started: {files.Length} torrents, goal={campaign.Goal}, connection={campaign.Connection} ({globalUpKBps} kB/s budget)");
      timer.Start();
      Tick(); // kick the first torrent immediately (offset 0)
    }

    public void Stop() {
      timer.Stop();
      Running = false;
      foreach (var s in slots) s.Engine?.Stop();
      log?.Invoke("campaign stopped by user");
    }

    private void Tick() {
      if (done) return;
      var elapsedMin = (DateTime.Now - startTime).TotalMinutes;

      long totalUp = 0, totalDown = 0;
      foreach (var s in slots) if (s.Engine != null) { totalUp += s.Engine.UploadedBytes; totalDown += s.Engine.DownloadedBytes; }

      var reached = campaign.Goal == "ratio"
        ? CampaignPlanner.RatioGoalReached(totalUp, totalDown, campaign.TargetRatio)
        : CampaignPlanner.UploadGoalReached(totalUp, goalBytes);
      if (reached) {
        foreach (var s in slots) s.Engine?.Stop();
        done = true; Running = false; timer.Stop();
        log?.Invoke("campaign goal reached — complete.");
        return;
      }

      var running = slots.Where(s => s.Engine != null && s.Engine.IsRunning).ToList();
      var inHours = !campaign.UseActiveHours ||
        Stealth.InActiveHours(DateTime.Now, campaign.ActiveHoursStart, campaign.ActiveHoursEnd);

      if (inHours) {
        foreach (var slot in slots) {
          if (slot.Started || elapsedMin < slot.OffsetMin) continue;
          if (campaign.MaxConcurrent > 0 && running.Count >= campaign.MaxConcurrent) break;
          StartSlot(slot);
          if (slot.Engine != null) running.Add(slot);
        }
      }

      var ceiling = CampaignPlanner.PaceCeiling(goalBytes, elapsedMin, campaign.DeadlineHours * 60.0);
      var aheadOfPace = campaign.Goal == "upload" && totalUp >= ceiling;

      var active = slots.Where(s => s.Engine != null && s.Engine.IsRunning).ToList();
      if (globalUpKBps > 0) {
        var leechers = active.Select(s => Math.Max(0, s.Engine.LeecherCount)).ToArray();
        var alloc = CampaignPlanner.AllocateByDemand(globalUpKBps, leechers);
        for (var i = 0; i < active.Count; i++)
          active[i].Engine.SetUploadKBps(aheadOfPace ? 1 : Math.Max(1, alloc[i]));
      }
      else if (aheadOfPace) {
        foreach (var s in active) s.Engine.SetUploadKBps(1);
      }
    }

    private void StartSlot(Slot slot) {
      try {
        var torrent = new Torrent(slot.Path);
        var client = PickClient();
        var upl = globalUpKBps > 0 ? globalUpKBps : ProfileUpKBps(campaign.Connection);
        var engine = new SeedEngine(torrent, client, new ProxyInfo(), upl, 0, 100) { Log = log };

        if (!string.IsNullOrEmpty(campaign.RealFileFolder) && Directory.Exists(campaign.RealFileFolder)) {
          var candidate = Path.Combine(campaign.RealFileFolder, torrent.Name);
          if (File.Exists(candidate)) engine.EnableRealSeed(candidate);
        }

        engine.Start();
        slot.Engine = engine;
        slot.Started = true;
        log?.Invoke("brought online: " + Path.GetFileName(slot.Path));
      }
      catch (Exception ex) {
        slot.Started = true; // don't retry a broken torrent forever
        log?.Invoke("failed to start " + Path.GetFileName(slot.Path) + ": " + ex.Message);
      }
    }

    private TorrentClient PickClient() {
      var pool = TorrentClientFactory.ModernClients;
      if (campaign.RotateClient && pool != null && pool.Length > 0)
        return TorrentClientFactory.GetClient(pool[rand.Next(pool.Length)]);
      return TorrentClientFactory.GetClient("qBittorrent 5.2.3");
    }

    private static int ProfileUpKBps(string connectionName) {
      foreach (var p in ConnectionProfiles.All) if (p.Name == connectionName) return p.UpKBps;
      return 0;
    }
  }
}
