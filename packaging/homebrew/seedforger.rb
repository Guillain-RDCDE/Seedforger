# Homebrew formula for the Seedforger headless CLI (macOS).
# Update `version` and both `sha256` values from the release's SHA256SUMS.txt.
#   brew install --formula ./packaging/homebrew/seedforger.rb
# or submit to a tap (e.g. Guillain-RDCDE/homebrew-tap).
class Seedforger < Formula
  desc "Report torrent stats to a tracker without transferring bytes (headless)"
  homepage "https://github.com/Guillain-RDCDE/Seedforger"
  version "2.19.0"
  license "GPL-3.0-only"

  on_arm do
    url "https://github.com/Guillain-RDCDE/Seedforger/releases/download/v2.19.0/Seedforger-cli-osx-arm64"
    sha256 "d0040b2f945a9dda846153e6c53146898821aaee58d203b076cd9db3b7931a5b"
  end
  on_intel do
    url "https://github.com/Guillain-RDCDE/Seedforger/releases/download/v2.19.0/Seedforger-cli-osx-x64"
    sha256 "681845d191a8b1ee9e00204b3d84db3c5ffb15ac635508afc8ad447428ccb36a"
  end

  def install
    src = Dir["Seedforger-cli-*"].first
    bin.install src => "seedforger"
  end

  test do
    assert_match "Seedforger", shell_output("#{bin}/seedforger --help")
  end
end
