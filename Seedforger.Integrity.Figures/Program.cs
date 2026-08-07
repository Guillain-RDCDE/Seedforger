using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Seedforger.Integrity;

namespace Seedforger.Integrity.Figures {

  /// <summary>
  /// Recomputes every headline number in docs/papers from the Integrity model and
  /// renders the figures. The papers quote metrics.json; the xUnit suite guards the
  /// same computations with thresholds. Nothing here is hand-authored, so the prose
  /// cannot drift from the code.
  /// </summary>
  internal static class Program {
    private static readonly CultureInfo C = CultureInfo.InvariantCulture;
    private const int Seeds = 25;

    private static int Main(string[] args) {
      string outDir = args.Length > 0
        ? args[0]
        : Path.Combine("docs", "papers", "figures");
      Directory.CreateDirectory(outDir);

      var metrics = new Dictionary<string, object>();

      // ---- Detector performance (Paper 1) --------------------------------------
      var careful = Canonical();                       // careful cheaters (hardest case)
      var naive = Canonical(); naive.CheaterPhysicalOvershoot = 0.6;
      double aucCareful = MeanAuc(careful);
      double aucNaive = MeanAuc(naive);
      metrics["auc_careful"] = Round(aucCareful);
      metrics["auc_naive"] = Round(aucNaive);

      // ---- Swarm-level mass alarm ---------------------------------------------
      double alarmHealthy = MeanAlarm(WithCheaters(0.0));
      double alarmCheated = MeanAlarm(WithCheaters(0.15));
      metrics["mass_alarm_healthy"] = Round(alarmHealthy);
      metrics["mass_alarm_cheated"] = Round(alarmCheated);

      // ---- Robust estimator breakdown (Paper 1, hard layer) -------------------
      var (bdFrac, bdMean, bdMedian, bdHuber, honestCenter) = BreakdownCurves();
      metrics["breakdown_honest_center"] = Round(honestCenter);
      metrics["breakdown_mean_at_040"] = Round(Interp(bdFrac, bdMean, 0.40));
      metrics["breakdown_huber_at_040"] = Round(Interp(bdFrac, bdHuber, 0.40));
      metrics["breakdown_median_at_040"] = Round(Interp(bdFrac, bdMedian, 0.40));

      // ---- Asymmetry game (Paper 2) -------------------------------------------
      double tau = 0.20;
      metrics["game_tau"] = tau;
      metrics["game_credit_multiplier"] = Round(AsymmetryGame.MaxCreditMultiplier(tau));
      metrics["game_free_ratio_at_zero_work"] = Round(AsymmetryGame.MaxUndetectedDeclaredUp(0.0, tau));

      // ---- Boundary: where the server design weakens ---------------------------
      // Two stressors combine: thin monitoring coverage and a fresh torrent (a short
      // window = little volume = few pieces = a noisy corroboration measurement).
      var covGrid = new double[] { 0.02, 0.05, 0.10, 0.15, 0.25, 0.40 };
      var aucEstablished = new List<(double x, double y)>();
      var aucFresh = new List<(double x, double y)>();
      foreach (var c in covGrid) {
        var e = Canonical(); e.Coverage = c;                       // established torrent (30-min window)
        aucEstablished.Add((c, MeanAuc(e)));
        var f = Canonical(); f.Coverage = c; f.WindowSeconds = 90; // fresh torrent, tiny window
        aucFresh.Add((c, MeanAuc(f)));
      }
      metrics["auc_established_c015"] = Round(aucEstablished[3].y);
      metrics["auc_established_c002"] = Round(aucEstablished[0].y);
      metrics["auc_fresh_c015"] = Round(aucFresh[3].y);
      metrics["auc_fresh_c002"] = Round(aucFresh[0].y);
      metrics["auc_default"] = Round(aucCareful);

      // ==== Figures =============================================================
      File.WriteAllText(Path.Combine(outDir, "fig1-invariants.svg"), FigInvariants());
      File.WriteAllText(Path.Combine(outDir, "fig2-roc.svg"), FigRoc(careful));
      File.WriteAllText(Path.Combine(outDir, "fig3-breakdown.svg"), FigBreakdown(bdFrac, bdMean, bdMedian, bdHuber, honestCenter));
      File.WriteAllText(Path.Combine(outDir, "fig4-plateau.svg"), FigPlateau(tau));
      File.WriteAllText(Path.Combine(outDir, "fig5-boundary.svg"), FigBoundary(aucEstablished, aucFresh));

      File.WriteAllText(Path.Combine(outDir, "metrics.json"), Json(metrics));

      // Console echo for CI logs / local validation.
      foreach (var kv in metrics) Console.WriteLine($"{kv.Key} = {kv.Value}");
      Console.WriteLine($"[figures] wrote 5 SVGs + metrics.json to {outDir}");
      return 0;
    }

    // ---------- model helpers -------------------------------------------------

    private static SwarmConfig Canonical() => new SwarmConfig();

    private static SwarmConfig WithCheaters(double frac) { var c = new SwarmConfig(); c.CheaterFraction = frac; return c; }

    private static double MeanAuc(SwarmConfig cfg) {
      double s = 0;
      for (int seed = 1; seed <= Seeds; seed++) {
        var swarm = SwarmSim.Generate(cfg, seed);
        var scores = AnomalyDetector.Score(swarm);
        var pos = new bool[swarm.Count];
        for (int i = 0; i < swarm.Count; i++) pos[i] = swarm.Peers[i].IsCheater;
        s += Roc.Auc(scores, pos);
      }
      return s / Seeds;
    }

    private static double MeanAlarm(SwarmConfig cfg) {
      double s = 0;
      for (int seed = 1; seed <= Seeds; seed++)
        s += Math.Abs(AnomalyDetector.SwarmAlarm(SwarmSim.Generate(cfg, seed)));
      return s / Seeds;
    }

    // Mean invariant residuals for an archetype, over all seeds.
    private static (double phys, double corr, double mass) InvariantMeans(SwarmConfig cfg) {
      double phys = 0, corr = 0, mass = 0;
      for (int seed = 1; seed <= Seeds; seed++) {
        var sw = SwarmSim.Generate(cfg, seed);
        double pSum = 0; int pN = 0, cN = 0; double cSum = 0;
        foreach (var p in sw.Peers) {
          pSum += Invariants.PhysicalResidual(p.DeclaredUp, p.WindowSeconds, p.LinkCapacityBytesPerSec); pN++;
          if (p.IsCheater) { cSum += Invariants.CorroborationDeficit(p.DeclaredUp, p.CorroboratedUp, sw.Coverage); cN++; }
        }
        // With no cheaters, report the honest corroboration deficit instead.
        if (cN == 0) { cSum = 0; foreach (var p in sw.Peers) { cSum += Invariants.CorroborationDeficit(p.DeclaredUp, p.CorroboratedUp, sw.Coverage); cN++; } }
        phys += pN > 0 ? pSum / pN : 0;
        corr += cN > 0 ? cSum / cN : 0;
        mass += Math.Abs(AnomalyDetector.SwarmAlarm(sw));
      }
      return (phys / Seeds, corr / Seeds, mass / Seeds);
    }

    private static (double[] frac, double[] mean, double[] median, double[] huber, double center)
      BreakdownCurves() {
      // A clean honest sample of normalized upload rates; a colluding minority injects
      // a far-away value. Watch which estimator holds.
      const int n = 240;
      const double center = 1.0, spread = 0.14, outlier = 11.0;
      var rng = new Random(20260807);
      var honest = new double[n];
      for (int i = 0; i < n; i++) honest[i] = center + spread * Gauss(rng);

      var fracs = new List<double>();
      var means = new List<double>(); var medians = new List<double>(); var hubers = new List<double>();
      for (double f = 0.0; f <= 0.601; f += 0.05) {
        int k = (int)Math.Round(n * f);
        var xs = (double[])honest.Clone();
        for (int i = 0; i < k; i++) xs[i] = outlier; // replace k honest values with the colluding value
        fracs.Add(f);
        means.Add(RobustEstimator.Mean(xs));
        medians.Add(RobustEstimator.Median(xs));
        hubers.Add(RobustEstimator.HuberLocation(xs));
      }
      return (fracs.ToArray(), means.ToArray(), medians.ToArray(), hubers.ToArray(), center);
    }

    // ---------- figures -------------------------------------------------------

    private static string FigInvariants() {
      var healthy = InvariantMeans(WithCheaters(0.0));
      var cNaive = Canonical(); cNaive.CheaterPhysicalOvershoot = 0.6;
      var naive = InvariantMeans(cNaive);
      var careful = InvariantMeans(Canonical());

      var groups = new[] {
        ("Healthy swarm", healthy), ("Naive cheaters", naive), ("Careful cheaters", careful)
      };
      var s = new Svg(720, 420, left: 66, bottom: 64);
      s.SetData(0, 3, 0, 1.0);
      s.AxesBox();
      s.YTicks(new[] { 0.0, 0.25, 0.5, 0.75, 1.0 }, v => v.ToString("0.##", C));
      s.Title("The three invariants across swarm archetypes");
      s.YLabel("mean residual (0 = consistent)");
      s.Legend(new[] {
        ("Physical implausibility", Svg.Blue),
        ("Corroboration deficit", Svg.Green),
        ("Mass-balance alarm |·|", Svg.Amber)
      });
      double bw = 26;
      for (int g = 0; g < groups.Length; g++) {
        double gx = g + 0.5;
        var (name, m) = groups[g];
        s.Bar(gx - 0.22, bw, Clamp01(m.phys), Svg.Blue);
        s.Bar(gx, bw, Clamp01(m.corr), Svg.Green);
        s.Bar(gx + 0.22, bw, Clamp01(m.mass), Svg.Amber);
        s.Text(PXgroup(gx, 720, 66, 24), 420 - 40, name, Svg.Ink, 13, "middle", false);
      }
      return s.End();
    }

    private static double PXgroup(double xData, int W, int left, int right) =>
      left + xData / 3.0 * (W - left - right);

    private static string FigRoc(SwarmConfig cfg) {
      // A representative ROC curve (one seed) plus the degraded thin-coverage case.
      var swarm = SwarmSim.Generate(cfg, 7);
      var pos = new bool[swarm.Count];
      for (int i = 0; i < swarm.Count; i++) pos[i] = swarm.Peers[i].IsCheater;
      var (fpr, tpr) = Roc.Curve(AnomalyDetector.Score(swarm), pos);

      var thin = Canonical(); thin.Coverage = 0.03; thin.PeerCount = 40;
      var sw2 = SwarmSim.Generate(thin, 7);
      var pos2 = new bool[sw2.Count];
      for (int i = 0; i < sw2.Count; i++) pos2[i] = sw2.Peers[i].IsCheater;
      var (f2, t2) = Roc.Curve(AnomalyDetector.Score(sw2), pos2);

      double aucA = MeanAuc(cfg);
      double aucB = MeanAuc(thin);

      var s = new Svg(460, 460, left: 62, bottom: 56, right: 20, top: 46);
      s.SetData(0, 1, 0, 1);
      s.AxesBox();
      s.YTicks(new[] { 0.0, 0.25, 0.5, 0.75, 1.0 }, v => v.ToString("0.##", C));
      s.XTicks(new[] { 0.0, 0.25, 0.5, 0.75, 1.0 }, v => v.ToString("0.##", C));
      s.Title("Detector ROC");
      s.XLabel("false-positive rate");
      s.YLabel("true-positive rate");
      s.Polyline(new[] { (0.0, 0.0), (1.0, 1.0) }, Svg.Grid, 1.5, dashed: true); // chance line
      s.Polyline(ToPts(fpr, tpr), Svg.Green, 2.6);
      s.Polyline(ToPts(f2, t2), Svg.Red, 2.4);
      s.Legend(new[] {
        ($"healthy deployment  (AUC {aucA.ToString("0.000", C)})", Svg.Green),
        ($"thin coverage + tiny swarm  (AUC {aucB.ToString("0.000", C)})", Svg.Red)
      });
      return s.End();
    }

    private static string FigBreakdown(double[] frac, double[] mean, double[] median, double[] huber, double center) {
      var s = new Svg(560, 420, left: 66, bottom: 56);
      double ymax = 12;
      s.SetData(0, 0.6, 0, ymax);
      s.AxesBox();
      s.YTicks(new[] { 0.0, 2.0, 4.0, 6.0, 8.0, 10.0, 12.0 }, v => v.ToString("0", C));
      s.XTicks(new[] { 0.0, 0.1, 0.2, 0.3, 0.4, 0.5, 0.6 }, v => v.ToString("0.#", C));
      s.Title("Breakdown: what a colluding minority moves");
      s.XLabel("fraction of swarm colluding");
      s.YLabel("estimated 'normal' upload rate");
      s.Polyline(new[] { (0.0, center), (0.6, center) }, Svg.Grid, 1.2, dashed: true);
      s.Polyline(ToPts(frac, mean), Svg.Red, 2.6);
      s.Polyline(ToPts(frac, huber), Svg.Green, 2.6);
      s.Polyline(ToPts(frac, median), Svg.Blue, 2.0, dashed: true);
      s.Legend(new[] {
        ("arithmetic mean (breakdown 0)", Svg.Red),
        ("Huber M-estimator (~0.5)", Svg.Green),
        ("median (~0.5)", Svg.Blue)
      });
      return s.End();
    }

    private static string FigPlateau(double tau) {
      var s = new Svg(560, 420, left: 74, bottom: 56);
      double xmax = 10, ymax = 14; // arbitrary work units
      s.SetData(0, xmax, 0, ymax);
      s.AxesBox();
      s.YTicks(new[] { 0.0, 2.0, 4.0, 6.0, 8.0, 10.0, 12.0, 14.0 }, v => v.ToString("0", C));
      s.XTicks(new[] { 0.0, 2.0, 4.0, 6.0, 8.0, 10.0 }, v => v.ToString("0", C));
      s.Title("No free ratio: credit is capped at a multiple of real work");
      s.XLabel("real upload actually served (work)");
      s.YLabel("max credited upload that survives");
      // y = x (honest) and y = x/(1-tau) (ceiling)
      s.Polyline(new[] { (0.0, 0.0), (xmax, xmax) }, Svg.Grid, 1.6, dashed: true);
      var ceil = new List<(double, double)>();
      for (double x = 0; x <= xmax + 1e-9; x += 0.5) ceil.Add((x, AsymmetryGame.MaxUndetectedDeclaredUp(x, tau)));
      s.Polyline(ceil, Svg.Green, 2.6);
      // Blind-client search points sit exactly on the ceiling.
      for (double x = 0.5; x <= xmax; x += 0.9)
        s.Point(x, AsymmetryGame.BlindClientBestDeclared(x, tau, ymax * 2), Svg.Red, 4.2);
      s.Legend(new[] {
        ($"detection ceiling  x/(1-τ),  τ={tau.ToString("0.##", C)}", Svg.Green),
        ("blind client's best effort", Svg.Red),
        ("honest y = x", Svg.Axis)
      });
      s.Text(PXd(6.2, 560, 74, 24, 0, xmax), PYd(0.9, 420, 46, 56, 0, ymax), "0 work → 0 credit", Svg.Ink, 12.5, "start", false);
      return s.End();
    }

    private static string FigBoundary(List<(double x, double y)> established, List<(double x, double y)> fresh) {
      var s = new Svg(560, 420, left: 66, bottom: 56);
      s.SetData(0, 0.4, 0.5, 1.0);
      s.AxesBox();
      s.YTicks(new[] { 0.5, 0.6, 0.7, 0.8, 0.9, 1.0 }, v => v.ToString("0.##", C));
      s.XTicks(new[] { 0.0, 0.1, 0.2, 0.3, 0.4 }, v => v.ToString("0.##", C));
      s.Title("Where the server design weakens (detection AUC)");
      s.XLabel("monitoring coverage  c");
      s.YLabel("detection AUC");
      s.Polyline(established, Svg.Green, 2.6);
      foreach (var p in established) s.Point(p.x, p.y, Svg.Green, 3.4);
      s.Polyline(fresh, Svg.Red, 2.6);
      foreach (var p in fresh) s.Point(p.x, p.y, Svg.Red, 3.4);
      s.Legend(new[] {
        ("established torrent (30-min window)", Svg.Green),
        ("fresh torrent (90-s window)", Svg.Red)
      });
      return s.End();
    }

    // ---------- small numeric utilities --------------------------------------

    private static IReadOnlyList<(double, double)> ToPts(double[] xs, double[] ys) {
      var l = new List<(double, double)>(xs.Length);
      for (int i = 0; i < xs.Length; i++) l.Add((xs[i], ys[i]));
      return l;
    }

    // Direct pixel mapping helpers for free-floating annotations (mirror Svg's internal map).
    private static double PXd(double xData, int W, int left, int right, double x0, double x1) =>
      left + (xData - x0) / (x1 - x0) * (W - left - right);
    private static double PYd(double yData, int H, int top, int bottom, double y0, double y1) =>
      (H - bottom) - (yData - y0) / (y1 - y0) * (H - top - bottom);

    private static double Interp(double[] xs, double[] ys, double x) {
      for (int i = 1; i < xs.Length; i++) {
        if (x <= xs[i]) {
          double t = (x - xs[i - 1]) / (xs[i] - xs[i - 1]);
          return ys[i - 1] + t * (ys[i] - ys[i - 1]);
        }
      }
      return ys[ys.Length - 1];
    }

    private static double Clamp01(double v) => Math.Min(1.0, Math.Max(0.0, v));
    private static double Round(double v) => Math.Round(v, 4);

    private static double Gauss(Random rng) {
      double u1 = 1.0 - rng.NextDouble();
      double u2 = 1.0 - rng.NextDouble();
      return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    private static string Json(Dictionary<string, object> m) {
      var b = new StringBuilder();
      b.Append("{\n");
      int i = 0;
      foreach (var kv in m) {
        string val = kv.Value is double d ? d.ToString("0.####", C) : Convert.ToString(kv.Value, C);
        b.Append($"  \"{kv.Key}\": {val}");
        b.Append(++i < m.Count ? ",\n" : "\n");
      }
      b.Append("}\n");
      return b.ToString();
    }
  }
}
