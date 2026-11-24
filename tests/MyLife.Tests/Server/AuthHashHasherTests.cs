using System.Security.Cryptography;
using MyLife.Server.Infrastructure;

namespace MyLife.Tests.Server;

public sealed class AuthHashHasherTests
{
    [Fact]
    public async Task VerifiesOnlyTheOriginalAuthHash()
    {
        var authHash = RandomNumberGenerator.GetBytes(32);
        var other = RandomNumberGenerator.GetBytes(32);

        var phc = await AuthHashHasher.HashAsync(authHash);

        Assert.StartsWith("$argon2id$v=19$m=19456,t=2,p=1$", phc, StringComparison.Ordinal);
        Assert.True(await AuthHashHasher.VerifyAsync(phc, authHash));
        Assert.False(await AuthHashHasher.VerifyAsync(phc, other));
    }

    [Fact]
    public async Task UsesAFreshSaltEachTime()
    {
        var authHash = RandomNumberGenerator.GetBytes(32);

        Assert.NotEqual(await AuthHashHasher.HashAsync(authHash), await AuthHashHasher.HashAsync(authHash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("$argon2i$v=19$m=19456,t=2,p=1$c2FsdA$aGFzaA")]
    [InlineData("kein-phc")]
    public async Task RejectsForeignFormats(string phc)
    {
        Assert.False(await AuthHashHasher.VerifyAsync(phc, new byte[32]));
    }
}
