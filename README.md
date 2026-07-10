<div align="center">

# 🌱 Seedforger

### Tell any BitTorrent tracker whatever upload/download stats you want — without moving a single byte.

A modern, from-the-ground-up **.NET 8** revival of the classic *RatioMaster*, rebuilt as a **believability engine** — not just a number generator.

![Seedforger](.github/social-preview.png)

[![CI](https://github.com/Guillain-RDCDE/Seedforger/actions/workflows/ci.yml/badge.svg)](https://github.com/Guillain-RDCDE/Seedforger/actions/workflows/ci.yml)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Release](https://img.shields.io/github/v/release/Guillain-RDCDE/Seedforger?color=2ea043&label=download)](../../releases/latest)
[![Tests](https://img.shields.io/badge/tests-108%20passing-2ea043)](Seedforger.Tests)
[![Platform](https://img.shields.io/badge/platform-Windows-0078D6?logo=windows&logoColor=white)](../../releases/latest)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

**[Get started](docs/getting-started.md) · [How it works](docs/how-it-works.md) · [Features](docs/features.md) · [From-the-wire deep dive](docs/how-bittorrent-works.md)**

<table>
  <tr>
    <td width="50%"><img src="docs/screenshots/main-dark.png" alt="Seedforger main window, dark theme"></td>
    <td width="50%"><img src="docs/screenshots/campaign-builder.png" alt="Visual campaign builder"></td>
  </tr>
  <tr>
    <td align="center"><em>One tab per torrent · realistic speeds · live log · light &amp; dark</em></td>
    <td align="center"><em>Visual campaign builder — set a goal, no JSON</em></td>
  </tr>
  <tr>
    <td width="50%"><img src="docs/screenshots/guided-setup.png" alt="Guided setup — do you have it?"></td>
    <td width="50%"><img src="docs/screenshots/guided-ready.png" alt="Guided setup — ready to seed"></td>
  </tr>
  <tr>
    <td align="center"><em>Guided setup teaches the one rule that keeps you safe…</em></td>
    <td align="center"><em>…then probes the swarm and only starts on a torrent that works</em></td>
  </tr>
</table>

</div>

---

## What is this?

A torrent tracker keeps score of how much you upload — but it can't actually *watch* you upload. It just **believes the number your client reports**. Seedforger is a client that reports whatever number you tell it: give it a `.torrent`, say *"pretend I'm seeding,"* and it quietly tells the tracker that story, over and over, so your stats climb. No files are transferred; it doesn't even need a torrent client installed.

But sending a fake number is the easy part — **anyone can do that, and it gets you banned.** The whole point of Seedforger is making the number *believable*: shaping speeds like a real home line, tying them to the swarm's real demand, keeping announce timing human, staying under a statistical governor, and — if you want to go all the way — running a **real peer-wire engine that serves genuine, hash-verified data**, so even a tracker's monitoring spies see a legitimate peer.

It's equal parts a working tool and a guided tour of how BitTorrent's trust model really works.

> [!WARNING]
> **Educational / security-research tool.** Faking your ratio breaks the rules of virtually every private tracker and **can get you banned**. Nothing here makes fake stats *undetectable* — it makes them *consistent and human-shaped*. Only use it where you're allowed to. You alone are responsible for what you do with it.

---

## Table of contents

- [✨ Highlights](#highlights)
- [⬇️ Download](#download)
- [🚀 Quick start](#quick-start)
- [🧭 How it actually works (no code)](#how-it-works)
- [🛡️ The believability ladder](#ladder)
- [🔬 Going all the way: the real peer engine](#real-peer-engine)
- [✨ Feature tour](#features)
- [🎯 Campaigns](#campaigns)
- [⚙️ Configuration](#configuration)
- [🏗️ Architecture](#architecture)
- [🔧 Build &amp; test](#build)
- [📚 Documentation](#documentation)
- [🌱 Lineage &amp; credits](#credits)

---

<a id="highlights"></a>
## ✨ Highlights

- 🎭 **Impersonates real, current clients** — data-driven database of **47** clients with accurate `peer_id` + `User-Agent` fingerprints (qBittorrent, Transmission 4, Deluge 2, libtorrent…), verified against libtorrent's `generate_fingerprint` and each client's source.
- 🌊 **Swarm-aware realism** — reads the tracker's live leecher/seeder counts and scales your reported speed to match. Zero leechers ⇒ a trickle; nobody to feed, nothing claimed.
- 🕵️ **Stealth by default** — ramp-up + gentle wobble instead of a flat line, announce-interval jitter, a day/night rhythm, active-hours windows, and believability warnings.
- 🔬 **A real peer-wire engine** — optionally serve genuine, SHA-1-verified blocks over TCP, capped by a statistical governor, to defeat trackers that inject spy peers.
- 🎯 **Goal-seeking campaigns** — hand it *"reach ratio 2.0 in two weeks"* and it staggers starts, splits bandwidth by real demand, paces to the deadline, and auto-stops.
- 🧭 **Guided setup (newbie mode)** — a wizard that probes each torrent against the tracker and loops until it finds one that will actually earn ratio, then sets safe defaults and starts.
- 💾 **Portable & self-contained** — a single `.exe`, no installer, no registry. Everything lives in `settings.json` next to it.
- 🎨 **Polished UX** — flat light/dark themes (DWM dark title bar), English/French, a live upload/ratio graph, and a silent auto-update check.
- 🔒 **HTTPS + proxy** — full TLS trackers via `SslStream`, and SOCKS4/4a/5 + HTTP-CONNECT.
- ✅ **108 tests, green CI** — fingerprints, bencode, the shaper, swarm/stealth math, a loopback peer-wire integration test, and more.

---

<a id="download"></a>
## ⬇️ Download

Grab a build from the **[latest release](../../releases/latest)** — pick the one that suits you:

| Download | Size | Starts up | Needs |
|---|---|---|---|
| ⭐ **`Seedforger-lite.exe`** *(recommended)* | ~0.5 MB | **fastest** | the free [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0/runtime) (one-time install) |
| **`Seedforger.exe`** | ~68 MB | slower to launch | **nothing at all** — fully self-contained |

Either way it's a **single file, no installer** — drop it anywhere (USB stick included) and double-click. Seedforger also checks for newer releases silently at launch and offers the update if there is one.

> **Which one?** The self-contained build bundles the whole .NET runtime, so it's big and your antivirus rescans all 68 MB every launch. The lite build is a tiny 0.5 MB, starts ~2× faster, and just asks you to install the runtime once. Not sure if you have .NET? Grab lite first — Windows will offer you the installer if it's missing.

---

<a id="quick-start"></a>
## 🚀 Quick start

**New here? Use the wizard.** Open **File → Guided setup (newbie mode)** and it walks you through everything one click at a time — and, crucially, **asks the tracker whether each torrent will actually work** before you commit. → full walkthrough in **[Getting started](docs/getting-started.md)**.

**Prefer to drive?**

1. **Browse…** and pick a `.torrent` you actually have.
2. Set **Finished = 100** (you're a seeder) and pick a **Connection profile** (Settings) for a believable speed.
3. Choose a client — **qBittorrent 5.2.3** is a great, modern default.
4. Hit the green **START**. Watch the **Ratio** and the log climb; **STOP** when done.

> **The golden rule:** only seed files you *actually downloaded once, for real.* A tracker's monitoring peers will request real pieces — claiming to seed what you can't deliver is the surest way to get caught. The [golden rules](docs/getting-started.md#4-the-golden-rules-stay-believable) list the rest.

---

<a id="how-it-works"></a>
## 🧭 How it actually works (no code)

**1. The tracker trusts you.** Every few minutes a real client reports *"I'm client X, I've now uploaded N bytes."* The tracker has **no way to independently measure** your upload — it just adds up what you send. That trust is the entire attack surface.

**2. So why not just send a huge number?** Because private trackers run anti-cheat, and a lone big number is easy to spot:

| The tracker asks… | …and a naïve faker fails because |
|---|---|
| *Is this a real, allowed client?* | its `peer_id`/`User-Agent` isn't whitelisted, or they disagree |
| *Is this speed physically possible?* | it "uploaded" faster than any home line, or more than the file's size |
| *Is the timing human?* | it announces like a metronome, or more often than allowed |
| *Do the numbers add up?* | announce totals don't match the scrape data or `left` |
| *Is there a real peer behind this?* | the port never accepts a connection; no monitored peer ever got a byte |

**3. Seedforger's job is to pass every one of those checks** — which is the believability ladder below. And **4. the honest limit:** none of it is magic. A tracker that correlates announces against *real* peer connections can still catch you. → the full model, in prose, lives in **[How it actually works](docs/how-it-works.md)**.

---

<a id="ladder"></a>
## 🛡️ The believability ladder

From cheap to deep — each rung answers one of the tracker's questions:

| # | Rung | What it does |
|---|---|---|
| 1 | **Look like a real client** | accurate, *current* fingerprints (`-qB5230-`, `-TR4130-`…), optional client rotation, even Transmission's `peer_id` checksum |
| 2 | **Move at a believable speed** | connection profiles (ADSL→fibre), a ramp-up + gentle wobble instead of a flat line, and a global upstream budget shared across all tabs |
| 3 | **Only feed demand that exists** | swarm-aware speeds read the live leecher/seeder counts — 0 leechers ⇒ a trickle, your share diluted by competing seeders |
| 4 | **Keep timing human** | interval jitter (drift *later*, never early), a day/night rhythm, and active-hours windows |
| 5 | **Be a connectable peer** | answer inbound handshakes with a full bitfield + choke — a visible, complete seeder that transfers nothing |
| 6 | **Survive active spies** | serve genuine, hash-valid data with the real peer engine, and claim only `served × plausible peers` — [see below](#real-peer-engine) |

---

<a id="real-peer-engine"></a>
## 🔬 Going all the way: the real peer engine

An announce-only tool has one irreducible tell: a tracker can inject **monitoring peers** that don't just read your bitfield — they *request* real pieces and check you deliver. The only way past them is to **serve real data**. Seedforger can (*File → Serve a real file*), leaning on one elegant fact: **a spy only ever sees its own connection.** Serve real data at a steady per-peer rate, claim a total of `served × plausible peers`, and no single observer can refute it.

<details>
<summary><strong>The four stages + the governor</strong></summary>

| Stage | What it does |
|---|---|
| **A — serve from a local file** | `FilePieceSource` reads blocks and **verifies each piece's SHA-1** before serving; `PeerSession` runs handshake → bitfield → unchoke → `piece`. |
| **B — relay on demand** | `RelayPieceSource` serves pieces you don't hold by fetching them from a real seeder, verifying, caching, relaying — a swarm proxy that stores no whole file. |
| **C — behave like a real peer** | `SeederChoke` round-robins unchoke slots; the **BEP 10** extension handshake advertises `ut_pex` / `ut_metadata`. |
| **D — verifiable transfer** | `PeerClient` performs a real handshake + block download + hash-verify — the thing a spy actually measures. |
| **The governor** | `Governor.CapAnnounced` keeps the claim ≤ `served × plausible peers`, so you never claim more than you could defend. |

> Past this point, "faking convincingly" quietly converges on *being a real (if lazy) BitTorrent client that moves real data* — which is the honest note the whole exercise ends on. Scope: **TCP-only** (no µTP), PEX/metadata built but not live-negotiated, validated over loopback. Full write-up: [deep dive §13½](docs/how-bittorrent-works.md#13-the-deep-end-actually-participating-in-the-swarm).

</details>

---

<a id="features"></a>
## ✨ Feature tour

<details open>
<summary><strong>🎭 Realistic client impersonation</strong></summary>

- **Client database** — data-driven profiles (**47 clients**), extensible via an external [`clients.json`](docs/configuration.md#custom--updated-fingerprints-without-rebuilding), no recompile.
- **Modern fingerprints** — qBittorrent, Transmission, Deluge, libtorrent, µTorrent + the legacy zoo; verified to the source.
- **peer_id fidelity** — client-specific quirks incl. **Transmission's checksum**, so ids validate byte-for-byte.
- **Client rotation** — optionally look like a fresh client every start.
- **Byte-accurate HTTP** — header order and `User-Agent` hand-built to match, even over TLS.
</details>

<details>
<summary><strong>🕵️ Believability &amp; stealth</strong></summary>

- **`SpeedShaper`** — ramp-up + mean-reverting random walk instead of flat noise.
- **Interval jitter** — drift *later* (0–12%), never earlier than the tracker's `interval`.
- **Day/night rhythm + active hours** — a diurnal curve and an optional window (handles the midnight wrap).
- **Believability warnings** — flags physically implausible setups at START.
- **Connectable seeder** — full bitfield then a choke on inbound handshakes.
</details>

<details>
<summary><strong>🌊 Swarm-aware realism</strong></summary>

- **Swarm-aware speeds** — scaled by real leecher/seeder counts.
- **Global upstream budget** — one uplink, shared fairly across all tabs.
- **Statistical governor** — announced upload capped to `served × plausible peers`.
</details>

<details>
<summary><strong>🔌 Connectivity, inputs &amp; UX</strong></summary>

- **HTTPS trackers** (`SslStream`, byte-accurate raw request) · **Proxy** (SOCKS4/4a/5 + HTTP-CONNECT).
- **Magnet & batch** — infohash-only magnets, or a whole folder of `.torrent`s into tabs.
- **Auto-stop targets** — time, uploaded, downloaded, ratio, or seeders/leechers.
- **Dry-run** — *Test announce* sends one announce and shows the tracker's verdict.
- **Guided setup**, **light/dark themes**, **English/French**, **live graph**, **portable settings**, **silent update check**.
</details>

*Full catalogue with every detail: **[Features](docs/features.md)**.*

---

<a id="campaigns"></a>
## 🎯 Campaigns

Instead of babysitting tabs, hand Seedforger an *intent*. **File → New campaign…** opens a visual builder (no JSON): a goal (*ratio 2.0* or *200 GB by a deadline*), a connection profile, active hours, and a torrent folder. It then **staggers** the starts, **splits the upstream toward torrents that actually have leechers**, **paces** the total toward the deadline so you don't finish suspiciously early, and **auto-stops** at the goal — because launching everything at once, flat out, is exactly what a bot looks like.

Under the hood it's a `campaign.json` you can Save/Load — see [Configuration → Campaigns](docs/configuration.md#campaigns-goal-seeking-orchestrator).

---

<a id="configuration"></a>
## ⚙️ Configuration

Two optional JSON files, both dropped next to the exe on first launch, both editable without recompiling:

- **`clients.json`** — add or override client fingerprints (ship tomorrow's qBittorrent the day it releases).
- **`campaign.json`** — declarative campaigns (or just use the visual builder).

Formats and examples: **[Configuration](docs/configuration.md)**.

---

<a id="architecture"></a>
## 🏗️ Architecture

```
Seedforger/
├─ Program.cs                 entry point, single-instance, code-page provider
├─ TorrentClientFactory.cs    data-driven client lookup + clients.json merge
├─ DefaultClientProfiles.cs   the 47 built-in client profiles
├─ SpeedShaper.cs             realistic ramp-up / speed variation
├─ Stealth.cs · SwarmModel.cs · Bandwidth.cs   believability + swarm + budget
├─ HttpsTransport.cs          TLS transport for https:// trackers
├─ UpdateChecker.cs           silent GitHub release check at launch
├─ Theme.cs · Localization.cs · GraphForm.cs   themes, i18n, live graph
├─ GuideForm.cs               guided setup (newbie mode) wizard
├─ Peer/                      the real peer-wire engine (stages A–D + governor)
├─ Campaign/                  orchestrator + visual builder (CampaignForm)
├─ BitTorrent/                bencode + .torrent parsing
├─ BytesRoads/                SOCKS / HTTP-CONNECT proxy sockets
└─ RM.cs · MainForm.cs        the WinForms UI
Seedforger.Tests/             108 xUnit tests
docs/                         getting-started · how-it-works · features · configuration · build · the deep dive
```

---

<a id="build"></a>
## 🔧 Build & test

Requires the **.NET 8 SDK** (`dotnet --version` ≥ 8).

```bash
dotnet build Seedforger.sln -c Release
dotnet test  Seedforger.Tests/Seedforger.Tests.csproj      # 108 tests

# lite single-file exe (needs the .NET 8 Desktop runtime installed)
dotnet publish Seedforger/Seedforger.csproj -c Release -r win-x64 \
  --self-contained false -p:PublishSingleFile=true
```

Full build/publish notes, the startup-size gotcha, and the test breakdown: **[Build from source](docs/build.md)**.

---

<a id="documentation"></a>
## 📚 Documentation

| Page | What's inside |
|---|---|
| 🚀 **[Getting started](docs/getting-started.md)** | install, guided setup, the golden rules, FAQ |
| 🧭 **[How it actually works](docs/how-it-works.md)** | the anti-cheat model and the believability response — no code |
| ✨ **[Features](docs/features.md)** | the full catalogue, every detail |
| ⚙️ **[Configuration](docs/configuration.md)** | `clients.json` and `campaign.json` |
| 🔧 **[Build from source](docs/build.md)** | build, publish, project layout, tests |
| 📖 **[How BitTorrent actually works](docs/how-bittorrent-works.md)** | a from-the-wire deep dive: bencode → the peer engine |

---

<a id="credits"></a>
## 🌱 Lineage & credits

Seedforger stands on a long open-source lineage:

**RatioMaster** → [NikolayIT/RatioMaster.NET](https://github.com/NikolayIT/RatioMaster.NET) (MIT) → [sergiye/RatioMaster](https://github.com/sergiye/RatioMaster) → **Seedforger**

— a .NET 8 rewrite that adds a modern client database, HTTPS, realistic & swarm-aware announces, a real peer-wire engine, a goal-seeking campaign orchestrator, a guided newbie mode, i18n, themes, portable settings, and a redesigned UI. Keeping the MIT lineage intact.

## License

[MIT](LICENSE) — provided **as-is**, with no warranty. Do the right thing.

<div align="center"><sub>Built with care. If this taught you something about how BitTorrent trust really works, it did its job. 🌱</sub></div>
