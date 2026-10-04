using System;
using System.Text;

namespace Seedforger {
  internal class RandomStringGenerator {
    private readonly char[] characterArray;
    private readonly Random randomNumbersGenerator;

    public RandomStringGenerator() {
      characterArray = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789".ToCharArray();
      randomNumbersGenerator = new Random();
    }

    public char GetRandomCharacter() {
      return characterArray[
        (int) ((characterArray.GetUpperBound(0) + 1) * randomNumbersGenerator.NextDouble())];
    }

    public string Generate(int stringLength) {
      return Generate(stringLength, false);
    }

    public string Generate(int stringLength, bool randomness) {
      var stringBuilder = new StringBuilder {Capacity = stringLength};
      for (var count = 0; count <= stringLength - 1; count++) {
        if (randomness) {
          stringBuilder.Append((char) randomNumbersGenerator.Next(255));
        }
        else {
          stringBuilder.Append(GetRandomCharacter());
        }
      }

      return stringBuilder.ToString();
    }

    public string Generate(int stringLength, char[] charArray) {
      var stringBuilder = new StringBuilder {Capacity = stringLength};
      for (var count = 0; count <= stringLength - 1; count++) {
        stringBuilder.Append(
          charArray[(int) ((charArray.GetUpperBound(0) + 1) * randomNumbersGenerator.NextDouble())]);
      }

      return stringBuilder.ToString();
    }

    /// <summary>Percent-encodes a raw byte string (the legacy escape rule, kept as
    /// a thin alias of <see cref="Announce.PercentEncode"/>).</summary>
    public string Generate(string inputString, bool upperCase) => Announce.PercentEncode(inputString, upperCase);
  }
}