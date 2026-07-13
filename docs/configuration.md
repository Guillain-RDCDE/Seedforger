# Configuration

[← back to the README](../README.md)

Two optional JSON files, both dropped next to the exe on first launch and both editable without recompiling.

---

## Custom / updated fingerprints without rebuilding

On first launch Seedforger drops a **`clients.sample.json`** next to the exe. Copy it to **`clients.json`**, edit, done — entries are merged by name (yours override the built-ins), so you can add tomorrow's qBittorrent the day it ships:

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

Keep fingerprints accurate: a wrong `peer_id` gets *users* banned.

---

## Campaigns (goal-seeking orchestrator)

The **Campaigns** button opens the [visual builder](screenshots/campaign-builder.png) — pick a goal (ratio / GB by a deadline), a connection profile, active hours, a torrent folder, and hit **Start** (or Save / Load). No JSON to hand-write.

Under the hood it's a `campaign.json` (Save/Load in the dialog; a `campaign.sample.json` is dropped next to the exe):

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

Settings themselves (theme, language, connection profile, active hours…) live in a portable **`settings.json`** next to the exe — no registry.
