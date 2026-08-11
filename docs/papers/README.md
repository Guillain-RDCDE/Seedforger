# Science annex — why perfecting the client is a losing game

Seedforger is the most careful client of its kind we know how to build. These two
short papers are the honest counterweight: a **formal account of why even a perfect
client optimises the wrong variable**, and where — precisely — that argument runs out.

They are not hand-waving. Every claim is backed by a small, deterministic model that
lives in [`Seedforger.Integrity`](../../Seedforger.Integrity) and is guarded by the
project's xUnit suite. Every figure and every number quoted below is regenerated from
that model by [`Seedforger.Integrity.Figures`](../../Seedforger.Integrity.Figures), so
the prose cannot drift from the code.

Each paper is written twice over: a **plain-language** part you can read cold, and a
**formal** part with the definitions, the estimator, and the proof sketch.

| Paper | In one line |
|---|---|
| **[1 — Reconciling the swarm](01-swarm-integrity.md)** | A mature tracker doesn't *trust* your number, it *reconciles* it — modelling ratio cheating as robust anomaly detection, and measuring exactly how well it works. |
| **[2 — The client can't win](02-client-asymmetry.md)** | The credited number is computed from a signal the client can neither read nor forge, so client-side optimisation is capped at a fixed multiple of *real work* — and at zero work, zero credit. |

Together they close a loop: Paper 1 builds the estimator, Paper 2 proves that a
well-built estimator turns the client's game into one it cannot win — except in the
boundary regimes Paper 1 measures (a fresh torrent, thin monitoring coverage).

## The model, in five files

| File | What it is |
|---|---|
| [`Swarm.cs`](../../Seedforger.Integrity/Swarm.cs) | One announce window: what each peer *did* vs what it *declared*, plus the sliver a monitor witnessed. |
| [`Invariants.cs`](../../Seedforger.Integrity/Invariants.cs) | The three reconciliation checks — physical plausibility, swarm mass balance, corroboration deficit. |
| [`RobustEstimator.cs`](../../Seedforger.Integrity/RobustEstimator.cs) | Mean vs median vs Huber M-estimator, and the breakdown point that separates them. |
| [`AnomalyDetector.cs`](../../Seedforger.Integrity/AnomalyDetector.cs) | Turns invariants into a per-peer score; ROC / AUC evaluation. |
| [`AsymmetryGame.cs`](../../Seedforger.Integrity/AsymmetryGame.cs) | The client/server information game and its closed-form ceiling. |

## Reproducing the figures

```bash
# from the repository root
dotnet run --project src/Seedforger.Integrity.Figures -c Release -- docs/papers/figures
```

This rewrites the five SVGs and [`figures/metrics.json`](figures/metrics.json). CI runs
the same command on every push and fails if it crashes, so the figures are never stale.
The numbers behind the prose are the ones in `metrics.json`:

| Metric | Value | Meaning |
|---|---|---|
| `auc_careful` | ~1.00 | detection of careful cheaters in a healthy deployment |
| `auc_fresh_c002` | ~0.82 | the same detector on a fresh torrent with thin coverage — the weak spot |
| `mass_alarm_healthy` → `mass_alarm_cheated` | ~0.001 → ~0.13 | the swarm-level balance alarm going off |
| `game_credit_multiplier` | 1.25 | the most a cheater can inflate over **real work** at tolerance τ=0.2 |
| `game_free_ratio_at_zero_work` | 0 | credit for zero real upload — there is none |

> These papers are defensive and theoretical: they describe how a tracker protects
> itself and prove a limit on the client side. Nothing here is an evasion recipe — the
> conclusion is that the interesting, durable engineering is on the honest-seeding side,
> which is exactly where Seedforger's real peer-wire engine already sits.
