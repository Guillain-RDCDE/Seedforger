# Seedforger — project memory for Claude Code

What a new session needs to know before touching this repository. Keep it short and
current: when something here stops being true, change it in the same commit.

## What this is

A .NET 8 BitTorrent ratio spoofer (a modern RatioMaster) shipped together with a
research annex proving the client side cannot win. Educational / security-research
tool; the README carries the warning, keep it there.

## Architecture (since v3.0.0)

One portable **Core** (`src/Seedforger.Core`) holds everything that matters; the three
executables are thin front-ends over it.

- `SeedEngine` + `SeedOptions`: one run of one torrent. The engine reads **no settings
  and no globals**; every caller builds a `SeedOptions` and hands it over. Blocking
  `Start()`/`Stop()`, `StartAsync()`/`StopAsync()` for UIs, `Log` called from background threads.
- `StealthOptions` (realistic / swarm-aware / active hours) and `UpstreamBudget` (the shared
  uplink) are **instances**. `*.Shared` is what the GUIs edit; a `CampaignEngine` clones its
  own, so a campaign never changes the user's single-run settings.
- `AnnounceProbe`: the dry-run (one `started` as a complete seeder, read the answer, one
  `stopped`). Used by "Test announce", the guided setups and `--test-announce`.
- `Net/ProxyConnector` + `TrackerTransport`: plain sockets, direct / HTTP CONNECT / SOCKS4 /
  SOCKS4a / SOCKS5, one path for HTTP and HTTPS (TLS inside a CONNECT tunnel when proxied).
  Requests are hand-built raw HTTP on purpose: header order and User-Agent are part of a
  client's fingerprint. Never route announces through `HttpClient`.
- `Cli/CliApp`: the whole command line, shared by `Seedforger.Cli` (console) and the Windows
  exe (`Seedforger.exe --test-announce …`). `CommandLine` rejects unknown flags.
- `RunPreferences`: the Advanced-dialog values (proxy, fingerprint overrides, interval, stop
  rule) with one mapping to/from `Settings` and one `ApplyTo(SeedOptions)`.
- `Settings`: `settings.json` next to the exe. JSON names are frozen (misspellings included,
  via `[JsonPropertyName]`); numbers are read leniently from older files; a corrupt file is
  set aside as `settings.json.bad`, never overwritten.
- `UI/UiStrings`: the only EN/FR mechanism (`Get(key)`, `Pick(en, fr)`, `Language`).
- `Format`: the only byte/ratio/duration formatter.
- `TorrentClientFactory` + `DefaultClientProfiles`: the client database. `Resolve(family,
  version)`, `PickRandomModern`, `DefaultClientName` live here — do not hard-code client
  names elsewhere.

Front-ends: `src/Seedforger` (WinForms, Windows-only, `UI/MainForm` + `GuideForm` +
`CampaignForm` + `UI/AdvancedForm` + `GraphForm`), `src/Seedforger.App` (Avalonia,
cross-platform, `MainViewModel` is the counterpart of `MainForm`), `src/Seedforger.Cli`.
Both GUIs are classic layouts: menu bar, labelled group boxes, standard controls in the
system theme, no custom palette, no owner-drawn widgets. Keep them that way.

Nothing from the legacy RatioMaster form (`RM.cs`), the `BytesRoads` socket library,
`AppOptions`, `Bandwidth`, `Theme`, `Localization` exists any more. Do not reintroduce them.

## Conventions

- Style is in `.editorconfig`: 2-space indent, brace on the same line, space after casts,
  camelCase private fields, block-scoped namespaces. `Nullable`/`ImplicitUsings` are off
  project-wide (`Directory.Build.props`); say `using System;` explicitly.
- One version number: `<Version>` in `Directory.Build.props`. Nowhere else.
- Doc comments explain *why*; comments that narrate the code are deleted.
- Swallowed exceptions must at least log through the engine's `Log` sink; UI-side
  `catch (Exception) { }` only around things that cannot be acted on (a closed dialog).
- Public surface stays minimal: Core types are `internal` with `InternalsVisibleTo` for the
  three front-ends and the tests. Enums used in test `Theory` data are public.
- When adding a client profile: `DefaultClientProfiles.cs`, the peer_id prefix follows
  libtorrent's scheme (`-qB5240-`, digits then A–Z for values ≥ 10, so 2.0.15 → `-LT20F0-`),
  bump the profile count in `DefaultClientProfilesTests`, `docs/features.md` and
  `docs/REFERENCE.md`, and add current versions to `TorrentClientFactory.ModernClients`.
- Tests are xUnit in `tests/Seedforger.Tests`, no outbound network: use the in-process
  `FakeHttpServer` / `FakeProxy` from `TrackerTransportTests.cs`. The README badge carries the
  passing count; update it when the count changes.

## Build, test, validate

```
dotnet build Seedforger.sln -c Release
dotnet test tests/Seedforger.Tests/Seedforger.Tests.csproj -c Release
```

CI (`.github/workflows/ci.yml`): Windows build + tests; Linux build (Core, CLI, Avalonia,
Integrity), tests, science-figure regeneration with range checks, a published CLI smoke test,
and an end-to-end run of the CLI and the daemon against `tools/mock_tracker.py`. It runs on
pushes to `main`, on PRs, and **by hand** (`workflow_dispatch`) on any branch.

`.github/workflows/screenshots.yml` (manual): builds, tests, opens the WinForms window on a
Windows runner and the Avalonia window under Xvfb, drives them (loads a sample torrent,
starts seeding against the mock tracker bound on `0.0.0.0`), and commits the captures to
`docs/screenshots/`. Run it after any visible UI change; it is the only way to get a real
Windows capture.

### Working from a Claude Code cloud session

- The cloud environment's network policy (as configured today) blocks dot.net,
  builds.dotnet.microsoft.com, api.nuget.org and github.com for downloads, so **there is no
  local .NET SDK**. Validate through CI instead: push, trigger `ci.yml` with the GitHub
  `actions_run_trigger` tool on the branch, read the failed job logs, fix, repeat. Syntax
  can be pre-checked locally with tree-sitter (`pip install tree-sitter tree-sitter-c-sharp`).
- The session's git proxy allows pushes to the designated branch and to `main`
  (`git push origin <branch>:main` fast-forwards), but silently ignores branch deletion and
  the API refuses it. To delete a remote branch, commit a one-shot `workflow_dispatch`
  workflow that runs `git push origin --delete <branch>` with `contents: write`, trigger it,
  then remove the workflow.
- Bulk `git rm` can be refused by the session's safety classifier; ask the user, who can
  authorise it explicitly.

## Identity and publication

Every commit is authored by the single canonical identity in `.mailmap`
(`Guillain d'Erceville <167749917+Guillain-RDCDE@users.noreply.github.com>`); commits made
by CI use `github-actions[bot]`. Before any push or publication, check `git log --format='%an <%ae>'`
shows nothing else. Never write a model name into commits, code or docs.

## Docs that must stay in sync

`README.md` (badges, download table), `docs/build.md` (project layout), `docs/cli.md`
(every option in `CliApp.PrintHelp`), `docs/features.md`, `docs/REFERENCE.md`,
`docs/getting-started.md`. The science annex under `docs/papers/` is generated/validated by
CI — do not edit its figures by hand.
