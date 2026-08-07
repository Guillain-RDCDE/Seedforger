using System;

namespace Seedforger.Integrity {

  /// <summary>Knobs for one synthetic swarm. Defaults describe a healthy mid-size private-tracker swarm.</summary>
  public sealed class SwarmConfig {
    public int PeerCount = 200;
    public double SeederFraction = 0.35;   // of the honest population
    public double CheaterFraction = 0.15;  // of the whole swarm
    public double Coverage = 0.15;          // c: fraction of genuine upload witnessed by monitoring peers

    public double WindowSeconds = 1800.0;   // a 30-minute announce window
    public double MeanLinkCapacityBytesPerSec = 1_500_000.0; // ~1.5 MB/s uplinks
    public double LinkCapacitySpread = 0.45;
    public double MeanUtilization = 0.35;   // honest peers use ~35% of their uplink on average
    public double UtilizationSpread = 0.35;
    public double ReportingNoise = 0.03;    // honest declared-vs-true jitter (fractional)

    // A realistic ratio-cheater does *some* genuine seeding and inflates on top of it —
    // the interesting adversary, not a strawman. Declared upload = own real work × inflation.
    public double CheaterRealFraction = 0.4;      // real upload a cheater serves, as a fraction of an honest peer's
    public double CheaterInflationFactor = 4.0;   // how much they inflate over their OWN real work
    public double CheaterPhysicalOvershoot = 0.0; // 0 = careful (claims a fast link); >0 = naive (implausible speed)
  }

  /// <summary>
  /// A swarm-in-a-jar. Honest peers conserve mass (every byte one peer downloads,
  /// another uploaded), report near their truth, and get witnessed by monitoring
  /// peers at the coverage rate. Cheaters declare a large upload they never served,
  /// so their corroboration collapses. Corroboration is sampled at *piece* rather
  /// than byte granularity — that is what makes small swarms and low coverage genuinely
  /// noisy, which is where the papers' boundary lives. Deterministic in the seed.
  /// </summary>
  public static class SwarmSim {

    public const long PieceSize = 262144; // 256 KiB, a common torrent piece size

    public static Swarm Generate(SwarmConfig cfg, int seed) {
      var rng = new Random(seed);
      var swarm = new Swarm { Coverage = cfg.Coverage };
      int n = Math.Max(0, cfg.PeerCount);
      if (n == 0) return swarm;

      int nCheat = (int)Math.Round(n * Clamp(cfg.CheaterFraction, 0.0, 1.0));
      int nHonest = n - nCheat;
      int nSeed = (int)Math.Round(nHonest * Clamp(cfg.SeederFraction, 0.0, 1.0));

      // --- Honest population -------------------------------------------------
      // Draw link capacities and each honest leecher's real download demand.
      var honest = new SwarmPeer[nHonest];
      double totalDemand = 0.0;      // bytes the swarm really pulled this window
      double totalUploadWeight = 0.0; // capacity-proportional serving weight
      for (int i = 0; i < nHonest; i++) {
        bool isSeeder = i < nSeed;
        double link = cfg.MeanLinkCapacityBytesPerSec * Math.Max(0.15, 1.0 + cfg.LinkCapacitySpread * Gauss(rng));
        var p = new SwarmPeer {
          Id = i, IsSeeder = isSeeder, IsCheater = false,
          WindowSeconds = cfg.WindowSeconds, LinkCapacityBytesPerSec = link
        };
        if (!isSeeder) {
          double util = Math.Max(0.0, cfg.MeanUtilization * Math.Max(0.05, 1.0 + cfg.UtilizationSpread * Gauss(rng)));
          double demand = Math.Min(0.95, util) * link * cfg.WindowSeconds; // a leecher can't pull past its own downlink≈uplink proxy
          p.TrueDown = (long)demand;
          totalDemand += p.TrueDown;
        }
        honest[i] = p;
        totalUploadWeight += link;
      }

      // Distribute the swarm's real download demand back out as upload, proportional
      // to capacity, so that Σ trueUp == Σ trueDown (mass is conserved by construction).
      double meanTrueUp = 0.0;
      if (nHonest > 0 && totalUploadWeight > 0.0) {
        for (int i = 0; i < nHonest; i++) {
          var p = honest[i];
          double share = p.LinkCapacityBytesPerSec / totalUploadWeight;
          double up = totalDemand * share;
          double cap = p.LinkCapacityBytesPerSec * cfg.WindowSeconds;
          if (up > cap) up = cap; // never claim past the physical ceiling
          p.TrueUp = (long)up;
          // Report near the truth, with a little human jitter.
          p.DeclaredUp = NonNeg((long)(p.TrueUp * (1.0 + cfg.ReportingNoise * Gauss(rng))));
          p.DeclaredDown = NonNeg((long)(p.TrueDown * (1.0 + cfg.ReportingNoise * Gauss(rng))));
          p.CorroboratedUp = WitnessedBytes(p.TrueUp, cfg.Coverage, rng);
          meanTrueUp += p.TrueUp;
        }
        meanTrueUp /= nHonest;
      }
      double meanDeclUpHonest = 0.0;
      for (int i = 0; i < nHonest; i++) meanDeclUpHonest += honest[i].DeclaredUp;
      if (nHonest > 0) meanDeclUpHonest /= nHonest;
      // Fallbacks when there is no honest population to calibrate against.
      if (meanDeclUpHonest <= 0.0)
        meanDeclUpHonest = cfg.MeanUtilization * cfg.MeanLinkCapacityBytesPerSec * cfg.WindowSeconds;
      if (meanTrueUp <= 0.0) meanTrueUp = meanDeclUpHonest;

      foreach (var p in honest) swarm.Peers.Add(p);

      // --- Cheaters ----------------------------------------------------------
      for (int i = 0; i < nCheat; i++) {
        // Genuine work the cheater actually serves, then an inflation on top of it.
        double realFrac = Math.Max(0.05, cfg.CheaterRealFraction * Math.Max(0.15, 1.0 + 0.4 * Gauss(rng)));
        long trueUp = NonNeg((long)(meanTrueUp * realFrac));
        double inflate = Math.Max(1.1, cfg.CheaterInflationFactor * Math.Max(0.4, 1.0 + 0.15 * Gauss(rng)));
        long declaredUp = NonNeg((long)(trueUp * inflate));

        double claimedSpeed = declaredUp / cfg.WindowSeconds;
        double link;
        if (cfg.CheaterPhysicalOvershoot > 0.0)
          link = claimedSpeed / (1.0 + cfg.CheaterPhysicalOvershoot); // naive: speed exceeds the claimed link
        else
          link = claimedSpeed * 1.25 + 1.0; // careful: claims a fast enough link to stay plausible

        swarm.Peers.Add(new SwarmPeer {
          Id = nHonest + i, IsSeeder = false, IsCheater = true,
          WindowSeconds = cfg.WindowSeconds, LinkCapacityBytesPerSec = link,
          TrueUp = trueUp, TrueDown = 0,
          DeclaredUp = declaredUp, DeclaredDown = 0,
          CorroboratedUp = WitnessedBytes(trueUp, cfg.Coverage, rng)
        });
      }

      return swarm;
    }

    /// <summary>
    /// Bytes of a peer's genuine upload that monitoring peers witness. Sampled at
    /// piece granularity: witnessed pieces ~ Binomial(pieces, coverage), so the
    /// relative noise grows like √((1−c)/(c·pieces)) — negligible for a big honest
    /// seeder, dominant for a tiny swarm or thin coverage.
    /// </summary>
    internal static long WitnessedBytes(long trueUp, double coverage, Random rng) {
      if (trueUp <= 0 || coverage <= 0.0) return 0;
      long pieces = trueUp / PieceSize;
      if (pieces <= 0) {
        // Sub-piece upload: a single Bernoulli witness of the partial piece.
        return (rng.NextDouble() < coverage) ? trueUp : 0;
      }
      double mean = pieces * coverage;
      double sd = Math.Sqrt(pieces * coverage * (1.0 - coverage));
      long witnessed = (long)Math.Round(mean + sd * Gauss(rng));
      if (witnessed < 0) witnessed = 0;
      if (witnessed > pieces) witnessed = pieces;
      return witnessed * PieceSize;
    }

    private static long NonNeg(long v) => v < 0 ? 0 : v;
    private static double Clamp(double v, double lo, double hi) => Math.Min(hi, Math.Max(lo, v));

    /// <summary>Standard normal via Box–Muller, driven only by the seeded RNG (fully deterministic).</summary>
    private static double Gauss(Random rng) {
      double u1 = 1.0 - rng.NextDouble();
      double u2 = 1.0 - rng.NextDouble();
      return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }
  }
}
