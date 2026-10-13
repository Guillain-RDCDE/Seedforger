# Homebrew formula for the Seedforger headless CLI (macOS).
# Update `version` and both `sha256` values from the release's SHA256SUMS.txt.
#   brew install --formula ./packaging/homebrew/seedforger.rb
# or submit to a tap (e.g. Guillain-RDCDE/homebrew-tap).
class Seedforger < Formula
  desc "Report torrent stats to a tracker without transferring bytes (headless)"
  homepage "https://github.com/Guillain-RDCDE/Seedforger"
  version "3.0.2"
  license "GPL-3.0-only"

  on_arm do
    url "https://github.com/Guillain-RDCDE/Seedforger/releases/download/v3.0.2/Seedforger-cli-osx-arm64"
    sha256 "2a2cd36d7f47cc2d9c960824d07e191b827aedeb0df67504907a0487900fa67d"
  end
  on_intel do
    url "https://github.com/Guillain-RDCDE/Seedforger/releases/download/v3.0.2/Seedforger-cli-osx-x64"
    sha256 "af2faa2b2ddddc59b97ff1c48de4ae6b4af3327bb66680110095adbce6163e3a"
  end

  def install
    src = Dir["Seedforger-cli-*"].first
    bin.install src => "seedforger"
  end

  test do
    assert_match "Seedforger", shell_output("#{bin}/seedforger --help")
  end
end
