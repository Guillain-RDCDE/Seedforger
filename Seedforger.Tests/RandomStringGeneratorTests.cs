using System.Linq;
using Seedforger;
using Xunit;

namespace Seedforger.Tests {
  public class RandomStringGeneratorTests {
    private const string Alphanumeric =
      "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    private readonly RandomStringGenerator generator = new RandomStringGenerator();

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(8)]
    [InlineData(20)]
    public void Generate_ReturnsRequestedLength(int length) {
      Assert.Equal(length, generator.Generate(length).Length);
    }

    [Fact]
    public void Generate_Default_UsesOnlyAlphanumericCharacters() {
      var s = generator.Generate(200);
      Assert.All(s, c => Assert.Contains(c, Alphanumeric));
    }

    [Fact]
    public void Generate_WithNumericCharset_ProducesOnlyDigits() {
      var s = generator.Generate(100, "0123456789".ToCharArray());
      Assert.Equal(100, s.Length);
      Assert.All(s, c => Assert.True(char.IsDigit(c)));
    }

    [Fact]
    public void Generate_WithHexCharset_ProducesOnlyHexCharacters() {
      const string hex = "0123456789ABCDEF";
      var s = generator.Generate(100, hex.ToCharArray());
      Assert.Equal(100, s.Length);
      Assert.All(s, c => Assert.Contains(c, hex));
    }

    [Fact]
    public void GetRandomCharacter_IsAlphanumeric() {
      for (var i = 0; i < 500; i++) {
        Assert.Contains(generator.GetRandomCharacter(), Alphanumeric);
      }
    }

    [Fact]
    public void Generate_UrlEncode_KeepsAsciiLettersAndDigitsButEncodesOthers() {
      // "a" stays, space (0x20) becomes "%20".
      Assert.Equal("a", generator.Generate("a", false));
      Assert.Equal("%20", generator.Generate(" ", false));
    }

    [Fact]
    public void Generate_UrlEncode_UpperCaseAffectsHexDigits() {
      // 0x7f (DEL) is non-alphanumeric -> "%7f" (lower) or "%7F" (upper).
      var del = ((char) 127).ToString();
      Assert.Equal("%7f", generator.Generate(del, false));
      Assert.Equal("%7F", generator.Generate(del, true));
    }
  }
}
