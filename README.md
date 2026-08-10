<div align="center">

# Seedforger

### The most complete ratio-faking BitTorrent client we could build — shipped with the formal proof that it optimises the wrong variable.

An **executable research artifact**, not a product: a maximally believable ratio spoofer, and — in the same repository — the reproducible science showing why the client side is a game you cannot win.

[![CI](https://github.com/Guillain-RDCDE/Seedforger/actions/workflows/ci.yml/badge.svg)](https://github.com/Guillain-RDCDE/Seedforger/actions/workflows/ci.yml)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Release](https://img.shields.io/github/v/release/Guillain-RDCDE/Seedforger?color=2ea043&label=release)](../../releases/latest)
[![Tests](https://img.shields.io/badge/tests-172%20passing-2ea043)](Seedforger.Tests)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

<br>

[![Read the science](https://img.shields.io/badge/Read_the_science-No_Free_Ratio-1f6feb?style=for-the-badge)](https://guillain-rdcde.github.io/Seedforger/) &nbsp;
[![Run the artifact](https://img.shields.io/badge/Run_the_artifact-2ea043?style=for-the-badge)](../../releases/latest) &nbsp;
[![Docs](https://img.shields.io/badge/Docs-30363d?style=for-the-badge)](docs/getting-started.md)

</div>

---

## What this is

Seedforger is two things at once, on purpose:

1. **The illustration** — the most complete *ratio-faking* BitTorrent client we knew how to build. It announces fabricated upload/download to a tracker while transferring nothing, impersonating a real, current client down to the `peer_id` and `User-Agent`, shaping speeds and timing so the whole story stays believable.
2. **The point** — in the same repository, the **formal, reproducible proof that even a perfect version of that client is capped.** A tracker doesn't *trust* your number, it *reconciles* it; once you write that reconciliation down as maths, no amount of client polish beats it.

So this isn't a tool to reach for — it's a **study piece**. The most interesting thing the client can tell you is that it doesn't work, and it ships with the maths to prove it.

> [!WARNING]
> Educational and security-research artifact. Faking your ratio breaks the rules of virtually every private tracker and can get you banned. Nothing here makes fake stats *undetectable* — the whole conclusion is the opposite. Use only where you are permitted to. **You are responsible for what you do with it.**

<br>

<div align="center">

## The point — *No Free Ratio*

**We built the most careful ratio client we knew how to build, then proved — inside its own repository — that even a perfect one optimises the wrong variable.**

[![Read the illustrated story](https://img.shields.io/badge/Read_the_illustrated_story-No_Free_Ratio-2ea043?style=for-the-badge)](https://guillain-rdcde.github.io/Seedforger/)

<br>

<a href="https://guillain-rdcde.github.io/Seedforger/"><img src="docs/papers/figures/fig1-invariants.svg" width="620" alt="The three integrity invariants across a healthy swarm, naive cheaters and careful cheaters"></a>

<sub>A tracker doesn't <em>trust</em> your number — it <em>reconciles</em> it. You can fake how fast you claim to be; you cannot fake that nobody received it.</sub>

</div>

Two short, **reproducible** papers turn Seedforger into an object of study. They're backed by a runnable, deterministic model ([`Seedforger.Integrity`](Seedforger.Integrity)) and guarded by the test suite, so **the figures can't drift from the code**.

|  | Paper | In one line |
|:--:|---|---|
| **1** | [**Reconciling the swarm**](docs/papers/01-swarm-integrity.md) | Ratio cheating as **robust anomaly detection** on the tracker — the three invariants, an estimator that survives a colluding minority, and exactly how well it works. |
| **2** | [**The client can't win**](docs/papers/02-client-asymmetry.md) | The credited number is computed from a signal you **can't see or forge**, so client-side optimisation is capped at a fixed multiple of *real work* — and at zero work, **zero credit**. |

<div align="center">

| Detector AUC, healthy swarm | Weak spot *(fresh torrent + thin coverage)* | Max inflation over real work *(τ = 0.2)* | Credit for zero real upload |
|:--:|:--:|:--:|:--:|
| **≈ 1.00** | **≈ 0.82** | **1.25×** | **0** |

**[Illustrated story](https://guillain-rdcde.github.io/Seedforger/)** · **[The papers](docs/papers/)** · **[The model](Seedforger.Integrity)**

</div>

> The honest corollary the papers land on: the only lever that raises the ceiling is *real work* — actually serving bytes to real peers. At which point you haven't beaten the system, you've just become a torrent client. That's the joke, and it's the whole point.

<br>

## The illustration, up close

The client half exists to make the proof concrete: it is a genuinely serious attempt at believable ratio faking, so that "even a perfect client is capped" isn't hand-waving. Here is what "as careful as we could make it" actually means.

| | |
|---|---|
| **Client impersonation** — 50 clients with accurate `peer_id` / `User-Agent` fingerprints, verified against libtorrent, with rotation and Transmission checksums. | **Swarm-aware realism** — reported speeds scale with the tracker's live leecher/seeder counts; no demand means a trickle, not an implausible claim. |
| **Stealth** — speed ramp-up with variation, announce-interval jitter, a day/night rhythm, active-hours windows, and believability warnings. | **Real peer-wire engine** — optionally serve genuine, SHA-1-verified blocks over TCP, capped by a statistical governor, to satisfy monitoring peers. |
| **Goal-seeking campaigns** — set a ratio or a volume-by-deadline; it staggers starts, allocates bandwidth by demand, paces, and stops itself. | **Daemon + web dashboard** — run a folder 24/7 on a seedbox/NAS behind a self-contained dark dashboard and JSON API (BEP-12, `completed`, `min interval`). |
| **Guided setup** — a wizard that probes each torrent and loops until it finds one that will genuinely earn ratio, then applies safe defaults. | **Connectivity** — HTTPS trackers over `SslStream`, SOCKS4/4a/5 & HTTP-CONNECT proxies, magnet links, batch loading, and DNS-over-HTTPS. |
| **Cross-platform** — the WinForms-free core drives a headless **CLI** and an **Avalonia GUI** on Windows, Linux & macOS. | **Tested** — **172 xUnit tests**, green CI on Windows + Linux, including a peer-wire integration test and an end-to-end CLI run against a live mock tracker. |

Every one of these techniques answers a specific tracker check; the papers then show, formally, that answering all of them still leaves the client below a fixed ceiling. The full catalogue is in [Features](docs/features.md).

## Run it

You can run the illustration yourself — a single file, no installer.

| Download | Size | Requires |
|---|:--:|---|
| **`Seedforger-lite.exe`** &nbsp;·&nbsp; *recommended* | ~0.5 MB | the free [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0/runtime) |
| **`Seedforger.exe`** | ~68 MB | nothing — fully self-contained |

Grab a build from the [**latest release**](../../releases/latest). The **graphical interface** and a **headless command line** drive the same engine — see [Getting started](docs/getting-started.md) and [Command line](docs/cli.md).

```bash
# Dry-run one announce and print what the tracker says
./Seedforger.Cli --test-announce -t movie.torrent --client qBittorrent

# Reproduce the science-annex figures from the model
dotnet run --project Seedforger.Integrity.Figures -c Release -- docs/papers/figures
```

## How the illustration works

A tracker cannot watch you upload; it trusts your reported numbers. But private trackers run anti-cheat, so a single large number is easy to flag — the wrong fingerprint, an impossible speed, robotic timing, figures that don't reconcile with scrape data, or a port with no real peer behind it. The client's job is to answer each of those checks so the tracker-visible story stays consistent and human-shaped. It does **not** make fakery undetectable — and [the science](docs/papers/) proves exactly why the client side is a capped game.

The full model, without code, is in [How it actually works](docs/how-it-works.md); the byte-level detail is in [How BitTorrent actually works](docs/how-bittorrent-works.md).

> [!NOTE]
> **Field report:** [*Ten Days on a Real Tracker*](STORY.md) — the client held a live private tracker for ten days with zero flags. A good story, and exactly the kind of short-run result the science explains is not a durable win.

## Documentation

| Page | Contents |
|---|---|
| **[Science annex](docs/papers/)** | **The heart of the project: two reproducible papers + an [illustrated story](https://guillain-rdcde.github.io/Seedforger/) — why perfecting the client is a losing game.** |
| [Getting started](docs/getting-started.md) | Install, guided setup, the rules that keep you safe, FAQ. |
| [Command line](docs/cli.md) | Every flag for headless / scripted use. |
| [Daemon & web dashboard](docs/daemon.md) | Run 24/7 on a seedbox/NAS with a live browser dashboard. |
| [Install & packaging](docs/packaging.md) | Per-platform binaries, package managers, code signing. |
| [How it actually works](docs/how-it-works.md) | The anti-cheat model and the believability response, no code. |
| [Features](docs/features.md) | The complete feature catalogue. |
| [Configuration](docs/configuration.md) | Custom fingerprints (`clients.json`) and campaigns (`campaign.json`). |
| [Build from source](docs/build.md) | Build, publish, project layout, tests. |
| [How BitTorrent actually works](docs/how-bittorrent-works.md) | A from-the-wire technical deep dive. |

## Build

Requires the .NET 8 SDK.

```bash
dotnet build Seedforger.sln -c Release
dotnet test  Seedforger.Tests/Seedforger.Tests.csproj
```

Full notes in [Build from source](docs/build.md).

## Lineage

**RatioMaster** → [NikolayIT/RatioMaster.NET](https://github.com/NikolayIT/RatioMaster.NET) (MIT) → [sergiye/RatioMaster](https://github.com/sergiye/RatioMaster) → **Seedforger**: a .NET 8 rewrite that took the old idea as far as it could go — a modern client database, HTTPS, swarm-aware announces, a real peer-wire engine, campaigns, guided setup, i18n, portable settings — and then answered the question the old tools never asked, with a formal science annex.

## License

[MIT](LICENSE). Provided as-is, without warranty.
