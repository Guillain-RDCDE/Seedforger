<div align="center">

# Seedforger

### Fake your BitTorrent upload/download stats on any tracker — the most complete ratio spoofer we could build, shipped with the proof it can't actually win.

A modern **.NET 8** revival of RatioMaster: it tells a tracker you uploaded gigabytes while transferring nothing, impersonating a real client down to the last byte. And — in the same repository — the reproducible science showing why the client side is a game you cannot win.

[![CI](https://github.com/Guillain-RDCDE/Seedforger/actions/workflows/ci.yml/badge.svg)](https://github.com/Guillain-RDCDE/Seedforger/actions/workflows/ci.yml)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Release](https://img.shields.io/github/v/release/Guillain-RDCDE/Seedforger?color=2ea043&label=release)](../../releases/latest)
[![Tests](https://img.shields.io/badge/tests-172%20passing-2ea043)](tests/Seedforger.Tests)
[![Platforms](https://img.shields.io/badge/Windows%20·%20Linux%20·%20macOS-30363d)](../../releases/latest)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

<br>

[![Download](https://img.shields.io/badge/⬇_Download-2ea043?style=for-the-badge)](../../releases/latest) &nbsp;
[![Getting started](https://img.shields.io/badge/Getting_started-30363d?style=for-the-badge)](docs/getting-started.md) &nbsp;
[![Read the science](https://img.shields.io/badge/Read_the_science-No_Free_Ratio-1f6feb?style=for-the-badge)](https://guillain-rdcde.github.io/Seedforger/)

<img src="docs/screenshots/main.png" width="760" alt="The Seedforger main window: client picker, live ratio, and per-torrent up/down">

</div>

---

## What it is

A tracker can't watch you upload — it trusts the numbers your client reports. Seedforger reports **fabricated** ones. It announces invented upload/download to a tracker while transferring nothing, impersonating a real, current client down to the `peer_id` and `User-Agent`, and shaping speed and timing so the whole story stays believable to anti-cheat.

It's also **honest about its own limits**: shipped alongside the client is a small, reproducible research annex — two short papers and a runnable model — that proves, formally, that even a *perfect* version of this client is capped. A tracker doesn't *trust* your number, it *reconciles* it; once you write that reconciliation down as maths, no amount of client polish beats it.

So you get both: a genuinely serious ratio spoofer, **and** the proof of why the honest move is to actually seed. Use the first; believe the second.

> [!WARNING]
> **Educational and security-research artifact.** Faking your ratio breaks the rules of virtually every private tracker and can get you banned. Nothing here makes fake stats *undetectable* — the whole conclusion is the opposite. Use only where you are permitted to. **You are responsible for what you do with it.**

## Run it in 30 seconds

No installer — a single file. Grab the build for your platform from the [**latest release**](../../releases/latest):

| Platform | Download | Requires |
|---|---|---|
| **Windows** (recommended) | `Seedforger-lite-win-x64.exe` &nbsp;·&nbsp; ~0.5 MB | the free [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0/runtime) |
| **Windows** (portable) | `Seedforger-win-x64.exe` &nbsp;·&nbsp; ~68 MB | nothing — fully self-contained |
| **Linux / macOS** | `Seedforger-gui-*` (GUI) · `Seedforger-cli-*` (headless) | nothing — self-contained |

The **graphical app** and a **headless command line** drive the same engine:

```bash
# Dry-run one announce and print exactly what the tracker says back
./Seedforger.Cli --test-announce -t movie.torrent --client qBittorrent
```

New to this? The [**guided setup**](docs/getting-started.md) probes each torrent and walks you to one that will genuinely earn ratio, with safe defaults.

## What it does

The client half is a genuinely serious attempt at believable ratio faking — so that *"even a perfect client is capped"* isn't hand-waving. Every technique here answers a specific tracker check:

| | |
|---|---|
| **Client impersonation** — 50 clients with accurate `peer_id` / `User-Agent` fingerprints, verified against libtorrent, with rotation and Transmission checksums. | **Swarm-aware realism** — reported speeds scale with the tracker's live leecher/seeder counts; no demand means a trickle, not an implausible claim. |
| **Stealth** — speed ramp-up with variation, announce-interval jitter, a day/night rhythm, active-hours windows, and believability warnings. | **Real peer-wire engine** — optionally serve genuine, SHA-1-verified blocks over TCP, capped by a statistical governor, to satisfy monitoring peers. |
| **Goal-seeking campaigns** — set a ratio or a volume-by-deadline; it staggers starts, allocates bandwidth by demand, paces, and stops itself. | **Daemon + web dashboard** — run a folder 24/7 on a seedbox/NAS behind a self-contained dark dashboard and JSON API. |
| **Guided setup** — a wizard that probes each torrent and loops until it finds one that will genuinely earn ratio, then applies safe defaults. | **Connectivity** — HTTPS trackers over `SslStream`, SOCKS4/4a/5 & HTTP-CONNECT proxies, magnet links, batch loading, DNS-over-HTTPS. |
| **Cross-platform** — a WinForms-free core drives a headless **CLI** and an **Avalonia GUI** on Windows, Linux & macOS. | **Tested** — **172 xUnit tests**, green CI on Windows + Linux, incl. a peer-wire integration test and an end-to-end CLI run against a live mock tracker. |

Full catalogue in [Features](docs/features.md).

<br>

<div align="center">

## The science — *No Free Ratio*

**We built the most careful ratio client we knew how to build, then proved — inside its own repository — that even a perfect one optimises the wrong variable.**

<a href="https://guillain-rdcde.github.io/Seedforger/"><img src="docs/papers/figures/fig1-invariants.svg" width="620" alt="The three integrity invariants across a healthy swarm, naive cheaters and careful cheaters"></a>

<sub>A tracker doesn't <em>trust</em> your number — it <em>reconciles</em> it. You can fake how fast you claim to be; you cannot fake that nobody received it.</sub>

</div>

Two short, **reproducible** papers, backed by a runnable deterministic model ([`src/Seedforger.Integrity`](src/Seedforger.Integrity)) and guarded by the test suite — so **the figures can't drift from the code**.

|  | Paper | In one line |
|:--:|---|---|
| **1** | [**Reconciling the swarm**](docs/papers/01-swarm-integrity.md) | Ratio cheating as **robust anomaly detection** on the tracker — the three invariants, an estimator that survives a colluding minority, and exactly how well it works. |
| **2** | [**The client can't win**](docs/papers/02-client-asymmetry.md) | The credited number is computed from a signal you **can't see or forge**, so client-side optimisation is capped at a fixed multiple of *real work* — and at zero work, **zero credit**. |

<div align="center">

| Detector AUC, healthy swarm | Weak spot *(fresh torrent + thin coverage)* | Max inflation over real work *(τ = 0.2)* | Credit for zero real upload |
|:--:|:--:|:--:|:--:|
| **≈ 1.00** | **≈ 0.82** | **1.25×** | **0** |

**[Illustrated story](https://guillain-rdcde.github.io/Seedforger/)** · **[The papers](docs/papers/)** · **[The model](src/Seedforger.Integrity)**

</div>

> The corollary the papers land on: the only lever that raises the ceiling is *real work* — actually serving bytes to real peers. At which point you haven't beaten the system, you've become a torrent client. That's the joke, and it's the whole point.

> [!NOTE]
> **Field report:** [*Ten Days on a Real Tracker*](STORY.md) — the client held a live private tracker for ten days with zero flags. A good story, and exactly the kind of short-run result the science explains is not a durable win.

## Documentation

| Page | Contents |
|---|---|
| [Getting started](docs/getting-started.md) | Install, guided setup, the rules that keep you safe, FAQ. |
| [Command line](docs/cli.md) | Every flag for headless / scripted use. |
| [Daemon & web dashboard](docs/daemon.md) | Run 24/7 on a seedbox/NAS with a live browser dashboard. |
| [How it actually works](docs/how-it-works.md) | The anti-cheat model and the believability response — no code. |
| [How BitTorrent actually works](docs/how-bittorrent-works.md) | A from-the-wire technical deep dive. |
| [Features](docs/features.md) | The complete feature catalogue. |
| [Configuration](docs/configuration.md) | Custom fingerprints (`clients.json`) and campaigns (`campaign.json`). |
| [Install & packaging](docs/packaging.md) | Per-platform binaries, package managers, code signing. |
| [Build from source](docs/build.md) | Build, publish, project layout, tests. |
| **[Science annex](docs/papers/)** | **The two reproducible papers + [illustrated story](https://guillain-rdcde.github.io/Seedforger/).** |

## Build

Requires the **.NET 8 SDK**.

```bash
dotnet build Seedforger.sln -c Release
dotnet test  tests/Seedforger.Tests/Seedforger.Tests.csproj
```

The repository uses a standard `src/` + `tests/` layout; full notes in [Build from source](docs/build.md).

## Lineage

**RatioMaster** → [NikolayIT/RatioMaster.NET](https://github.com/NikolayIT/RatioMaster.NET) (MIT) → [sergiye/RatioMaster](https://github.com/sergiye/RatioMaster) → **Seedforger**: a .NET 8 rewrite that took the old idea as far as it could go — a modern client database, HTTPS, swarm-aware announces, a real peer-wire engine, campaigns, guided setup, i18n, portable settings — and then answered the question the old tools never asked, with a formal science annex.

## License

[MIT](LICENSE). Provided as-is, without warranty.
