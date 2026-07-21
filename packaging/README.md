# Packaging

Maintainer templates for shipping Seedforger through the common package managers.
They are **not** auto-submitted — each ecosystem wants a pull request against its own
repository — but they are kept version-accurate here so a release is a copy-paste.

The binaries they reference are produced by [`.github/workflows/release.yml`](../.github/workflows/release.yml),
attached to each GitHub Release with a `SHA256SUMS.txt`. Bump `version` and the
`sha256` fields from that file when cutting a release.

| Manager | File | Ships | Notes |
|---|---|---|---|
| **winget** (Windows) | [`winget/`](winget/) | Portable GUI exe | `InstallerType: portable` — no installer. Submit to `microsoft/winget-pkgs`. |
| **Homebrew** (macOS) | [`homebrew/seedforger.rb`](homebrew/seedforger.rb) | CLI (x64 + arm64) | A formula for the headless CLI. Submit to a tap or `homebrew-core`. |
| **AUR** (Arch Linux) | [`aur/PKGBUILD`](aur/PKGBUILD) | CLI | Self-contained single file; no .NET runtime dependency. |
| **Flatpak** (Linux GUI) | [`flatpak/`](flatpak/) | Avalonia GUI | Manifest skeleton for the cross-platform GUI. |

See [docs/packaging.md](../docs/packaging.md) for the full per-platform install story
and how Windows code signing fits in.
