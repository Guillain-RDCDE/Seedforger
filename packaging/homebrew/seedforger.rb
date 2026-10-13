# Homebrew formula for the Seedforger headless CLI (macOS).
# Update `version` and both `sha256` values from the release's SHA256SUMS.txt.
#   brew install --formula ./packaging/homebrew/seedforger.rb
# or submit to a tap (e.g. Guillain-RDCDE/homebrew-tap).
class Seedforger < Formula
  desc "Report torrent stats to a tracker without transferring bytes (headless)"
  homepage "https://github.com/Guillain-RDCDE/Seedforger"
  version "3.0.1"
  license "GPL-3.0-only"

  on_arm do
    url "https://github.com/Guillain-RDCDE/Seedforger/releases/download/v3.0.1/Seedforger-cli-osx-arm64"
    sha256 "d1af50efa82ab2d20ce671b17d8994e00012a080df6983954f8e93b3fa1d7910"
  end
  on_intel do
    url "https://github.com/Guillain-RDCDE/Seedforger/releases/download/v3.0.1/Seedforger-cli-osx-x64"
    sha256 "963eb0c54ed9a009ee23368385675754baaeabfe8aecfc5933cdaac75511a5e7"
  end

  def install
    src = Dir["Seedforger-cli-*"].first
    bin.install src => "seedforger"
  end

  test do
    assert_match "Seedforger", shell_output("#{bin}/seedforger --help")
  end
end
