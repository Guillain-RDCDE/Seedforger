# Command line

Seedforger runs the same engine with no window, so it scripts cleanly — cron, CI, a headless server, or just automating a routine. The command line lives in the core and is shared: the dedicated CLI runs on **Linux, macOS and Windows** as a self-contained single file, and the Windows GUI executable switches to it when started with these flags.

```
# cross-platform CLI (Linux / macOS / Windows)
./Seedforger.Cli [mode] [options]

# on Windows, the GUI executable also accepts the same flags
Seedforger.exe [mode] [options]
```

## Modes

| Flag | Effect |
|---|---|
| *(none)* | Launch the graphical interface (Windows exe) / show the help (console exe). |
| `--cli`, `--headless`, `--nogui` | Run without a window (automation). Needs a torrent. |
| `--test-announce`, `--dry-run` | One `started` announce as a complete seeder (and a `stopped` right after): prints accepted / rejected with the tracker's reason, and the swarm. |
| `--daemon`, `--folder <dir>` | Run one torrent or a whole folder 24/7 behind the [web dashboard](daemon.md). |
| `--list-clients` | List every client/version and every connection profile, exit. |
| `--help`, `-h` | Show the built-in help, exit. |

Unknown options are an error (exit code 2), and an option is never taken as another option's value — `-t --quiet` reports a missing torrent.

## Torrent (required)

| Flag | Meaning |
|---|---|
| `--torrent`, `-t <file>` | Path to a `.torrent` file. |
| `--folder <dir>` | Every `.torrent` in a folder (implies `--daemon`). Magnet links need the GUI, which asks for the size they lack. |

## Impersonate

| Flag | Meaning |
|---|---|
| `--client <name>` | Which client to report — e.g. `qBittorrent`, `Transmission`, `µTorrent`. |
| `--client-version <ver>` | e.g. `5.2.4`. Defaults to the newest known version of that client. |
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
| `--serve-real <file>` | Serve genuine hash-valid pieces of a real, matching file — opens the announced port and answers monitoring peers with real data. |
| `--connection <profile>` | A believable line: sets upload/download and the shared upstream budget (`--list-clients` prints the names). |
| `--interval <sec>` | Base announce interval; the tracker's own interval and `min interval` always win. |
| `--port <n>` | Announced listening port (default: random, like a real client). |

## Stop by itself

| Flag | Meaning |
|---|---|
| `--stop-after <minutes>` | Stop after that long. |
| `--stop-uploaded <MB>` | Stop once that much upload was reported. |
| `--stop-ratio <ratio>` | Stop once uploaded ÷ downloaded reaches it. |
| `--duration <minutes>` | Hard limit for the whole run. `0` (or omitted) = until `Ctrl+C`. |

## Believability

| Flag | Meaning |
|---|---|
| `--realistic on\|off` | Ramp-up + smooth variation instead of a flat rate. Default **on**. |
| `--swarm-aware on\|off` | Scale reported speed to the swarm's real demand. Default **on**. |

## Proxy

| Flag | Meaning |
|---|---|
| `--proxy-type none\|http\|socks4\|socks4a\|socks5` | Proxy protocol. HTTPS trackers work through a proxy too (CONNECT tunnel, TLS inside). SOCKS4a, SOCKS5 and HTTP CONNECT let the proxy resolve the tracker's name. |
| `--proxy-host <h>` | Proxy host. |
| `--proxy-port <p>` | Proxy port. |
| `--proxy-user <u>` | Username (if required). |
| `--proxy-pass <p>` | Password (if required). |

## Output

| Flag | Meaning |
|---|---|
| `--quiet`, `-q` | Suppress the per-announce log (DNS notes included). |

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Success (seeded, dry-run accepted, or help/list printed). |
| `1` | Tracker rejection or no usable answer (dry run), or the dashboard could not start. |
| `2` | Bad usage (unknown option, no torrent, file not found). |

## Examples

```bash
# Check a torrent against the tracker before committing to it
Seedforger.exe --test-announce -t movie.torrent --client qBittorrent

# Seed headless at ~800 kB/s for two hours, then stop cleanly
Seedforger.exe --cli -t movie.torrent -u 800 --duration 120

# Impersonate a specific version through a SOCKS5 proxy
Seedforger.exe --cli -t movie.torrent --client Transmission --client-version 4.0.6 \
  --proxy-type socks5 --proxy-host 127.0.0.1 --proxy-port 9050

# Use a connection profile and a random current client, stop once 5 GB were reported
Seedforger.exe --cli -t movie.torrent --connection "VDSL2  (10 / 50 Mbps)" --randomize-client --stop-uploaded 5120

# A whole folder, 24/7, behind the dashboard on the LAN
./Seedforger.Cli --folder ~/torrents --connection "Fibre  100 / 100 Mbps" --web-bind 0.0.0.0 --web-port 8080
```

> **Reminder.** This is an educational / security-research tool. Faking your ratio breaks the rules of virtually every private tracker and can get you banned. Automating it does not make it safer — use it only where you are permitted to.
