# Seedforger — reference

[← Back to the README](../README.md)

---

## What it does

The client half is a genuinely serious attempt at believable ratio faking — so that *"even a perfect client is capped"* isn't hand-waving. Every technique here answers a specific tracker check:

| | |
|---|---|
| **Client impersonation** — 50 clients with accurate `peer_id` / `User-Agent` fingerprints, verified against libtorrent, with rotation and Transmission checksums. | **Swarm-aware realism** — reported speeds scale with the tracker's live leecher/seeder counts; no demand means a trickle, not an implausible claim. |
| **Stealth** — speed ramp-up with variation, announce-interval jitter, a day/night rhythm, active-hours windows, and believability warnings. | **Real peer-wire engine** — optionally serve genuine, SHA-1-verified blocks over TCP, capped by a statistical governor, to satisfy monitoring peers. |
| **Goal-seeking campaigns** — set a ratio or a volume-by-deadline; it staggers starts, allocates bandwidth by demand, paces, and stops itself. | **Daemon + web dashboard** — run a folder 24/7 on a seedbox/NAS behind a self-contained dark dashboard and JSON API. |
| **Guided setup** — a wizard that probes each torrent and loops until it finds one that will genuinely earn ratio, then applies safe defaults. | **Connectivity** — HTTPS trackers over `SslStream`, SOCKS4/4a/5 & HTTP-CONNECT proxies, magnet links, batch loading, DNS-over-HTTPS. |
| **Cross-platform** — a WinForms-free core drives a headless **CLI** and an **Avalonia GUI** on Windows, Linux & macOS. | **Tested** — **172 xUnit tests**, green CI on Windows + Linux, incl. a peer-wire integration test and an end-to-end CLI run against a live mock tracker. |

Full catalogue in [Features](../docs/features.md).

<br>

<div align="center">

## The science — *No Free Ratio*

**We built the most careful ratio client we knew how to build, then proved — inside its own repository — that even a perfect one optimises the wrong variable.**

<a href="https://guillain-rdcde.github.io/Seedforger/"><img src="../docs/papers/figures/fig1-invariants.svg" width="620" alt="The three integrity invariants across a healthy swarm, naive cheaters and careful cheaters"></a>

<sub>A tracker doesn't <em>trust</em> your number — it <em>reconciles</em> it. You can fake how fast you claim to be; you cannot fake that nobody received it.</sub>

</div>

Two short, **reproducible** papers, backed by a runnable deterministic model ([`src/Seedforger.Integrity`](../src/Seedforger.Integrity)) and guarded by the test suite — so **the figures can't drift from the code**.

|  | Paper | In one line |
|:--:|---|---|
| **1** | [**Reconciling the swarm**](../docs/papers/01-swarm-integrity.md) | Ratio cheating as **robust anomaly detection** on the tracker — the three invariants, an estimator that survives a colluding minority, and exactly how well it works. |
| **2** | [**The client can't win**](../docs/papers/02-client-asymmetry.md) | The credited number is computed from a signal you **can't see or forge**, so client-side optimisation is capped at a fixed multiple of *real work* — and at zero work, **zero credit**. |

<div align="center">

| Detector AUC, healthy swarm | Weak spot *(fresh torrent + thin coverage)* | Max inflation over real work *(τ = 0.2)* | Credit for zero real upload |
|:--:|:--:|:--:|:--:|
| **≈ 1.00** | **≈ 0.82** | **1.25×** | **0** |

**[Illustrated story](https://guillain-rdcde.github.io/Seedforger/)** · **[The papers](../docs/papers/)** · **[The model](../src/Seedforger.Integrity)**

</div>

> The corollary the papers land on: the only lever that raises the ceiling is *real work* — actually serving bytes to real peers. At which point you haven't beaten the system, you've become a torrent client. That's the joke, and it's the whole point.

> [!NOTE]
> **Field report:** [*Ten Days on a Real Tracker*](../STORY.md) — the client held a live private tracker for ten days with zero flags. A good story, and exactly the kind of short-run result the science explains is not a durable win.

## Documentation

| Page | Contents |
|---|---|
| [Getting started](../docs/getting-started.md) | Install, guided setup, the rules that keep you safe, FAQ. |
| [Command line](../docs/cli.md) | Every flag for headless / scripted use. |
| [Daemon & web dashboard](../docs/daemon.md) | Run 24/7 on a seedbox/NAS with a live browser dashboard. |
| [How it actually works](../docs/how-it-works.md) | The anti-cheat model and the believability response — no code. |
| [How BitTorrent actually works](../docs/how-bittorrent-works.md) | A from-the-wire technical deep dive. |
| [Features](../docs/features.md) | The complete feature catalogue. |
| [Configuration](../docs/configuration.md) | Custom fingerprints (`clients.json`) and campaigns (`campaign.json`). |
| [Install & packaging](../docs/packaging.md) | Per-platform binaries, package managers, code signing. |
| [Build from source](../docs/build.md) | Build, publish, project layout, tests. |
| **[Science annex](../docs/papers/)** | **The two reproducible papers + [illustrated story](https://guillain-rdcde.github.io/Seedforger/).** |

## Build

Requires the **.NET 8 SDK**.

```bash
dotnet build Seedforger.sln -c Release
dotnet test  tests/Seedforger.Tests/Seedforger.Tests.csproj
```

The repository uses a standard `src/` + `tests/` layout; full notes in [Build from source](../docs/build.md).

