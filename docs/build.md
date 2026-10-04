# Build from source

[← back to the README](../README.md)

Requires the **.NET 8 SDK** (`dotnet --version` ≥ 8).

```bash
# build everything
dotnet build Seedforger.sln -c Release

# run the tests (xUnit)
dotnet test tests/Seedforger.Tests/Seedforger.Tests.csproj

# lite single-file exe — tiny & fast (needs the .NET 8 Desktop runtime installed)
dotnet publish src/Seedforger/Seedforger.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true

# self-contained single-file exe — bundles the runtime, needs nothing installed
dotnet publish src/Seedforger/Seedforger.csproj -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true
```

> **Size:** any `--self-contained` publish is trimmed and compressed automatically
> (see [`src/Publish.props`](../src/Publish.props)) — the Windows GUI comes out at
> ~24 MB instead of ~68 MB, the CLI at ~12 MB instead of ~68 MB, the Avalonia GUI at
> ~22 MB instead of ~93 MB. Most of what goes is **WPF**: the SDK ships the whole
> WindowsDesktop runtime pack with a WinForms app even though nothing loads it.
>
> Trimming needs three guardrails, all set in `Publish.props`: WinForms itself is
> kept whole (it isn't trim-annotated — trimming it throws `TypeLoadException` in
> the modal message loop), `JsonSerializerIsReflectionEnabledByDefault` is turned
> back on (settings/clients/campaigns round-trip by reflection), and
> `BuiltInComInteropSupport` too (without it `RichTextBox` throws
> `InvalidCastException` when its handle is created).

Or build both Windows executables at once with **`scripts/build-release.cmd`** (Windows). It closes any running instance first — the self-contained single-file build fails (`MSB4018`) if the target `Seedforger.exe` is locked by a running process — and writes `publish\lite\Seedforger.exe` and `publish\fat\Seedforger.exe`.

> **Startup tip:** don't reach for `-p:PublishReadyToRun=true` on the self-contained build — it more than doubles the file size (≈170 MB), and the extra bytes your antivirus has to rescan on every launch cost *more* startup time than R2R saves. Measured here, the compressed self-contained build (≈12 s to window) beats the R2R one (≈22 s), and the lite build (≈6 s) beats both. **Size, not JIT, dominates startup.**

## Project layout

A standard `src/` + `tests/` layout. One portable **Core** holds the engine, the protocol, the client database and the command line; the three executables are thin front-ends over it. The **Integrity** projects are the reproducible science annex. Shared build settings (the single version number, analyzers, code style) live in `Directory.Build.props`, `.editorconfig` and `global.json` at the root.

```
src/
  Seedforger.Core/              net8.0 — the engine, shared by every front-end (Windows/Linux/macOS)
  │  ├─ SeedEngine.cs · SeedOptions.cs     one run of one torrent: announces, shaped counters, stop rules
  │  ├─ StealthOptions.cs · UpstreamBudget.cs   believability profile + shared uplink (instances, not globals)
  │  ├─ AnnounceProbe.cs              the dry-run announce (accepted / rejected / swarm)
  │  ├─ Announce.cs · TrackerResponse.cs       announce URL + info_hash + bencoded answer
  │  ├─ TrackerTransport.cs · SecureDns.cs     one HTTP/HTTPS path (proxy-aware) + DNS-over-HTTPS
  │  ├─ Net/                          ProxyConnector (direct, HTTP CONNECT, SOCKS4/4a/5) + ProxySettings
  │  ├─ TorrentClientFactory.cs · DefaultClientProfiles.cs   the client fingerprints
  │  ├─ SpeedShaper.cs · Stealth.cs · SwarmModel.cs          the shaping maths
  │  ├─ Settings.cs · RunPreferences.cs · UI/UiStrings.cs    portable JSON settings, Advanced values, EN/FR
  │  ├─ Cli/                          CliApp (the command line), CommandLine, DaemonHost
  │  ├─ Peer/                         the real peer-wire engine (stages A–D + governor)
  │  ├─ Campaign/                     campaign model, planner and orchestrator
  │  ├─ BitTorrent/                   bencode + .torrent parsing, magnets
  │  └─ Web/                          the daemon's dashboard + JSON status
  Seedforger.Cli/               net8.0 — the console executable (one line: it calls CliApp)
  Seedforger.App/               net8.0 — the cross-platform Avalonia GUI (Views/, ViewModels/)
  Seedforger/                   net8.0-windows — the Windows GUI (UI/MainForm, GuideForm, CampaignForm…);
                                        started with CLI flags it runs CliApp too
  Seedforger.Integrity/         net8.0 — the reproducible science model (the "No Free Ratio" annex)
  Seedforger.Integrity.Figures/ net8.0 — regenerates the paper figures from the model
tests/
  Seedforger.Tests/             net8.0 — the xUnit suite, run on Windows AND Linux in CI
scripts/                        build-release.cmd (Windows two-in-one publish)
docs/  ·  packaging/  ·  tools/  (mock tracker for the E2E test)
```

Every front-end builds its run from a `SeedOptions` and hands it to a `SeedEngine`; the engine reads no settings and no globals, which is what keeps the Windows window, the cross-platform window, the CLI, the daemon and campaigns byte-for-byte identical on the wire. The Core, CLI and Avalonia GUI build and run on Windows, Linux and macOS; the WinForms app is Windows-only.

## Tests

The xUnit suite covers the client fingerprints (incl. Transmission checksum), bencode round-trips, the speed shaper, stealth/swarm/budget math, the peer-wire protocol and a **loopback integration test** (one node downloads a hash-valid piece from another), the campaign planner, JSON settings round-trips (including files written by older versions), the **tracker transport and proxy connector against in-process fakes** (a fake HTTP tracker, a SOCKS5 proxy with credentials, HTTP CONNECT, SOCKS4a — no outbound network), the **dry-run probe** (accepted, rejected with a reason, empty swarm, unreachable), stop rules, the command-line reader, the **announce core** (a byte-exact announce URL, the `info_hash` percent-encoding, and parsing a tracker's answer straight from a raw HTTP response), and the **science model** in `Seedforger.Integrity` (the figures are pinned to the code with tolerances, so the papers can't drift).

## Contributing

PRs welcome — especially **new / updated client fingerprints** (`src/Seedforger.Core/DefaultClientProfiles.cs`) and tracker-compatibility fixes. Keep fingerprints accurate: a wrong `peer_id` gets *users* banned.
