# Homebrew formula for the Seedforger headless CLI (macOS).
# Update `version` and both `sha256` values from the release's SHA256SUMS.txt.
#   brew install --formula ./packaging/homebrew/seedforger.rb
# or submit to a tap (e.g. Guillain-RDCDE/homebrew-tap).
class Seedforger < Formula
  desc "Report torrent stats to a tracker without transferring bytes (headless)"
  homepage "https://github.com/Guillain-RDCDE/Seedforger"
  version "3.0.0"
  license "GPL-3.0-only"

  on_arm do
    url "https://github.com/Guillain-RDCDE/Seedforger/releases/download/v3.0.0/Seedforger-cli-osx-arm64"
    sha256 "4b0e2e330fb3dce8575eb8daf653bb835c96014f684009a0cfcaf2d2f3b80c6f"
  end
  on_intel do
    url "https://github.com/Guillain-RDCDE/Seedforger/releases/download/v3.0.0/Seedforger-cli-osx-x64"
    sha256 "99ce5d16299edd8e53659e094f8c90dafd09205061761c25b141b83b2fe77b37"
  end

  def install
    src = Dir["Seedforger-cli-*"].first
    bin.install src => "seedforger"
  end

  test do
    assert_match "Seedforger", shell_output("#{bin}/seedforger --help")
  end
end
