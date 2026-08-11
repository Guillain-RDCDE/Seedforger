# Build from source

[← back to the README](../README.md)

Requires the **.NET 8 SDK** (`dotnet --version` ≥ 8).

```bash
# build everything
dotnet build Seedforger.sln -c Release

# run the tests (xUnit) — 172 of them
dotnet test tests/Seedforger.Tests/Seedforger.Tests.csproj

# lite single-file exe — tiny & fast (needs the .NET 8 Desktop runtime installed)
dotnet publish src/Seedforger/Seedforger.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true

# self-contained single-file exe — bundles the runtime, needs nothing installed
dotnet publish src/Seedforger/Seedforger.csproj -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
```

Or build both Windows executables at once with **`scripts/build-release.cmd`** (Windows). It closes any running instance first — the self-contained single-file build fails (`MSB4018`) if the target `Seedforger.exe` is locked by a running process — and writes `publish\lite\Seedforger.exe` and `publish\fat\Seedforger.exe`.

> **Startup tip:** don't reach for `-p:PublishReadyToRun=true` on the self-contained build — it more than doubles the file size (≈170 MB), and the extra bytes your antivirus has to rescan on every launch cost *more* startup time than R2R saves. Measured here, the compressed self-contained build (≈12 s to window) beats the R2R one (≈22 s), and the lite build (≈6 s) beats both. **Size, not JIT, dominates startup.**

## Project layout

A standard `src/` + `tests/` layout. A portable **Core** (no WinForms) drives every front-end; the **Integrity** projects are the reproducible science annex.

```
src/
  Seedforger.Core/              net8.0 — the portable engine (Windows/Linux/macOS, no WinForms)
  │  ├─ SeedEngine.cs                 headless announce/seed loop (the portable engine)
  │  ├─ Announce.cs                   announce URL + info_hash + response parsing
  │  ├─ TrackerTransport.cs           HTTP (proxy) / HTTPS fetch, shared by GUI + CLI
  │  ├─ HttpsTransport.cs · SecureDns.cs      TLS transport + DNS-over-HTTPS
  │  ├─ TorrentClientFactory.cs · DefaultClientProfiles.cs   the 50 client fingerprints
  │  ├─ SpeedShaper.cs · Stealth.cs · SwarmModel.cs · Bandwidth.cs   believability
  │  ├─ Settings.cs · Language.cs     portable JSON settings + language enum
  │  ├─ Peer/                         the real peer-wire engine (stages A–D + governor)
  │  ├─ Campaign/                     campaign model + policy
  │  ├─ BitTorrent/                   bencode + .torrent parsing
  │  └─ BytesRoads/                   SOCKS / HTTP-CONNECT proxy sockets
  Seedforger.Cli/               net8.0 — the cross-platform headless command line
  Seedforger.App/               net8.0 — the cross-platform Avalonia GUI (Views/, ViewModels/)
  Seedforger/                   net8.0-windows — the WinForms GUI (UI/, wizards, Theme…)
  Seedforger.Integrity/         net8.0 — the reproducible science model (the "No Free Ratio" annex)
  Seedforger.Integrity.Figures/ net8.0 — regenerates the paper figures from the model
tests/
  Seedforger.Tests/             net8.0 — 172 xUnit tests, run on Windows AND Linux in CI
scripts/                        build-release.cmd (Windows two-in-one publish)
docs/  ·  packaging/  ·  tools/  (mock tracker for the E2E test)
```

The Core, CLI and Avalonia GUI build and run on Windows, Linux and macOS; the WinForms app is Windows-only.

## Tests

**172 xUnit tests** cover the client fingerprints (incl. Transmission checksum), bencode round-trips, the speed shaper, stealth/swarm/bandwidth math, the peer-wire protocol and a **loopback integration test** (one node downloads a hash-valid piece from another), the campaign planner, JSON settings/clients round-trips, the HTTPS transport (a real TLS fetch, skipped gracefully offline), the **announce core** (a byte-exact announce URL, the `info_hash` percent-encoding, and parsing a tracker's answer straight from a raw HTTP response — all WinForms-free, in `Announce.cs`), and the **science model** in `Seedforger.Integrity` (the figures are pinned to the code with tolerances, so the papers can't drift).

## Contributing

PRs welcome — especially **new / updated client fingerprints** (`src/Seedforger.Core/DefaultClientProfiles.cs`) and tracker-compatibility fixes. Keep fingerprints accurate: a wrong `peer_id` gets *users* banned.
