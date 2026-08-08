<div align="center">

# Seedforger

### Report any upload/download stats you want to a BitTorrent tracker — without transferring a single byte.

A modern, from-the-ground-up **.NET 8** revival of the classic *RatioMaster*, built around **believability** rather than raw numbers.

[![CI](https://github.com/Guillain-RDCDE/Seedforger/actions/workflows/ci.yml/badge.svg)](https://github.com/Guillain-RDCDE/Seedforger/actions/workflows/ci.yml)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Release](https://img.shields.io/github/v/release/Guillain-RDCDE/Seedforger?color=2ea043&label=download)](../../releases/latest)
[![Tests](https://img.shields.io/badge/tests-172%20passing-2ea043)](Seedforger.Tests)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

<br>

[![Download](https://img.shields.io/badge/Download-2ea043?style=for-the-badge)](../../releases/latest) &nbsp;
[![Read the science](https://img.shields.io/badge/Read_the_science-No_Free_Ratio-1f6feb?style=for-the-badge)](https://guillain-rdcde.github.io/Seedforger/) &nbsp;
[![Docs](https://img.shields.io/badge/Docs-30363d?style=for-the-badge)](docs/getting-started.md)

<br>

<img src="docs/screenshots/main.png" width="540" alt="Seedforger">

<sub>A flat, from-the-ground-up interface — plus a full headless command line.</sub>

</div>

---

A torrent tracker keeps score of how much you upload, but it **cannot independently measure it** — it simply trusts the number your client reports. Seedforger reports whatever number you tell it, impersonating a real, current BitTorrent client (matching `peer_id` and `User-Agent`), while **transferring no files** and running independently of any torrent client.

Sending a fake number is trivial; making it **believable** is the point. Seedforger shapes reported speeds like a real connection, ties them to the swarm's actual demand, keeps announce timing human, and — optionally — runs a real peer-wire engine that serves genuine, hash-verified data so that even a tracker's monitoring peers see a legitimate seeder.

> [!WARNING]
> Educational and security-research tool. Faking your ratio breaks the rules of virtually every private tracker and can get you banned. None of these techniques make fake stats undetectable — they make them internally consistent. Use only where you are permitted to. **You are responsible for what you do with it.**

<br>

<div align="center">

## The science — *No Free Ratio*

**We built the most careful ratio client we knew how to build — then proved, inside its own repository, that even a *perfect* client optimises the wrong variable.**

[![Read the story](https://img.shields.io/badge/Read_the_illustrated_story-No_Free_Ratio-2ea043?style=for-the-badge)](https://guillain-rdcde.github.io/Seedforger/)

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

<br>

## Download

Grab a build from the [**latest release**](../../releases/latest) — a single file, no installer.

| Download | Size | Requires |
|---|:--:|---|
| **`Seedforger-lite.exe`** &nbsp;·&nbsp; *recommended* | ~0.5 MB | the free [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0/runtime) |
| **`Seedforger.exe`** | ~68 MB | nothing — fully self-contained |

New users should start with the **Guided** button, which walks through setup and verifies each torrent against the tracker before starting. There are two ways to run the same engine: the **graphical interface** (default) and a **headless command line** for automation — see [Getting started](docs/getting-started.md) and [Command line](docs/cli.md).

## Features

| | |
|---|---|
| **Client impersonation** — 50 clients with accurate `peer_id` / `User-Agent` fingerprints, verified against libtorrent, with rotation and Transmission checksums. | **Swarm-aware realism** — reported speeds scale with the tracker's live leecher/seeder counts; no demand means a trickle, not an implausible claim. |
| **Stealth** — speed ramp-up with variation, announce-interval jitter, a day/night rhythm, active-hours windows, and believability warnings. | **Real peer-wire engine** — optionally serve genuine, SHA-1-verified blocks over TCP, capped by a statistical governor, to satisfy monitoring peers. |
| **Goal-seeking campaigns** — set a ratio or a volume-by-deadline; it staggers starts, allocates bandwidth by demand, paces, and stops itself. | **Daemon + web dashboard** — run a folder 24/7 on a seedbox/NAS behind a self-contained dark dashboard and JSON API (BEP-12, `completed`, `min interval`). |
| **Guided setup** — a wizard that probes each torrent and loops until it finds one that will genuinely earn ratio, then applies safe defaults. | **Connectivity** — HTTPS trackers over `SslStream`, SOCKS4/4a/5 & HTTP-CONNECT proxies, magnet links, batch loading, and DNS-over-HTTPS. |
| **Cross-platform** — the WinForms-free core drives a headless **CLI** and an **Avalonia GUI** on Windows, Linux & macOS. | **Tested** — **172 xUnit tests**, green CI on Windows + Linux, including a peer-wire integration test and an end-to-end CLI run against a live mock tracker. |

The full catalogue is in [Features](docs/features.md).

## How it works

A tracker cannot watch you upload; it trusts your reported numbers. But private trackers run anti-cheat, so a single large number is easy to flag — the wrong fingerprint, an impossible speed, robotic timing, figures that don't reconcile with scrape data, or a port with no real peer behind it. Seedforger's job is to answer each of those checks so the tracker-visible story stays consistent and human-shaped. It does **not** make fakery undetectable — and [the science](docs/papers/) proves exactly why the client side is a capped game.

The full model, without code, is in [How it actually works](docs/how-it-works.md); the byte-level detail is in [How BitTorrent actually works](docs/how-bittorrent-works.md).

> [!NOTE]
> **Field report:** [*Ten Days on a Real Tracker*](STORY.md) — how Seedforger held a live private tracker for ten days, turning a failing ratio into a healthy one with zero flags, and what it taught us about believability.

## Documentation

| Page | Contents |
|---|---|
| [Getting started](docs/getting-started.md) | Install, guided setup, the rules that keep you safe, FAQ. |
| [Command line](docs/cli.md) | Every flag for headless / scripted use. |
| [Daemon & web dashboard](docs/daemon.md) | Run 24/7 on a seedbox/NAS with a live browser dashboard. |
| [Install & packaging](docs/packaging.md) | Per-platform binaries, package managers, code signing. |
| [How it actually works](docs/how-it-works.md) | The anti-cheat model and the believability response, no code. |
| **[Science annex](docs/papers/)** | **Two reproducible papers + an [illustrated story](https://guillain-rdcde.github.io/Seedforger/): why perfecting the client is a losing game.** |
| [Features](docs/features.md) | The complete feature catalogue. |
| [Configuration](docs/configuration.md) | Custom fingerprints (`clients.json`) and campaigns (`campaign.json`). |
| [Build from source](docs/build.md) | Build, publish, project layout, tests. |
| [How BitTorrent actually works](docs/how-bittorrent-works.md) | A from-the-wire technical deep dive. |

## Command line

The WinForms-free core (`Seedforger.Core`) makes the headless CLI build and run on **Linux, macOS and Windows** — ideal for a seedbox, a server or CI. It ships as a self-contained single file. Full reference in [Command line](docs/cli.md).

```bash
# Dry-run one announce and print what the tracker says
./Seedforger.Cli --test-announce -t movie.torrent --client qBittorrent

# Seed headless at ~800 kB/s for two hours, then stop
./Seedforger.Cli -t movie.torrent -u 800 --duration 120

# Run a whole folder 24/7 on a seedbox, behind a live web dashboard
./Seedforger.Cli --folder ~/torrents --daemon -u 1500 --randomize-client
```

`--daemon` (or `--folder`) serves a self-contained dark **web dashboard** — live totals, per-torrent ratio, swarm counts, a Stop button — plus a JSON API at `/api/status`. See [Daemon & web dashboard](docs/daemon.md).

## Build

Requires the .NET 8 SDK.

```bash
dotnet build Seedforger.sln -c Release
dotnet test  Seedforger.Tests/Seedforger.Tests.csproj

# reproduce the science-annex figures from the model
dotnet run   --project Seedforger.Integrity.Figures -c Release -- docs/papers/figures
```

Full notes in [Build from source](docs/build.md).

## Lineage

**RatioMaster** → [NikolayIT/RatioMaster.NET](https://github.com/NikolayIT/RatioMaster.NET) (MIT) → [sergiye/RatioMaster](https://github.com/sergiye/RatioMaster) → **Seedforger**: a .NET 8 rewrite adding a modern client database, HTTPS, swarm-aware announces, a real peer-wire engine, a campaign orchestrator, a guided setup, i18n, themes, portable settings — and a formal science annex.

## License

[MIT](LICENSE). Provided as-is, without warranty.
