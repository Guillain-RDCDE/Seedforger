# Features

[← back to the README](../README.md)

For *why* these matter, read [How it actually works](how-it-works.md); for the protocol detail, the [from-the-wire deep dive](how-bittorrent-works.md).

---

## 🎭 Realistic client impersonation

| | |
|---|---|
| **Client database** | Data-driven profiles (**50 clients**), not a hard-coded switch. Add or override any client via an external [`clients.json`](configuration.md#custom--updated-fingerprints-without-rebuilding) — **no recompile**. |
| **Modern fingerprints** | qBittorrent `-qB`, Transmission `-TR`, Deluge `-DE`, libtorrent `-LT`, µTorrent, plus the whole legacy zoo. Verified against libtorrent's `generate_fingerprint` and each client's source. |
| **peer_id fidelity** | Reproduces client-specific quirks, including **Transmission's peer_id checksum**, so ids validate byte-for-byte. |
| **Client rotation** | Optionally pick a fresh modern client on every start, so you don't always look like the same machine. |
| **Byte-accurate HTTP** | Header order and `User-Agent` are hand-built to match the impersonated client exactly — including over TLS. |

## 🕵️ Believability & stealth

| | |
|---|---|
| **Realistic announces** | `SpeedShaper` applies a ramp-up + mean-reverting random walk instead of flat noise, so reported speeds look human. |
| **Interval jitter** | Announce timing drifts *later* (0–12%), never earlier than the tracker's `interval` — no metronome. |
| **Day/night rhythm + active hours** | A diurnal speed curve plus an optional active-hours window (handles the midnight wrap, e.g. `22–6`). |
| **Believability warnings** | Logs a warning at START on physically implausible setups (absurd upstream, upload ≫ download). |
| **Connectable seeder** | Answers inbound handshakes on your port with a full **bitfield then a choke** — a connectable, complete seeder that transfers nothing. |

## 🌊 Swarm-aware realism

| | |
|---|---|
| **Swarm-aware speeds** | Upload/download scaled by the tracker's **real leecher/seeder counts** — 0 leechers ⇒ a trickle, your share diluted by competing seeders. Makes the numbers physically plausible. |
| **Global upstream budget** | A connection profile caps your **total** upload across *all* tabs — one uplink, shared fairly, like a real line. |
| **Statistical governor** | In real-seed mode, the announced upload is capped to `served × plausible peers` — you never claim more than you could defend. |

## 🔬 The real peer-wire engine (advanced)

*File → Serve a real file* arms a genuine TCP **peer-wire engine** — the answer to trackers that inject monitoring peers which *request-and-verify*. See the [deep-dive §13½](how-bittorrent-works.md#deep-end).

| Stage | What it does |
|---|---|
| **A — serve from a local file** | `FilePieceSource` reads blocks and **verifies each piece's SHA-1** before serving; `PeerSession` runs handshake → bitfield → unchoke → `piece`. |
| **B — relay on demand** | `RelayPieceSource` serves pieces you don't hold by fetching them from a real seeder, verifying, caching, relaying — a swarm proxy that stores no whole file. |
| **C — behave like a real peer** | `SeederChoke` round-robins unchoke slots; the **BEP 10** extension handshake advertises `ut_pex` / `ut_metadata`. |
| **D — verifiable transfer** | `PeerClient` performs a real handshake + block download + hash-verify — the thing a spy actually measures. |
| **The governor** | `Governor.CapAnnounced` keeps the claim ≤ what was actually served × a plausible peer count. |

> Scope: **TCP-only** (no µTP), PEX/metadata are built but not live-negotiated, validated over loopback — not against live swarms.

## 🎯 Campaign orchestration

| | |
|---|---|
| **Goal-seeking campaigns** | Give it an intent — *reach ratio 2.0* or *upload 200 GB by a deadline* — and it derives the actions over time. |
| **Visual builder** | *File → New campaign…* opens a themed form: goal, connection profile, active hours, torrent folder, stagger, concurrency. **No JSON by hand.** |
| **Human pacing** | **Staggered** starts (launching everything at once is a tell), upstream **budget split by real demand**, **pacing** so you don't finish suspiciously early, then **auto-stop** at the goal. |

See [Configuration → Campaigns](configuration.md#campaigns-goal-seeking-orchestrator) for the `campaign.json` format.

## 🔌 Connectivity & inputs

| | |
|---|---|
| **HTTPS trackers** | Full TLS via `SslStream`, sending a raw hand-built request so header order / User-Agent stay byte-accurate. |
| **Proxy** | SOCKS4 / 4a / 5 and HTTP-CONNECT for HTTP trackers. |
| **Magnet & batch** | Open **magnet links** (infohash-only) and load a whole folder of `.torrent`s into tabs at once. |
| **Auto-stop targets** | Stop on time, uploaded, downloaded, **ratio**, or seeders/leechers. |
| **Dry-run** | *File → Test announce* sends a single announce and shows whether the tracker accepted it — before you commit. |

## 🎨 Experience

| | |
|---|---|
| **Guided setup (newbie mode)** | A step-by-step wizard that probes each torrent against the tracker — accepted? enough leechers? — and loops until it finds one that will actually earn ratio, then sets believable defaults and starts. See [Getting started](getting-started.md#guided-setup). |
| **Light & dark themes** | Follows your OS on first launch (dark title bar via DWM); toggle in Settings. Owner-drawn flat cards + pill buttons. |
| **English / French** | Full in-app localization, switchable at runtime. |
| **Live graph** | *File → Live graph* — a dashboard tracing cumulative upload + ratio for the active tab. |
| **Portable settings** | Everything lives in `settings.json` next to the exe. **No registry**, fully portable (USB-friendly). |

<p align="center">
  <img src="screenshots/main-dark.png" width="420" alt="Seedforger main window in the dark theme">
  <br><sub><em>The dark theme — flat cards, pill buttons, a terminal-style log.</em></sub>
</p>

## Emulated clients (built-in)

qBittorrent · Transmission · Deluge · libtorrent · µTorrent · BitTorrent · BitComet · Vuze · Azureus · BitLord · ABC · BTuga · BitTornado · Burst · BitTyrant · BitSpirit · KTorrent · Gnome BT — several versions each, 50 profiles in all.
