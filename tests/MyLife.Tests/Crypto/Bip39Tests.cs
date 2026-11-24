using System.Security.Cryptography;
using MyLife.Crypto;

namespace MyLife.Tests.Crypto;

public sealed class Bip39Tests
{
    // Offizielle Testvektoren (github.com/trezor/python-mnemonic, vectors.json), 256 Bit Entropie.
    [Theory]
    [InlineData("0000000000000000000000000000000000000000000000000000000000000000",
        "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon art")]
    [InlineData("7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f7f",
        "legal winner thank year wave sausage worth useful legal winner thank year wave sausage worth useful legal winner thank year wave sausage worth title")]
    [InlineData("ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff",
        "zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo zoo vote")]
    public void MatchesOfficialVectors(string entropy, string phrase)
    {
        Assert.Equal(phrase, string.Join(' ', Bip39.Encode(Convert.FromHexString(entropy))));
        Assert.True(Bip39.TryDecode(phrase, out var decoded));
        Assert.Equal(entropy, Convert.ToHexStringLower(decoded));
    }

    [Fact]
    public void WordListIsTheStandardList()
    {
        Assert.Equal(2048, Bip39.Words.Count);
        Assert.Equal("abandon", Bip39.Words[0]);
        Assert.Equal("zoo", Bip39.Words[^1]);
    }

    [Fact]
    public void RandomKeysSurviveTheRoundTrip()
    {
        for (var i = 0; i < 50; i++)
        {
            var entropy = RandomNumberGenerator.GetBytes(32);
            Assert.True(Bip39.TryDecode(string.Join(' ', Bip39.Encode(entropy)), out var decoded));
            Assert.Equal(entropy, decoded);
        }
    }

    [Fact]
    public void FourLettersCaseAndSeparatorsDoNotMatter()
    {
        var entropy = RandomNumberGenerator.GetBytes(32);
        var words = Bip39.Encode(entropy);
        var typed = string.Join(",\n ", words.Select(w => (w.Length > 4 ? w[..4] : w).ToUpperInvariant()));

        Assert.True(Bip39.TryDecode(typed, out var decoded));
        Assert.Equal(entropy, decoded);
    }

    [Fact]
    public void WrongChecksumOrCountIsRejected()
    {
        var words = Bip39.Encode(new byte[32]).ToArray();

        words[^1] = "zoo";
        Assert.False(Bip39.TryDecode(string.Join(' ', words), out _));
        Assert.False(Bip39.TryDecode(string.Join(' ', words[..23]), out _));
        Assert.False(Bip39.TryDecode(null, out _));
    }

    [Theory]
    [InlineData("abandon", 0)]
    [InlineData("aban", 0)]
    [InlineData("ABANDON", 0)]
    [InlineData("abandom", -1)]
    [InlineData("ab", -1)]
    [InlineData("xyzzy", -1)]
    public void IndexOfAcceptsPrefixesButNotTypos(string word, int expected)
    {
        Assert.Equal(expected, Bip39.IndexOf(word));
    }
}
