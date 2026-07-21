# Install & packaging

[← back to the README](../README.md)

Every release attaches self-contained binaries for Windows, Linux and macOS, plus a
`SHA256SUMS.txt`, built by [`.github/workflows/release.yml`](../.github/workflows/release.yml).
You can always just download and run — no installer, no runtime (except the tiny
`lite` Windows GUI build, which uses the free .NET 8 Desktop Runtime).

## Release artifacts

| File | Platform | What |
|---|---|---|
| `Seedforger-lite-win-x64.exe` | Windows | GUI, ~0.5 MB, needs the .NET 8 Desktop Runtime |
| `Seedforger-win-x64.exe` | Windows | GUI, self-contained (~68 MB) |
| `Seedforger-gui-win-x64.exe` | Windows | Cross-platform Avalonia GUI |
| `Seedforger-cli-win-x64.exe` | Windows | Headless CLI / daemon |
| `Seedforger-cli-linux-x64`, `-linux-arm64` | Linux | Headless CLI / daemon |
| `Seedforger-gui-linux-x64` | Linux | Avalonia GUI |
| `Seedforger-cli-osx-x64`, `-osx-arm64` | macOS | Headless CLI / daemon |
| `Seedforger-gui-osx-x64`, `-osx-arm64` | macOS | Avalonia GUI |

Verify a download against the checksums:

```bash
sha256sum -c SHA256SUMS.txt --ignore-missing
```

## Package managers

Maintainer manifests live in [`packaging/`](../packaging/) and are kept
version-accurate. Once published to each ecosystem:

```powershell
# Windows
winget install Guillain-RDCDE.Seedforger
```
```bash
# macOS (Homebrew tap)
brew install guillain-rdcde/tap/seedforger

# Arch Linux (AUR)
yay -S seedforger-cli

# Linux GUI (Flatpak)
flatpak install io.github.guillain_rdcde.Seedforger
```

See [`packaging/README.md`](../packaging/README.md) for how each manifest maps to a
release artifact.

## Windows code signing (SmartScreen)

Unsigned executables trigger a SmartScreen "unknown publisher" warning — a real
friction point for the newcomers the Guided mode targets. The release workflow signs
every Windows `.exe` with **Authenticode** automatically **when a certificate is
configured**, and ships unsigned otherwise (so forks still build).

To enable it, add two repository secrets:

| Secret | Value |
|---|---|
| `WINDOWS_CERT_BASE64` | Your code-signing `.pfx`, base64-encoded (`base64 -w0 cert.pfx`). |
| `WINDOWS_CERT_PASSWORD` | The `.pfx` password. |

The workflow decodes the cert to a temp file, signs each `.exe` with `signtool`
(SHA-256, RFC-3161 timestamp via DigiCert), and deletes the cert. No secret ⇒ the
signing step is skipped, not failed.

> macOS notarization (`codesign` + `notarytool`) needs an Apple Developer ID and is
> **not** automated here — run it locally on the macOS artifacts if you distribute
> outside Homebrew. Linux binaries are unsigned by convention (checksums cover integrity).
