using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Seedforger {

  /// <summary>
  /// Drives a <see cref="Campaign"/> on the UI thread: brings torrents online in a
  /// staggered, human way, splits the upstream budget toward torrents that have
  /// real demand, paces the total upload toward the deadline (so the goal isn't
  /// hit suspiciously early), and stops when the goal is reached. All policy comes
  /// from the pure <see cref="CampaignPlanner"/>; this is the thin, guarded driver.
  /// </summary>
  internal sealed class CampaignRunner {

    private sealed class Slot { public string Path; public int OffsetMin; public RM Rm; public bool Started; }

    private readonly MainForm form;
    private readonly Campaign campaign;
    private readonly Timer timer;
    private readonly List<Slot> slots = new List<Slot>();
    private DateTime startTime;
    private long goalBytes;
    private int globalUpKBps;
    private bool done;

    internal bool Running { get; private set; }

    internal CampaignRunner(MainForm form, Campaign campaign) {
      this.form = form;
      this.campaign = campaign;
      timer = new Timer { Interval = 15000 };
      timer.Tick += (s, e) => { try { Tick(); } catch (Exception ex) { form.CampaignLog("error: " + ex.Message); } };
    }

    internal void Start() {
      if (!Directory.Exists(campaign.TorrentFolder)) {
        MessageBox.Show("Campaign torrent folder not found:\n" + campaign.TorrentFolder, "Seedforger");
        return;
      }
      var files = Directory.GetFiles(campaign.TorrentFolder, "*.torrent");
      if (files.Length == 0) {
        MessageBox.Show("No .torrent files in:\n" + campaign.TorrentFolder, "Seedforger");
        return;
      }

      // Believability options from the campaign.
      AppOptions.RandomizeClientOnStart = campaign.RotateClient;
      AppOptions.ActiveHoursEnabled = campaign.UseActiveHours;
      AppOptions.ActiveHoursStart = campaign.ActiveHoursStart;
      AppOptions.ActiveHoursEnd = campaign.ActiveHoursEnd;

      globalUpKBps = ProfileUpKBps(campaign.Connection);
      Bandwidth.GlobalUpKBps = globalUpKBps;

      var seed = (int) (DateTime.Now.Ticks & 0x7fffffff);
      var offsets = CampaignPlanner.StaggerOffsets(files.Length,
        campaign.StaggerMinMinutes, campaign.StaggerMaxMinutes, seed);
      for (var i = 0; i < files.Length; i++)
        slots.Add(new Slot { Path = files[i], OffsetMin = offsets[i] });

      goalBytes = campaign.Goal == "upload" ? (long) (campaign.UploadGoalGB * 1024 * 1024 * 1024) : long.MaxValue;
      startTime = DateTime.Now;
      Running = true;
      done = false;
      form.CampaignLog($"started: {files.Length} torrents, goal={campaign.Goal}, connection={campaign.Connection} ({globalUpKBps} kB/s budget)");
      timer.Start();
      Tick(); // kick the first torrent immediately (offset 0)
    }

    internal void Stop() {
      timer.Stop();
      Running = false;
      foreach (var s in slots) s.Rm?.CampaignStop();
      form.CampaignLog("stopped by user");
    }

    private void Tick() {
      if (done) return;
      var elapsedMin = (DateTime.Now - startTime).TotalMinutes;

      long totalUp = 0, totalDown = 0;
      foreach (var s in slots) if (s.Rm != null) { totalUp += s.Rm.UploadedBytes; totalDown += s.Rm.DownloadedBytes; }

      // Goal reached?
      var reached = campaign.Goal == "ratio"
        ? CampaignPlanner.RatioGoalReached(totalUp, totalDown, campaign.TargetRatio)
        : CampaignPlanner.UploadGoalReached(totalUp, goalBytes);
      if (reached) {
        foreach (var s in slots) s.Rm?.CampaignStop();
        done = true;
        Running = false;
        timer.Stop();
        form.CampaignLog("goal reached — campaign complete.");
        return;
      }

      var running = slots.Where(s => s.Rm != null && s.Rm.IsRunning).ToList();
      var inHours = !campaign.UseActiveHours ||
        Stealth.InActiveHours(DateTime.Now, campaign.ActiveHoursStart, campaign.ActiveHoursEnd);

      // Bring due torrents online (staggered, within the concurrency cap, during active hours).
      if (inHours) {
        foreach (var slot in slots) {
          if (slot.Started || elapsedMin < slot.OffsetMin) continue;
          if (campaign.MaxConcurrent > 0 && running.Count >= campaign.MaxConcurrent) break;
          StartSlot(slot);
          if (slot.Rm != null) running.Add(slot);
        }
      }

      // Pace: if we're already ahead of where we should be, throttle to a trickle.
      var ceiling = CampaignPlanner.PaceCeiling(goalBytes, elapsedMin, campaign.DeadlineHours * 60.0);
      var aheadOfPace = campaign.Goal == "upload" && totalUp >= ceiling;

      // Allocate the upstream budget toward torrents with real demand.
      var active = slots.Where(s => s.Rm != null && s.Rm.IsRunning).ToList();
      if (globalUpKBps > 0) {
        var leechers = active.Select(s => Math.Max(0, s.Rm.LeecherCount)).ToArray();
        var alloc = CampaignPlanner.AllocateByDemand(globalUpKBps, leechers);
        for (var i = 0; i < active.Count; i++)
          active[i].Rm.SetUploadKBps(aheadOfPace ? 1 : Math.Max(1, alloc[i]));
      }
      else if (aheadOfPace) {
        // No global budget: still honour the pace by throttling to a trickle.
        foreach (var s in active) s.Rm.SetUploadKBps(1);
      }
    }

    private void StartSlot(Slot slot) {
      try {
        var rm = form.AddTorrentTab(slot.Path);
        if (rm == null) { slot.Started = true; return; }
        slot.Rm = rm;
        form.ApplyConnectionProfileByName(campaign.Connection);

        if (!string.IsNullOrEmpty(campaign.RealFileFolder) && Directory.Exists(campaign.RealFileFolder)) {
          var candidate = Path.Combine(campaign.RealFileFolder, rm.TorrentDisplayName);
          if (File.Exists(candidate)) rm.EnableRealSeed(candidate);
        }

        rm.CampaignStart();
        slot.Started = true;
        form.CampaignLog("brought online: " + Path.GetFileName(slot.Path));
      }
      catch (Exception ex) {
        slot.Started = true; // don't retry a broken torrent forever
        form.CampaignLog("failed to start " + Path.GetFileName(slot.Path) + ": " + ex.Message);
      }
    }

    private static int ProfileUpKBps(string connectionName) {
      foreach (var p in ConnectionProfiles.All) if (p.Name == connectionName) return p.UpKBps;
      return 0; // unknown -> unlimited/no budget
    }
  }
}
