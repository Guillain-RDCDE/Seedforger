# Homebrew formula for the Seedforger headless CLI (macOS).
# Update `version` and both `sha256` values from the release's SHA256SUMS.txt.
#   brew install --formula ./packaging/homebrew/seedforger.rb
# or submit to a tap (e.g. Guillain-RDCDE/homebrew-tap).
class Seedforger < Formula
  desc "Report torrent stats to a tracker without transferring bytes (headless)"
  homepage "https://github.com/Guillain-RDCDE/Seedforger"
  version "2.18.0"
  license "MIT"

  on_arm do
    url "https://github.com/Guillain-RDCDE/Seedforger/releases/download/v2.18.0/Seedforger-cli-osx-arm64"
    sha256 "0000000000000000000000000000000000000000000000000000000000000000"
  end
  on_intel do
    url "https://github.com/Guillain-RDCDE/Seedforger/releases/download/v2.18.0/Seedforger-cli-osx-x64"
    sha256 "0000000000000000000000000000000000000000000000000000000000000000"
  end

  def install
    src = Dir["Seedforger-cli-*"].first
    bin.install src => "seedforger"
  end

  test do
    assert_match "Seedforger", shell_output("#{bin}/seedforger --help")
  end
end
