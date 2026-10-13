# Configuration

[← back to the README](../README.md)

Two optional JSON files, both editable without recompiling, plus the `settings.json` the windows keep for you. Where each one lives is under [Where your files live](#where-your-files-live).

---

## Custom / updated fingerprints without rebuilding

On first launch the window drops a **`clients.sample.json`** (next to the exe for a portable copy or a writable folder, in your user folder otherwise). Copy it to **`clients.json`** in the same folder, edit, done — entries are merged by name (yours override the built-ins), so you can add tomorrow's qBittorrent the day it ships:

```json
[
  {
    "family": "qBittorrent",
    "version": "5.2.4",
    "httpProtocol": "HTTP/1.1",
    "hashUpperCase": false,
    "key": { "type": "hex", "length": 8, "urlEncode": false, "upperCase": true },
    "peerIdPrefix": "-qB5240-",
    "peerIdRandom": { "type": "random", "length": 12, "urlEncode": true, "upperCase": false },
    "headers": "Host: {host}\r\nUser-Agent: qBittorrent/5.2.4\r\nAccept-Encoding: gzip\r\nConnection: close\r\n",
    "query": "info_hash={infohash}&peer_id={peerid}&port={port}&uploaded={uploaded}&downloaded={downloaded}&left={left}&corrupt=0&key={key}{event}&numwant={numwant}&compact=1&no_peer_id=1&supportcrypto=1&redundant=0",
    "defNumWant": 200,
    "parse": true,
    "searchString": "&peer_id=-qB5240-",
    "processName": "qbittorrent",
    "startOffset": 0,
    "maxOffset": 200000000
  }
]
```

Keep fingerprints accurate: a wrong `peer_id` gets *users* banned.

---

## Campaigns (goal-seeking orchestrator)

*Tools → Campaigns…* opens the [visual builder](screenshots/campaign-builder.png) — pick a goal (ratio / GB by a deadline), a connection profile, active hours, a torrent folder, and hit **Start** (or Save / Load). No JSON to hand-write.

Under the hood it's a `campaign.json` (Save/Load in the dialog; a `campaign.sample.json` is dropped beside `clients.sample.json`):

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

---

## Where your files live

Settings themselves (language, believability toggles, connection profile, active hours, the last client, speeds and proxy…) live in a **`settings.json`** — no registry. The GUIs read and write it; the CLI and the daemon take everything from their command line and do not use it.

| Install | `settings.json` |
|---|---|
| **Portable** — a `settings.json` already sits next to the exe and that folder is writable | next to the exe, as before |
| Windows (anything else: winget, Program Files…) | `%APPDATA%\Seedforger\settings.json` |
| Linux (AUR, a copy under `/usr`…) | `$XDG_CONFIG_HOME/seedforger/settings.json`, by default `~/.config/seedforger/settings.json` |
| Linux, Flatpak | `~/.var/app/io.github.guillain_rdcde.Seedforger/config/seedforger/settings.json` (the sandbox's own config folder) |
| macOS (Homebrew, an app bundle…) | `~/Library/Application Support/Seedforger/settings.json` |

- **To keep a copy portable** (a USB stick, a folder of tools), put a `settings.json` next to the exe — an empty file will do. Copies that already have one carry on unchanged.
- **Read-only install folder:** a `settings.json` found next to the exe is read once as the starting point and saved to the per-user folder; the original is left untouched.
- **A failed save is never silent:** the window's log says where the write failed and why. A file that cannot be parsed is set aside as `settings.json.bad`, and the log says so.

**`clients.json`** and the two samples (`clients.sample.json`, `campaign.sample.json`) follow the same portable rule; the CLI and the daemon read `clients.json` too.

- **Portable copy:** the copy is self-contained. `clients.json` is read only from next to the exe and the samples are written there; the per-user folder is never looked at.
- **Installed copy:** `clients.json` is read from the per-user folder first, then from next to the exe. The samples are written next to the exe when that folder is writable, in the per-user folder otherwise (inside the Flatpak: `~/.var/app/io.github.guillain_rdcde.Seedforger/config/seedforger/`).
