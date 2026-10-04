using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Seedforger.BitTorrent;
using Seedforger.Net;

namespace Seedforger {

  /// <summary>
  /// The campaign orchestrator: brings a folder of torrents online in a staggered,
  /// human way over <see cref="SeedEngine"/> instances, splits the upstream budget
  /// toward real demand, paces to the deadline and stops at the goal. Policy
  /// comes from the pure <see cref="CampaignPlanner"/>.
  ///
  /// It runs with its own <see cref="StealthOptions"/> and its own
  /// <see cref="UpstreamBudget"/>, so a campaign never changes what the user set
  /// for single-torrent runs. Used by both GUIs and scriptable from the CLI.
  /// </summary>
  internal sealed class CampaignEngine : IDisposable {

    private const int TickMilliseconds = 15_000;

    private sealed class Slot {
      public string Path;
      public int OffsetMin;
      public SeedEngine Engine;
      public bool Started;
    }

    private readonly Campaign campaign;
    private readonly Action<string> log;
    private readonly ProxySettings proxy;
    private readonly StealthOptions stealth;
    private readonly UpstreamBudget budget = new UpstreamBudget();
    private readonly List<Slot> slots = new List<Slot>();
    private readonly Random rand = new Random();
    private Timer timer;
    private int ticking;
    private DateTime startTime;
    private long goalBytes;
    private int globalUpKBps;
    private volatile bool done;

    /// <summary>Raised (on a background thread) once the goal is reached.</summary>
    public event Action<CampaignEngine> Completed;

    /// <param name="campaign">The plan.</param>
    /// <param name="log">Log sink (the engines' lines go there too).</param>
    /// <param name="proxy">Proxy for every engine; null = direct.</param>
    /// <param name="baseStealth">Realistic/swarm-aware flags to start from (cloned,
    /// then the campaign's own active-hours window is applied).</param>
    internal CampaignEngine(Campaign campaign, Action<string> log, ProxySettings proxy = null, StealthOptions baseStealth = null) {
      this.campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
      this.log = log;
      this.proxy = proxy ?? ProxySettings.None;
      stealth = (baseStealth ?? StealthOptions.Shared).Clone();
      stealth.ActiveHoursEnabled = campaign.UseActiveHours;
      stealth.ActiveHoursStart = campaign.ActiveHoursStart;
      stealth.ActiveHoursEnd = campaign.ActiveHoursEnd;
    }

    public bool Running { get; private set; }
    public bool GoalReached => done;
    public int TorrentCount => slots.Count;
    public int ActiveCount => slots.Count(s => s.Engine != null && s.Engine.IsRunning);
    public long TotalUploaded => slots.Sum(s => s.Engine?.UploadedBytes ?? 0);
    public long TotalDownloaded => slots.Sum(s => s.Engine?.DownloadedBytes ?? 0);
    public TimeSpan Elapsed => Running || done ? DateTime.Now - startTime : TimeSpan.Zero;
    /// <summary>The engines started so far (for a per-torrent read-out).</summary>
    public IReadOnlyList<SeedEngine> Engines => slots.Where(s => s.Engine != null).Select(s => s.Engine).ToList();

    /// <summary>Starts the campaign. Returns false, after logging why, when the
    /// folder is missing or holds no torrents.</summary>
    public bool Start() {
      if (Running) return true;
      if (!Directory.Exists(campaign.TorrentFolder)) { log?.Invoke("Campaign folder not found: " + campaign.TorrentFolder); return false; }
      var files = Directory.GetFiles(campaign.TorrentFolder, "*.torrent");
      if (files.Length == 0) { log?.Invoke("No .torrent files in " + campaign.TorrentFolder); return false; }

      globalUpKBps = ConnectionProfiles.UpKBpsOf(campaign.Connection);
      budget.TotalKBps = globalUpKBps;

      var seed = (int) (DateTime.Now.Ticks & 0x7fffffff);
      var offsets = CampaignPlanner.StaggerOffsets(files.Length, campaign.StaggerMinMinutes, campaign.StaggerMaxMinutes, seed);
      slots.Clear();
      for (var i = 0; i < files.Length; i++) slots.Add(new Slot { Path = files[i], OffsetMin = offsets[i] });

      goalBytes = campaign.IsRatioGoal ? long.MaxValue : (long) (campaign.UploadGoalGB * 1024 * 1024 * 1024);
      startTime = DateTime.Now;
      Running = true;
      done = false;
      log?.Invoke($"campaign started: {files.Length} torrents, goal={campaign.Goal}, connection={campaign.Connection} ({globalUpKBps} kB/s budget)");
      timer = new Timer(_ => Tick(), null, 0, TickMilliseconds);
      return true;
    }

    public void Stop() {
      if (!Running) return;
      Running = false;
      timer?.Dispose();
      timer = null;
      foreach (var s in slots) s.Engine?.Stop();
      log?.Invoke("campaign stopped by user");
    }

    public void Dispose() => Stop();

    private void Tick() {
      // Ticks come from the thread pool; starting an engine blocks on its first
      // announce, so never let two ticks overlap.
      if (Interlocked.Exchange(ref ticking, 1) == 1) return;
      try { TickCore(); }
      catch (Exception ex) { log?.Invoke("campaign error: " + ex.Message); }
      finally { Interlocked.Exchange(ref ticking, 0); }
    }

    private void TickCore() {
      if (done || !Running) return;
      var elapsedMin = (DateTime.Now - startTime).TotalMinutes;
      var totalUp = TotalUploaded;
      var totalDown = TotalDownloaded;

      var reached = campaign.IsRatioGoal
        ? CampaignPlanner.RatioGoalReached(totalUp, totalDown, campaign.TargetRatio)
        : CampaignPlanner.UploadGoalReached(totalUp, goalBytes);
      if (reached) {
        foreach (var s in slots) s.Engine?.Stop();
        done = true;
        Running = false;
        timer?.Dispose();
        timer = null;
        log?.Invoke("campaign goal reached — complete.");
        try { Completed?.Invoke(this); } catch (Exception ex) { log?.Invoke("campaign handler error: " + ex.Message); }
        return;
      }

      var running = slots.Where(s => s.Engine != null && s.Engine.IsRunning).ToList();
      if (stealth.IsActive(DateTime.Now)) {
        foreach (var slot in slots) {
          if (slot.Started || elapsedMin < slot.OffsetMin) continue;
          if (campaign.MaxConcurrent > 0 && running.Count >= campaign.MaxConcurrent) break;
          StartSlot(slot);
          if (slot.Engine != null) running.Add(slot);
        }
      }

      var ceiling = CampaignPlanner.PaceCeiling(goalBytes, elapsedMin, campaign.DeadlineHours * 60.0);
      var aheadOfPace = !campaign.IsRatioGoal && totalUp >= ceiling;

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
      slot.Started = true; // never retry a broken torrent forever
      try {
        var torrent = new Torrent(slot.Path);
        var client = campaign.RotateClient ? TorrentClientFactory.PickRandomModern(rand) : TorrentClientFactory.Default();
        var upl = globalUpKBps > 0 ? globalUpKBps : ConnectionProfiles.UpKBpsOf(campaign.Connection);
        var opts = new SeedOptions {
          Torrent = torrent, Client = client, Proxy = proxy,
          UploadKBps = upl, DownloadKBps = 0, FinishedPercent = 100,
          Stealth = stealth, Budget = budget, Log = log,
        };
        if (!string.IsNullOrEmpty(campaign.RealFileFolder) && Directory.Exists(campaign.RealFileFolder)) {
          var candidate = Path.Combine(campaign.RealFileFolder, torrent.Name);
          if (File.Exists(candidate)) opts.RealFile = candidate;
        }
        var engine = new SeedEngine(opts);
        engine.Start();
        slot.Engine = engine;
        log?.Invoke("brought online: " + Path.GetFileName(slot.Path) + " as " + client.Name);
      }
      catch (Exception ex) {
        log?.Invoke("failed to start " + Path.GetFileName(slot.Path) + ": " + ex.Message);
      }
    }
  }
}
