# Build from source

[← back to the README](../README.md)

Requires the **.NET 8 SDK** (`dotnet --version` ≥ 8).

```bash
# build
dotnet build Seedforger.sln -c Release

# run the tests (xUnit) — 108 of them
dotnet test Seedforger.Tests/Seedforger.Tests.csproj

# lite single-file exe — tiny & fast (needs the .NET 8 Desktop runtime installed)
dotnet publish Seedforger/Seedforger.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true

# self-contained single-file exe — bundles the runtime, needs nothing installed
dotnet publish Seedforger/Seedforger.csproj -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
```

> **Startup tip:** don't reach for `-p:PublishReadyToRun=true` on the self-contained build — it more than doubles the file size (≈170 MB), and the extra bytes your antivirus has to rescan on every launch cost *more* startup time than R2R saves. Measured here, the compressed self-contained build (≈12 s to window) beats the R2R one (≈22 s), and the lite build (≈6 s) beats both. **Size, not JIT, dominates startup.**

## Project layout

```
Seedforger/
├─ Program.cs                 entry point, single-instance, code-page provider
├─ TorrentClientFactory.cs    data-driven client lookup + clients.json merge
├─ DefaultClientProfiles.cs   the 47 built-in client profiles
├─ ClientProfile.cs           profile model (peer_id recipe, headers, query, …)
├─ SpeedShaper.cs             realistic ramp-up / speed variation
├─ Stealth.cs · SwarmModel.cs · Bandwidth.cs   believability + swarm + budget
├─ HttpsTransport.cs          TLS transport for https:// trackers
├─ Settings.cs                portable JSON settings store
├─ Theme.cs · Localization.cs · GraphForm.cs   themes, i18n, live graph
├─ GuideForm.cs               guided setup (newbie mode) wizard
├─ Peer/                      the real peer-wire engine (stages A–D + governor)
├─ Campaign/                  orchestrator + visual builder (CampaignForm)
├─ BitTorrent/                bencode + .torrent parsing
├─ BytesRoads/                SOCKS / HTTP-CONNECT proxy sockets
└─ RM.cs / MainForm.cs        the WinForms UI
Seedforger.Tests/             108 xUnit tests
docs/how-bittorrent-works.md  the from-the-wire deep-dive
```

## Tests

**108 xUnit tests** cover the client fingerprints (incl. Transmission checksum), bencode round-trips, the speed shaper, stealth/swarm/bandwidth math, the peer-wire protocol and a **loopback integration test** (one node downloads a hash-valid piece from another), the campaign planner, JSON settings/clients round-trips, and the HTTPS transport (a real TLS fetch, skipped gracefully offline).

## Contributing

PRs welcome — especially **new / updated client fingerprints** (`DefaultClientProfiles.cs`) and tracker-compatibility fixes. Keep fingerprints accurate: a wrong `peer_id` gets *users* banned.
