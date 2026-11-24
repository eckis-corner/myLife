using System.Text;
using MyLife.Server.Infrastructure;

namespace MyLife.Tests.Server;

public sealed class TotpTests
{
    private static readonly byte[] RfcSecret = Encoding.ASCII.GetBytes("12345678901234567890");

    // RFC 6238, Anhang B (SHA-1, 8 Ziffern)
    [Theory]
    [InlineData(59, "94287082")]
    [InlineData(1111111109, "07081804")]
    [InlineData(1111111111, "14050471")]
    [InlineData(1234567890, "89005924")]
    [InlineData(2000000000, "69279037")]
    [InlineData(20000000000, "65353130")]
    public void MatchesRfc6238Vectors(long unixTime, string expected)
    {
        Assert.Equal(expected, Totp.Compute(RfcSecret, unixTime / Totp.PeriodSeconds, digits: 8));
    }

    [Fact]
    public void AcceptsTheNeighbouringStepsOnly()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        var step = Totp.StepAt(now);

        Assert.Equal(step, Totp.Verify(RfcSecret, Totp.Compute(RfcSecret, step), now));
        Assert.Equal(step - 1, Totp.Verify(RfcSecret, Totp.Compute(RfcSecret, step - 1), now));
        Assert.Equal(step + 1, Totp.Verify(RfcSecret, Totp.Compute(RfcSecret, step + 1), now));
        Assert.Null(Totp.Verify(RfcSecret, Totp.Compute(RfcSecret, step - 2), now));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("abcdef")]
    [InlineData("1234567")]
    public void RejectsMalformedCodes(string? code)
    {
        Assert.Null(Totp.Verify(RfcSecret, code, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void AcceptsCodeWithSpace()
    {
        var now = DateTimeOffset.UtcNow;
        var code = Totp.Compute(RfcSecret, Totp.StepAt(now));
        Assert.NotNull(Totp.Verify(RfcSecret, $"{code[..3]} {code[3..]}", now));
    }

    // RFC 4648, Abschnitt 10 (ohne Auffüllzeichen)
    [Theory]
    [InlineData("", "")]
    [InlineData("f", "MY")]
    [InlineData("fo", "MZXQ")]
    [InlineData("foo", "MZXW6")]
    [InlineData("foob", "MZXW6YQ")]
    [InlineData("fooba", "MZXW6YTB")]
    [InlineData("foobar", "MZXW6YTBOI")]
    public void Base32MatchesRfc4648(string plain, string encoded)
    {
        Assert.Equal(encoded, Base32.Encode(Encoding.ASCII.GetBytes(plain)));
        Assert.Equal(plain, Encoding.ASCII.GetString(Base32.Decode(encoded)));
    }
}
