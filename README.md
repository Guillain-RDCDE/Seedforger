<div align="center">

# 🌱 Seedforger

**Tell any BitTorrent tracker whatever upload/download stats you want — without moving a single byte.**

A modern **.NET 8** revival of the classic *RatioMaster*, rebuilt as a believability engine.

![Seedforger](.github/social-preview.png)

[![CI](https://github.com/Guillain-RDCDE/Seedforger/actions/workflows/ci.yml/badge.svg)](https://github.com/Guillain-RDCDE/Seedforger/actions/workflows/ci.yml)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Release](https://img.shields.io/github/v/release/Guillain-RDCDE/Seedforger?color=2ea043)](../../releases/latest)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

<img src="docs/screenshots/main-dark.png" width="440" alt="Seedforger — one tab per torrent, live log, light & dark themes">

</div>

---

Seedforger announces **fake progress** (uploaded / downloaded / left) to a torrent tracker, impersonating a **real, current BitTorrent client** — matching `peer_id` and `User-Agent` — so the announce looks exactly like the real thing. No files are ever transferred; only the numbers the tracker sees are made up. It runs **independently of your torrent client** — you don't even need one installed.

The hard part isn't sending a fake number — anyone can do that, and it gets you banned. The hard part is making it **believable**: realistic speeds, swarm-aware demand, human timing, and even a real peer-wire engine that serves hash-verified data. That's what this project is about. → **[How it actually works](docs/how-it-works.md)**

> [!WARNING]
> **Educational / security-research tool.** Faking your ratio breaks the rules of virtually every private tracker and **can get you banned**. Nothing here makes fake stats undetectable. Only use it where you are allowed to.

## Download

Grab a build from the **[latest release](../../releases/latest)**:

| | Size | Needs |
|---|---|---|
| ⭐ **`Seedforger-lite.exe`** *(recommended)* | ~0.5 MB | the free [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0/runtime) |
| **`Seedforger.exe`** | ~68 MB | nothing — fully self-contained |

Single file, no installer. New here? Open **File → Guided setup (newbie mode)** and it walks you through everything.
→ **[Getting started](docs/getting-started.md)**

## Documentation

- 🚀 **[Getting started](docs/getting-started.md)** — install, guided setup, the golden rules, FAQ.
- 🧭 **[How it actually works](docs/how-it-works.md)** — the anti-cheat model and how Seedforger stays believable. No code.
- ✨ **[Features](docs/features.md)** — the full catalogue: impersonation, stealth, swarm-aware speeds, the real peer engine, campaigns.
- ⚙️ **[Configuration](docs/configuration.md)** — custom client fingerprints (`clients.json`) and campaigns (`campaign.json`).
- 🔧 **[Build from source](docs/build.md)** — build, publish, project layout, tests.
- 📖 **[How BitTorrent actually works](docs/how-bittorrent-works.md)** — a from-the-wire deep dive, bencode to the peer engine.

## Credits & license

Lineage: **RatioMaster** → [NikolayIT/RatioMaster.NET](https://github.com/NikolayIT/RatioMaster.NET) → [sergiye/RatioMaster](https://github.com/sergiye/RatioMaster) → **Seedforger** (a .NET 8 rewrite with a modern client database, HTTPS, swarm-aware announces, a real peer-wire engine, a campaign orchestrator, i18n, themes and a redesigned UI).

[MIT](LICENSE) — provided **as-is**, no warranty. Do the right thing.
