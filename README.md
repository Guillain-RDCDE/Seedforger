<div align="center">

<img src=".github/social-preview.png" width="100%" alt="Seedforger — fake your BitTorrent ratio on any tracker, shipped with the proof it can't win">

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
| **Windows** (portable) | `Seedforger-win-x64.exe` &nbsp;·&nbsp; ~24 MB | nothing — fully self-contained |
| **Linux / macOS** | `Seedforger-gui-*` (GUI) · `Seedforger-cli-*` (headless) | nothing — self-contained |

The **graphical app** and a **headless command line** drive the same engine:

```bash
# Dry-run one announce and print exactly what the tracker says back
./Seedforger.Cli --test-announce -t movie.torrent --client qBittorrent
```

New to this? The [**guided setup**](docs/getting-started.md) probes each torrent and walks you to one that will genuinely earn ratio, with safe defaults.

## More

**[Reference](docs/REFERENCE.md)** — what it does in detail, the *No Free Ratio* science annex, the documentation index, and how to build it.

## Lineage

**RatioMaster** → [NikolayIT/RatioMaster.NET](https://github.com/NikolayIT/RatioMaster.NET) (MIT) → [sergiye/RatioMaster](https://github.com/sergiye/RatioMaster) → **Seedforger**: a .NET 8 rewrite that took the old idea as far as it could go — a modern client database, HTTPS, swarm-aware announces, a real peer-wire engine, campaigns, guided setup, i18n, portable settings — and then answered the question the old tools never asked, with a formal science annex.

## License

[MIT](LICENSE). Provided as-is, without warranty.
