<div align="center">

# 🌱 Seedforger

**Tell any BitTorrent tracker whatever upload/download stats you want — without moving a single byte.**

A modern, from-the-ground-up .NET 8 revival of the classic *RatioMaster*.

![Seedforger](.github/social-preview.png)

[![CI](https://github.com/Guillain-RDCDE/Seedforger/actions/workflows/ci.yml/badge.svg)](https://github.com/Guillain-RDCDE/Seedforger/actions/workflows/ci.yml)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

<img src="preview.png" width="540" alt="Seedforger interface (light theme)">

</div>

---

Seedforger connects to a torrent tracker and **announces fake progress** (uploaded / downloaded / left) on your behalf. It impersonates a **real BitTorrent client** — matching `peer_id` and `User-Agent` fingerprints — so the announce looks exactly like the real thing. No files are ever transferred; only the numbers the tracker sees are made up.

It works **independently of your torrent client** — you don't even need one installed.

> [!WARNING]
> **This is an educational / security-research tool.** Faking your ratio breaks the rules of virtually every private tracker and **will get you banned** if you're caught. Only use it where you are allowed to. You alone are responsible for what you do with it.

---

## 🌱 For everyone — the 60-second guide

**In plain words:** you give Seedforger a `.torrent` file and tell it *"pretend I'm uploading at 10 MB/s"*. It quietly tells the tracker that story, over and over, so your stats go up. That's the whole idea.

### Get it running
Grab a build from the [latest release](../../releases/latest) — pick the one that suits you:

| Download | Size | Starts up | Needs |
|---|---|---|---|
| ⭐ **`Seedforger-lite.exe`** *(recommended)* | ~0.5 MB | **fastest** | the free [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0/runtime) (one-time install) |
| **`Seedforger.exe`** | ~68 MB | slower to launch | **nothing at all** — fully self-contained |

Either way it's a **single file, no installer**, that you can drop anywhere (USB stick included). Double-click and go.

> **Why two?** The self-contained build carries the whole .NET runtime inside it, so it's big and your antivirus rescans all 68 MB on every launch — that's what makes it feel slow. The lite build is a tiny 0.5 MB and starts roughly twice as fast; it just asks you to install the .NET runtime once. If you're not sure whether you have .NET, download lite first — Windows will tell you (and hand you the installer) if it's missing.

### Use it
1. **Browse…** and pick your `.torrent` file.
2. Set the **Upload Speed** (in kB/s) — how fast you want to "seed".
3. Choose a **Client** to impersonate — **qBittorrent 5.2.3** is a great, modern default.
4. Hit the green **START** button. Watch the **Ratio** and the log update.
5. Hit the red **STOP** when you're done.

### A few common-sense tips
- **Keep it believable.** Announcing that you seeded 900 GB in ten minutes is a great way to get flagged. Realistic speeds win.
- One-click realistic speeds: **Settings → Connection profile** (ADSL, VDSL, fibre, cable, 4G, 5G…) fills the upload/download for you, so you look like a normal home line.
- Leave **Realistic speed (ramp-up)** on (Settings menu) — it makes your fake speed climb and wobble like a real client instead of a robotic flat line.
- Use a **current** client (qBittorrent, Transmission 4, Deluge 2). Old clients stand out.

---

## 🔧 For power users & developers

### 📖 How does any of this work?
Read the deep-dive: **[How BitTorrent actually works](docs/how-bittorrent-works.md)** — bencode, the `.torrent` file, the infohash, peer IDs, the tracker & peer-wire protocols, magnets, encryption, how ratio is measured, and exactly where Seedforger plugs in.

### What's under the hood
| Area | What Seedforger does |
|---|---|
| **Client database** | Data-driven profiles (**47 clients**), not a hard-coded switch. Add/override any client via an external `clients.json` — **no recompile**. |
| **Modern fingerprints** | qBittorrent `-qB`, Transmission `-TR`, Deluge `-DE`, libtorrent `-LT`, plus all the legacy clients. Fingerprints verified against libtorrent's `generate_fingerprint` and each client's source. |
| **HTTPS trackers** | Full TLS support via `SslStream`, sending a raw hand-built HTTP request so header order / User-Agent stay byte-accurate. |
| **Realistic announces** | `SpeedShaper` applies a ramp-up + mean-reverting random walk instead of flat noise, so reported speeds look human. |
| **Portable settings** | Everything is stored in `settings.json` next to the exe. **No registry**, fully portable. |
| **Proxy** | SOCKS4 / 4a / 5 and HTTP-CONNECT for HTTP trackers. |
| **Stealth** | Announce-interval jitter, a day/night speed rhythm, an **active-hours** window, believability warnings, and **client rotation** each start. |
| **Swarm-aware speeds** | Upload/download scaled by the tracker's **real leecher/seeder counts** — 0 leechers ⇒ a trickle (nobody to feed), your share diluted by competing seeders. Makes the numbers physically plausible. |
| **Global upstream budget** | Picking a connection profile caps your **total** upload across *all* tabs — one uplink, shared fairly. |
| **Connectable seeder** | Answers peer handshakes on your port with a **full bitfield then a choke** — a connectable, complete seeder to any monitoring peer, that transfers nothing. |
| **Real-seed engine** (advanced) | *File → Serve a real file*: a TCP **peer wire engine** that serves **genuine, SHA-1-verified** blocks from the downloaded file, with a governor that caps the announced upload to what was actually served — defeats monitoring peers that request-and-verify. See the [deep-dive §13½](docs/how-bittorrent-works.md#13-the-deep-end-actually-participating-in-the-swarm). |
| **Campaign orchestrator** | *File → New campaign…* — a **visual builder** (no JSON): a **goal** (ratio / GB by a deadline) + a believability profile + a torrent folder, and it drives the rest — **staggered** starts, upstream **budget split by real demand**, **pacing** so you don't finish suspiciously early, then auto-stops. Because "start 10 torrents at once, full speed" is itself a tell. |
| **Peer_id fidelity** | Reproduces client-specific quirks, incl. **Transmission's peer_id checksum**, so ids validate. |
| **Connection profiles** | One-click believable speeds for ADSL / VDSL / cable / fibre 100-300-1G / 4G / 5G. |
| **Auto-stop targets** | Stop on time, uploaded, downloaded, **ratio**, or seeders/leechers. |
| **Magnet & batch** | Open **magnet links** (infohash-only), and load a whole folder of `.torrent`s into tabs. |
| **Dry-run** | *File → Test announce* sends a single announce and shows whether the tracker accepted it. |
| **Light & dark themes** | Follows your OS on first launch; toggle in Settings. |

### Emulated clients (built-in)
qBittorrent · Transmission · Deluge · libtorrent · µTorrent · BitTorrent · BitComet · Vuze · Azureus · BitLord · ABC · BTuga · BitTornado · Burst · BitTyrant · BitSpirit · KTorrent · Gnome BT — several versions each.

### Custom / updated fingerprints without rebuilding
On first launch Seedforger drops a **`clients.sample.json`** next to the exe. Copy it to **`clients.json`**, edit, done — entries are merged by name (yours override the built-ins):

```json
[
  {
    "family": "qBittorrent",
    "version": "5.2.3",
    "httpProtocol": "HTTP/1.1",
    "hashUpperCase": false,
    "key": { "type": "hex", "length": 8, "urlEncode": false, "upperCase": true },
    "peerIdPrefix": "-qB5230-",
    "peerIdRandom": { "type": "random", "length": 12, "urlEncode": true, "upperCase": false },
    "headers": "Host: {host}\r\nUser-Agent: qBittorrent/5.2.3\r\nAccept-Encoding: gzip\r\nConnection: close\r\n",
    "query": "info_hash={infohash}&peer_id={peerid}&port={port}&uploaded={uploaded}&downloaded={downloaded}&left={left}&corrupt=0&key={key}{event}&numwant={numwant}&compact=1&no_peer_id=1&supportcrypto=1&redundant=0",
    "defNumWant": 200,
    "parse": true,
    "searchString": "&peer_id=-qB5230-",
    "processName": "qbittorrent",
    "startOffset": 0,
    "maxOffset": 200000000
  }
]
```

### Campaigns (goal-seeking orchestrator)
*File → **New campaign…*** opens a visual builder — pick a goal (ratio / GB by a deadline), a connection profile, active hours, a torrent folder, and hit **Start** (or Save/Load). No JSON to hand-write.

Under the hood it's a `campaign.json` (Save/Load in the dialog; a `campaign.sample.json` is also dropped next to the exe):

```json
{
  "Goal": "upload",                 // or "ratio"
  "UploadGoalGB": 200,
  "TargetRatio": 2.0,
  "DeadlineHours": 336,             // spread over ~2 weeks (0 = as fast as credible)
  "Connection": "Fibre  300 / 300 Mbps",
  "UseActiveHours": true, "ActiveHoursStart": 8, "ActiveHoursEnd": 24,
  "RotateClient": true,
  "TorrentFolder": "C:\\torrents",
  "RealFileFolder": "",            // optional: matching files to seed for real
  "StaggerMinMinutes": 3, "StaggerMaxMinutes": 40, "MaxConcurrent": 6
}
```

The orchestrator staggers the starts, splits the connection's upstream toward the torrents that actually have leechers, paces the total toward the deadline, and stops at the goal — because launching everything at once, flat out, is exactly what a bot looks like.

### Build from source
Requires the **.NET 8 SDK** (`dotnet --version` ≥ 8).

```bash
# build
dotnet build Seedforger.sln -c Release

# run the tests (xUnit)
dotnet test Seedforger.Tests/Seedforger.Tests.csproj

# lite single-file exe — tiny & fast (needs the .NET 8 Desktop runtime installed)
dotnet publish Seedforger/Seedforger.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true

# self-contained single-file exe — bundles the runtime, needs nothing installed
dotnet publish Seedforger/Seedforger.csproj -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
```

> **Startup tip:** don't reach for `-p:PublishReadyToRun=true` on the self-contained build — it more than doubles the file size (≈170 MB), and the extra bytes your antivirus has to rescan on every launch cost *more* startup time than R2R saves. The compressed self-contained build is the faster of the two; the lite build is faster still. Size, not JIT, dominates startup here.

### Project layout
```
Seedforger/
├─ Program.cs                 entry point, single-instance, code-page provider
├─ TorrentClientFactory.cs    data-driven client lookup + clients.json merge
├─ DefaultClientProfiles.cs   the 47 built-in client profiles
├─ ClientProfile.cs           profile model (peer_id recipe, headers, query, …)
├─ SpeedShaper.cs             realistic ramp-up / speed variation
├─ HttpsTransport.cs          TLS transport for https:// trackers
├─ Settings.cs                portable JSON settings store
├─ Theme.cs                   flat modern WinForms restyle
├─ BitTorrent/                bencode + .torrent parsing
├─ BytesRoads/                SOCKS / HTTP-CONNECT proxy sockets
└─ RM.cs / MainForm.cs        the WinForms UI
Seedforger.Tests/             52 xUnit tests
```

### Tests
52 xUnit tests cover the client fingerprints, bencode round-trips, the speed shaper, JSON settings/clients round-trips and the HTTPS transport (a real TLS fetch, skipped gracefully offline).

### Contributing
PRs welcome — especially **new / updated client fingerprints** (`DefaultClientProfiles.cs`) and tracker-compatibility fixes. Keep fingerprints accurate: a wrong `peer_id` gets *users* banned.

---

## Credits & lineage

Seedforger stands on a long open-source lineage:
**RatioMaster** → [NikolayIT/RatioMaster.NET](https://github.com/NikolayIT/RatioMaster.NET) (MIT) → [sergiye/RatioMaster](https://github.com/sergiye/RatioMaster) → **Seedforger** (this project: .NET 8 rewrite, modern client DB, HTTPS, realistic announces, portable settings, redesigned UI).

## License

[MIT](LICENSE). Provided **as-is**, with no warranty. Do the right thing.
