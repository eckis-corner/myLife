using System.Security.Cryptography;
using System.Text;
using MyLife.Crypto;

namespace MyLife.Tests.Crypto;

/// <summary>Leitet den teuren Stammschlüssel (Argon2id, 64 MiB) einmal je Testklasse ab.</summary>
public sealed class ReferenceCryptoFixture : IAsyncLifetime
{
    public const string Password = "Testpasswort für myLife";

    public ReferenceMyLifeCrypto Crypto { get; } = new();

    public byte[] Salt { get; } = RandomNumberGenerator.GetBytes(16);

    public CryptoKeyHandle Master { get; private set; }

    public CryptoKeyHandle Kek { get; private set; }

    public async ValueTask InitializeAsync()
    {
        Master = await Crypto.DeriveMasterKeyAsync(Password, Salt, Argon2Parameters.Default);
        Kek = await Crypto.DeriveKeyEncryptionKeyAsync(Master);
    }

    public ValueTask DisposeAsync() => Crypto.DisposeAsync();
}

public sealed class ReferenceCryptoTests(ReferenceCryptoFixture fixture) : IClassFixture<ReferenceCryptoFixture>
{
    private readonly ReferenceMyLifeCrypto _crypto = fixture.Crypto;

    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

    private async Task<CryptoKeyHandle> NewKeyAsync() =>
        (await _crypto.CreateKeyAsync([new(fixture.Kek, "test")])).Handle;

    [Fact]
    public async Task Argon2idMatchesRfc9106TestVector()
    {
        // RFC 9106, Abschnitt 5.3
        var tag = await Argon2.HashAsync(
            password: Enumerable.Repeat((byte)0x01, 32).ToArray(),
            salt: Enumerable.Repeat((byte)0x02, 16).ToArray(),
            memoryKiB: 32, iterations: 3, parallelism: 4, length: 32,
            secret: Enumerable.Repeat((byte)0x03, 8).ToArray(),
            associatedData: Enumerable.Repeat((byte)0x04, 12).ToArray());

        Assert.Equal("0d640df58d78766c08c037a34a8b53c9d01ef0452d75b65eb52520e96b01e659", Convert.ToHexStringLower(tag));
    }

    [Fact]
    public async Task PasswordIsNormalizedToNfc()
    {
        var decomposed = ReferenceCryptoFixture.Password.Normalize(NormalizationForm.FormD);
        Assert.NotEqual(ReferenceCryptoFixture.Password, decomposed);

        var other = await _crypto.DeriveMasterKeyAsync(decomposed, fixture.Salt, Argon2Parameters.Default);

        Assert.Equal(await _crypto.DeriveAuthHashAsync(fixture.Master), await _crypto.DeriveAuthHashAsync(other));
    }

    [Fact]
    public async Task AuthHashAndKekAreIndependent()
    {
        var authHash = await _crypto.DeriveAuthHashAsync(fixture.Master);
        var created = await _crypto.CreateKeyAsync([new(fixture.Kek, "vmk")]);

        Assert.Equal(CryptoFormat.KeyLength, authHash.Length);
        Assert.DoesNotContain(Convert.ToHexString(authHash), Convert.ToHexString(created.WrappedKeys[0]), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1024, 3, 1)]
    [InlineData(65536, 1, 1)]
    [InlineData(65536, 3, 0)]
    [InlineData(2_000_000, 3, 1)]
    public async Task WeakOrExcessiveArgon2ParametersAreRejected(int memory, int iterations, int parallelism)
    {
        var error = await Assert.ThrowsAsync<CryptographicException>(() =>
            _crypto.DeriveMasterKeyAsync("pw", fixture.Salt, new Argon2Parameters(memory, iterations, parallelism)));
        Assert.Equal("mylife:invalid-argon2-parameters", error.Message);
    }

    [Fact]
    public async Task ShortSaltIsRejected()
    {
        var error = await Assert.ThrowsAsync<CryptographicException>(() =>
            _crypto.DeriveMasterKeyAsync("pw", new byte[8], Argon2Parameters.Default));
        Assert.Equal("mylife:invalid-salt", error.Message);
    }

    [Fact]
    public async Task EncryptedDataHasTheDocumentedLayout()
    {
        var key = await NewKeyAsync();
        var plaintext = Utf8("Bankkonto");

        var blob = await _crypto.EncryptAsync(key, plaintext, "object:bank_account:1");

        Assert.Equal(CryptoFormat.CurrentVersion, blob[0]);
        Assert.Equal((byte)CryptoKind.Data, blob[1]);
        Assert.Equal(2 + CryptoFormat.NonceLength + plaintext.Length + CryptoFormat.TagLength, blob.Length);
        Assert.Equal(plaintext, await _crypto.DecryptAsync(key, blob, "object:bank_account:1"));
    }

    [Fact]
    public async Task EncryptionUsesAFreshNonceEachTime()
    {
        var key = await NewKeyAsync();
        var first = await _crypto.EncryptAsync(key, Utf8("gleich"), "ctx");
        var second = await _crypto.EncryptAsync(key, Utf8("gleich"), "ctx");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task DecryptionFailsForWrongContextTamperingAndWrongKey()
    {
        var key = await NewKeyAsync();
        var otherKey = await NewKeyAsync();
        var blob = await _crypto.EncryptAsync(key, Utf8("geheim"), "object:a");
        var tampered = blob.ToArray();
        tampered[^1] ^= 1;

        await AssertFailsAsync("mylife:decrypt-failed", () => _crypto.DecryptAsync(key, blob, "object:b"));
        await AssertFailsAsync("mylife:decrypt-failed", () => _crypto.DecryptAsync(key, tampered, "object:a"));
        await AssertFailsAsync("mylife:decrypt-failed", () => _crypto.DecryptAsync(otherKey, blob, "object:a"));
    }

    [Fact]
    public async Task HeaderIsChecked()
    {
        var key = await NewKeyAsync();
        var blob = await _crypto.EncryptAsync(key, Utf8("x"), "ctx");
        var wrongVersion = blob.ToArray();
        wrongVersion[0] = 2;
        var wrongKind = blob.ToArray();
        wrongKind[1] = (byte)CryptoKind.WrappedKey;

        await AssertFailsAsync("mylife:unsupported-version", () => _crypto.DecryptAsync(key, wrongVersion, "ctx"));
        await AssertFailsAsync("mylife:wrong-kind", () => _crypto.DecryptAsync(key, wrongKind, "ctx"));
        await AssertFailsAsync("mylife:format", () => _crypto.DecryptAsync(key, blob[..20], "ctx"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task EmptyContextIsRejected(string? context)
    {
        var key = await NewKeyAsync();
        await AssertFailsAsync("mylife:invalid-context", () => _crypto.EncryptAsync(key, Utf8("x"), context!));
    }

    [Fact]
    public async Task OverlongContextIsRejected()
    {
        var key = await NewKeyAsync();
        await AssertFailsAsync("mylife:invalid-context", () => _crypto.EncryptAsync(key, Utf8("x"), new string('ä', 129)));
    }

    [Fact]
    public async Task KeyTypesAreEnforced()
    {
        await AssertFailsAsync("mylife:wrong-key-type", () => _crypto.EncryptAsync(fixture.Kek, Utf8("x"), "ctx"));
        await AssertFailsAsync("mylife:wrong-key-type", () => _crypto.EncryptAsync(fixture.Master, Utf8("x"), "ctx"));
        await AssertFailsAsync("mylife:wrong-key-type", () => _crypto.DeriveAuthHashAsync(fixture.Kek));
        await AssertFailsAsync("mylife:unknown-key", () => _crypto.EncryptAsync(new CryptoKeyHandle("fehlt"), Utf8("x"), "ctx"));
    }

    [Fact]
    public async Task VmkCanBeOpenedWithPasswordAndWithRecoveryKey()
    {
        var recovery = await _crypto.ImportRecoveryKeyAsync(RandomNumberGenerator.GetBytes(32));
        var vmk = await _crypto.CreateKeyAsync([new(fixture.Kek, "vmk"), new(recovery, "vmk")]);
        var blob = await _crypto.EncryptAsync(vmk.Handle, Utf8("Inhalt"), "ctx");

        var byPassword = await _crypto.UnwrapKeyAsync(fixture.Kek, vmk.WrappedKeys[0], "vmk");
        var byRecovery = await _crypto.UnwrapKeyAsync(recovery, vmk.WrappedKeys[1], "vmk");

        Assert.Equal(Utf8("Inhalt"), await _crypto.DecryptAsync(byPassword, blob, "ctx"));
        Assert.Equal(Utf8("Inhalt"), await _crypto.DecryptAsync(byRecovery, blob, "ctx"));
        await AssertFailsAsync("mylife:decrypt-failed", () => _crypto.UnwrapKeyAsync(recovery, vmk.WrappedKeys[0], "vmk"));
    }

    [Fact]
    public async Task RewrapKeepsTheKeyAndBindsTheNewContext()
    {
        var newKek = await _crypto.ImportRecoveryKeyAsync(RandomNumberGenerator.GetBytes(32));
        var vmk = await _crypto.CreateKeyAsync([new(fixture.Kek, "vmk")]);
        var blob = await _crypto.EncryptAsync(vmk.Handle, Utf8("Inhalt"), "ctx");

        var rewrapped = await _crypto.RewrapKeyAsync(fixture.Kek, vmk.WrappedKeys[0], "vmk", newKek, "vmk:2");
        var reopened = await _crypto.UnwrapKeyAsync(newKek, rewrapped, "vmk:2");

        Assert.Equal(Utf8("Inhalt"), await _crypto.DecryptAsync(reopened, blob, "ctx"));
        await AssertFailsAsync("mylife:decrypt-failed", () => _crypto.UnwrapKeyAsync(newKek, rewrapped, "vmk"));
    }

    [Fact]
    public async Task ChunksAreBoundToPositionAndEnd()
    {
        var file = await NewKeyAsync();
        var first = await _crypto.EncryptChunkAsync(file, 0, false, Utf8("Teil 1"), "document:1");
        var last = await _crypto.EncryptChunkAsync(file, 1, true, Utf8("Teil 2"), "document:1");

        Assert.Equal(Utf8("Teil 1"), await _crypto.DecryptChunkAsync(file, 0, false, first, "document:1"));
        Assert.Equal(Utf8("Teil 2"), await _crypto.DecryptChunkAsync(file, 1, true, last, "document:1"));
        await AssertFailsAsync("mylife:decrypt-failed", () => _crypto.DecryptChunkAsync(file, 1, false, first, "document:1"));
        await AssertFailsAsync("mylife:decrypt-failed", () => _crypto.DecryptChunkAsync(file, 0, true, first, "document:1"));
        await AssertFailsAsync("mylife:decrypt-failed", () => _crypto.DecryptChunkAsync(file, 0, false, first, "document:2"));
    }

    [Fact]
    public async Task ChunkLargerThanOneMebibyteIsRejected()
    {
        var file = await NewKeyAsync();
        await AssertFailsAsync("mylife:chunk-too-large", () =>
            _crypto.EncryptChunkAsync(file, 0, true, new byte[CryptoFormat.MaxChunkBytes + 1], "document:1"));
    }

    [Fact]
    public async Task EnvelopeCanOnlyBeOpenedByTheRecipient()
    {
        var vmk = await _crypto.CreateKeyAsync([new(fixture.Kek, "vmk")]);
        var area = await _crypto.CreateKeyAsync([new(vmk.Handle, "area:1")]);
        var blob = await _crypto.EncryptAsync(area.Handle, Utf8("Versicherung"), "object:insurance:1");
        var trustee = await _crypto.CreateKeyPairAsync(KeyPairKind.Ecdh, vmk.Handle, "private-key:ecdh");
        var stranger = await _crypto.CreateKeyPairAsync(KeyPairKind.Ecdh, vmk.Handle, "private-key:ecdh");

        var envelope = await _crypto.SealKeyAsync(vmk.Handle, area.WrappedKeys[0], "area:1", trustee.PublicKey, "grant:1:death");
        var opened = await _crypto.OpenSealedKeyAsync(trustee.Handle, envelope, "grant:1:death");

        Assert.Equal(CryptoFormat.EnvelopeLength, envelope.Length);
        Assert.Equal(Utf8("Versicherung"), await _crypto.DecryptAsync(opened, blob, "object:insurance:1"));
        await AssertFailsAsync("mylife:decrypt-failed", () => _crypto.OpenSealedKeyAsync(stranger.Handle, envelope, "grant:1:death"));
        await AssertFailsAsync("mylife:decrypt-failed", () => _crypto.OpenSealedKeyAsync(trustee.Handle, envelope, "grant:1:incapacity"));
    }

    [Fact]
    public async Task InvalidPublicKeyIsRejected()
    {
        var vmk = await _crypto.CreateKeyAsync([new(fixture.Kek, "vmk")]);
        var notOnCurve = new byte[CryptoFormat.PublicKeyLength];
        notOnCurve[0] = 0x04;

        await AssertFailsAsync("mylife:invalid-public-key", () =>
            _crypto.SealKeyAsync(vmk.Handle, vmk.WrappedKeys[0], "vmk", notOnCurve, "grant:x"));
        await AssertFailsAsync("mylife:invalid-public-key", () =>
            _crypto.SealKeyAsync(vmk.Handle, vmk.WrappedKeys[0], "vmk", new byte[33], "grant:x"));
    }

    [Fact]
    public async Task PrivateKeysSurviveWrappingAndAreBoundToTheirKind()
    {
        var vmk = await _crypto.CreateKeyAsync([new(fixture.Kek, "vmk")]);
        var signing = await _crypto.CreateKeyPairAsync(KeyPairKind.Ecdsa, vmk.Handle, "private-key:ecdsa");

        var unwrapped = await _crypto.UnwrapPrivateKeyAsync(KeyPairKind.Ecdsa, vmk.Handle, signing.WrappedPrivateKey, "private-key:ecdsa");
        var signature = await _crypto.SignAsync(unwrapped, Utf8("Fingerprint"), "endorsement");

        Assert.Equal(CryptoFormat.PublicKeyLength, signing.PublicKey.Length);
        Assert.Equal(CryptoFormat.SignatureLength, signature.Length);
        Assert.True(await _crypto.VerifyAsync(signing.PublicKey, Utf8("Fingerprint"), signature, "endorsement"));
        Assert.False(await _crypto.VerifyAsync(signing.PublicKey, Utf8("Fingerprint"), signature, "anderer-zweck"));
        Assert.False(await _crypto.VerifyAsync(signing.PublicKey, Utf8("Fingerprint"), signature[..10], "endorsement"));
        await AssertFailsAsync("mylife:wrong-kind", () =>
            _crypto.UnwrapPrivateKeyAsync(KeyPairKind.Ecdh, vmk.Handle, signing.WrappedPrivateKey, "private-key:ecdsa"));
        await AssertFailsAsync("mylife:wrong-key-type", () => _crypto.OpenSealedKeyAsync(unwrapped, new byte[CryptoFormat.EnvelopeLength], "x"));
    }

    [Fact]
    public async Task ReleaseRemovesKeys()
    {
        await using var crypto = new ReferenceMyLifeCrypto();
        var recovery = await crypto.ImportRecoveryKeyAsync(RandomNumberGenerator.GetBytes(32));
        var key = await crypto.CreateKeyAsync([new(recovery, "vmk")]);

        await crypto.ReleaseAsync(key.Handle);
        await AssertFailsAsync("mylife:unknown-key", () => crypto.EncryptAsync(key.Handle, Utf8("x"), "ctx"));
        Assert.Equal(1, crypto.KeyCount);

        await crypto.ReleaseAllAsync();
        Assert.Equal(0, crypto.KeyCount);
    }

    [Fact]
    public async Task HmacRejectsShortKeys()
    {
        await AssertFailsAsync("mylife:invalid-key", () => _crypto.HmacSha256Async(new byte[8], Utf8("x")));
    }

    [Fact]
    public void HandleDoesNotRevealItsId()
    {
        Assert.Equal("CryptoKeyHandle", new CryptoKeyHandle("geheim").ToString());
    }

    private static async Task AssertFailsAsync(string code, Func<Task> action)
    {
        var error = await Assert.ThrowsAsync<CryptographicException>(action);
        Assert.Equal(code, error.Message);
    }
}
