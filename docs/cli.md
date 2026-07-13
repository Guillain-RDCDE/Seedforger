# Command line

Seedforger runs the same proven announce engine with no window, so it scripts cleanly — cron, CI, a headless server, or just automating a routine. Everything the GUI does to a single torrent, the command line does too.

> The executable is a Windows GUI app, so it attaches to the calling console when started from a terminal. Run it from `cmd`, PowerShell or a shell; output appears inline.

```
Seedforger.exe [mode] [options]
```

## Modes

| Flag | Effect |
|---|---|
| *(none)* | Launch the graphical interface. |
| `--cli`, `--headless`, `--nogui` | Run without a window (automation). Needs a torrent. |
| `--test-announce`, `--dry-run` | Announce once as a seeder, print the tracker's answer, exit. |
| `--list-clients` | List every client and version you can impersonate, exit. |
| `--help`, `-h` | Show the built-in help, exit. |

## Torrent (required for `--cli` and dry-run)

| Flag | Meaning |
|---|---|
| `--torrent`, `-t <file>` | Path to a `.torrent` file. |
| `--magnet <uri>` | A magnet link instead. |

## Impersonate

| Flag | Meaning |
|---|---|
| `--client <name>` | Which client to report — e.g. `qBittorrent`, `Transmission`, `µTorrent`. |
| `--client-version <ver>` | e.g. `5.2.3`. Defaults to the newest known version of that client. |
| `--randomize-client` | Pick a random modern client fingerprint on start. |

Run `--list-clients` for the exact names and versions.

## Speed & mode

| Flag | Meaning |
|---|---|
| `--upload`, `-u <kB/s>` | Reported upload speed. |
| `--download`, `-d <kB/s>` | Reported download speed (leecher mode). |
| `--seed` | Seeder: Finished 100 %, download forced to 0. **Default.** |
| `--leech` | Leecher: Finished 0 %. |
| `--finished <0-100>` | Explicit finished percentage. |
| `--connection <profile>` | Apply a connection profile's up/down caps (see `--help` for the list). |
| `--interval <sec>` | Base announce interval (the tracker may override it). |

## Believability

| Flag | Meaning |
|---|---|
| `--realistic on\|off` | Ramp-up + smooth variation instead of a flat rate. Default **on**. |
| `--swarm-aware on\|off` | Scale reported speed to the swarm's real demand. Default **on**. |

## Proxy

| Flag | Meaning |
|---|---|
| `--proxy-type none\|http\|socks4\|socks4a\|socks5` | Proxy protocol. |
| `--proxy-host <h>` | Proxy host. |
| `--proxy-port <p>` | Proxy port. |
| `--proxy-user <u>` | Username (if required). |
| `--proxy-pass <p>` | Password (if required). |

## Run

| Flag | Meaning |
|---|---|
| `--duration <minutes>` | Stop automatically after N minutes. `0` (or omitted) = run until `Ctrl+C`. |
| `--quiet`, `-q` | Suppress the per-announce log. |

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Success (seeded, dry-run accepted, or help/list printed). |
| `1` | Runtime error, tracker rejection, or dry-run timeout. |
| `2` | Bad usage (no torrent, file not found). |

## Examples

```bash
# Check a torrent against the tracker before committing to it
Seedforger.exe --test-announce -t movie.torrent --client qBittorrent

# Seed headless at ~800 kB/s for two hours, then stop cleanly
Seedforger.exe --cli -t movie.torrent -u 800 --duration 120

# Impersonate a specific version through a SOCKS5 proxy
Seedforger.exe --cli -t movie.torrent --client Transmission --client-version 4.0.6 \
  --proxy-type socks5 --proxy-host 127.0.0.1 --proxy-port 9050

# Use a connection profile and a random client, run until Ctrl+C
Seedforger.exe --cli -t movie.torrent --connection "VDSL2 (100/40)" --randomize-client

# Seed from a magnet link, quietly
Seedforger.exe --cli --magnet "magnet:?xt=urn:btih:…" -u 500 --quiet
```

> **Reminder.** This is an educational / security-research tool. Faking your ratio breaks the rules of virtually every private tracker and can get you banned. Automating it does not make it safer — use it only where you are permitted to.
