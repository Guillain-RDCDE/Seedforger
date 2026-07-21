# Daemon & web dashboard

[← back to the README](../README.md)

Seedforger can run **headless, 24/7, behind a live web dashboard** — the natural way
to run it on a seedbox, a NAS or any always-on box. It's the same cross-platform
engine as the GUI, driven from the CLI, with a browser UI bolted on so you can watch
(and stop) it from your phone.

## Start it

```bash
# One torrent, dashboard on the default port 8080 (localhost only)
Seedforger.Cli -t movie.torrent --daemon -u 800

# A whole folder of torrents (implies --daemon), each its own believable seeder
Seedforger.Cli --folder ~/torrents -u 1500 --randomize-client

# Expose the dashboard on the LAN, custom port, real seeding from a files folder
Seedforger.Cli --folder ~/torrents --daemon --web-bind 0.0.0.0 --web-port 9090 \
  --serve-real ~/downloads
```

Then open **http://127.0.0.1:8080/** (or whatever you set). Leave it running; stop it
with Ctrl+C, a `--duration <minutes>`, or the dashboard's **Stop** button.

## What the dashboard shows

- **Totals** — cumulative uploaded / downloaded, overall ratio, how many torrents are active.
- **Per torrent** — name, impersonated client, uploaded, downloaded, ratio, live
  seeders/leechers from the tracker, announce interval, tracker count, and a
  `real seed` badge when a hash-verified file is being served over the wire.
- It refreshes every two seconds by polling a small JSON endpoint.

The page is a single self-contained file (dark theme, no external assets) served by
the built-in HTTP listener — nothing to install, works the same on Windows, Linux and
macOS.

## The JSON API

The dashboard is just a view over one endpoint you can script against:

```bash
curl -s http://127.0.0.1:8080/api/status | jq
```

```json
{
  "app": "Seedforger", "version": "2.18.0", "uptimeSeconds": 3600,
  "totals": { "uploaded": 5368709120, "downloaded": 0, "ratio": 0, "running": 3, "count": 3 },
  "torrents": [
    { "name": "…", "client": "qBittorrent 5.2.3", "uploaded": 1789569706,
      "downloaded": 0, "ratio": 0, "seeders": 42, "leechers": 12,
      "interval": 1800, "running": true, "trackers": 1, "realSeed": false }
  ]
}
```

`POST /api/stop` halts the daemon and all seeding (this is what the Stop button calls).

## Flags

| Flag | Meaning |
|---|---|
| `--daemon` | Run 24/7 behind the dashboard (instead of the plain foreground run). |
| `--folder <dir>` | Seed every `.torrent` in a folder — implies `--daemon`. |
| `--web-port <n>` | Dashboard port (default `8080`). |
| `--web-bind <addr>` | Bind address (default `127.0.0.1`; `0.0.0.0` exposes it on the LAN). |
| `--serve-real <path>` | A file (single torrent) or a folder matched by torrent name — serve genuine hash-verified pieces. |
| `--randomize-client` | Give each torrent a fresh modern client fingerprint. |
| `--duration <min>` | Auto-stop after N minutes (0 = until Ctrl+C / Stop). |

> Binding to a non-loopback address (`0.0.0.0`) may need admin rights or a `urlacl`
> on Windows. The dashboard has no authentication — only expose it on a network you
> trust, or keep it on localhost and reach it over an SSH tunnel.

All the usual believability flags (`--realistic`, `--swarm-aware`, `--client`,
proxy options…) apply — see [Command line](cli.md).
