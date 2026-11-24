using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MyLife.Crypto;

namespace MyLife.Tests.Crypto;

/// <summary>
/// Testvektoren für das Format Version 1. Erzeugt über die öffentliche Schnittstelle von <see cref="IMyLifeCrypto"/>,
/// damit .NET und JavaScript denselben Weg gehen: Passwort → Stammschlüssel → KEK → VMK → Bereichsschlüssel → Daten.
/// Alle Byte-Werte sind Hex-Zeichenketten. Dasselbe Schema nutzt crypto-harness.mjs für seine eigenen Ergebnisse.
/// </summary>
public sealed record CryptoVectors
{
    public const string VmkContext = "vmk";
    public const string AreaContext = "area:3";
    public const string EcdhContext = "private-key:ecdh";
    public const string EcdsaContext = "private-key:ecdsa";
    public const string SignatureContext = "endorsement";

    public int Format { get; init; } = CryptoFormat.CurrentVersion;
    public required string Password { get; init; }
    /// <summary>Dasselbe Passwort in Unicode NFD; muss denselben Anmeldehash ergeben.</summary>
    public required string PasswordDecomposed { get; init; }
    public required string Salt { get; init; }
    public required Argon2Parameters Argon2 { get; init; }
    public required string AuthHash { get; init; }
    public required string RecoveryKey { get; init; }
    public required string WrappedVmkByKek { get; init; }
    public required string WrappedVmkByRecovery { get; init; }
    public required string WrappedAreaKey { get; init; }
    public required DataVector Data { get; init; }
    public required FileVector File { get; init; }
    public required KeyPairVector Ecdh { get; init; }
    public required KeyPairVector Ecdsa { get; init; }
    public required EnvelopeVector Envelope { get; init; }
    public required SignatureVector Signature { get; init; }
    public required HmacVector Hmac { get; init; }

    public sealed record DataVector(string Context, string Plaintext, string Blob);

    public sealed record FileVector(string KeyContext, string WrappedFileKey, string Context, IReadOnlyList<ChunkVector> Chunks);

    public sealed record ChunkVector(int Index, bool IsLast, string Plaintext, string Ciphertext);

    public sealed record KeyPairVector(string PublicKey, string WrappedPrivateKey);

    public sealed record EnvelopeVector(string Context, string Envelope);

    public sealed record SignatureVector(string Context, string Data, string Signature);

    public sealed record HmacVector(string Key, string Data, string Mac);

    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static byte[] Hex(string value) => Convert.FromHexString(value);

    public static string Hex(byte[] value) => Convert.ToHexStringLower(value);

    /// <summary>Erzeugt einen vollständigen Satz Vektoren mit frischen Zufallswerten.</summary>
    public static async Task<CryptoVectors> CreateAsync(IMyLifeCrypto crypto)
    {
        const string password = "Korrekt Pferd Batterie Heftklammer – Grüße aus Köln";
        var salt = await crypto.RandomBytesAsync(16);
        var recoveryKey = await crypto.RandomBytesAsync(32);
        var parameters = Argon2Parameters.Default;

        var master = await crypto.DeriveMasterKeyAsync(password, salt, parameters);
        var authHash = await crypto.DeriveAuthHashAsync(master);
        var kek = await crypto.DeriveKeyEncryptionKeyAsync(master);
        var recovery = await crypto.ImportRecoveryKeyAsync(recoveryKey);

        var vmk = await crypto.CreateKeyAsync([new(kek, VmkContext), new(recovery, VmkContext)]);
        var area = await crypto.CreateKeyAsync([new(vmk.Handle, AreaContext)]);

        var objectId = Guid.NewGuid();
        var dataContext = $"object:bank_account:{objectId}";
        var plaintext = Encoding.UTF8.GetBytes($$"""{"schemaVersion":1,"bank":"Sparkasse Köln","iban":"DE02 1203 0000 0000 2020 51","note":"{{objectId}}"}""");
        var blob = await crypto.EncryptAsync(area.Handle, plaintext, dataContext);

        var documentId = Guid.NewGuid();
        var fileKeyContext = $"file:{documentId}";
        var file = await crypto.CreateKeyAsync([new(area.Handle, fileKeyContext)]);
        var fileContext = $"document:{documentId}";
        var chunks = new List<ChunkVector>();
        int[] sizes = [1000, 1000, 17];
        for (var i = 0; i < sizes.Length; i++)
        {
            var chunk = RandomNumberGenerator.GetBytes(sizes[i]);
            var isLast = i == sizes.Length - 1;
            var ciphertext = await crypto.EncryptChunkAsync(file.Handle, i, isLast, chunk, fileContext);
            chunks.Add(new(i, isLast, Hex(chunk), Hex(ciphertext)));
        }

        var ecdh = await crypto.CreateKeyPairAsync(KeyPairKind.Ecdh, vmk.Handle, EcdhContext);
        var ecdsa = await crypto.CreateKeyPairAsync(KeyPairKind.Ecdsa, vmk.Handle, EcdsaContext);

        var envelopeContext = $"grant:{Guid.NewGuid()}:death";
        var envelope = await crypto.SealKeyAsync(vmk.Handle, area.WrappedKeys[0], AreaContext, ecdh.PublicKey, envelopeContext);

        var signature = await crypto.SignAsync(ecdsa.Handle, ecdh.PublicKey, SignatureContext);

        var hmacKey = await crypto.RandomBytesAsync(32);
        var mac = await crypto.HmacSha256Async(hmacKey, ecdh.PublicKey);

        await crypto.ReleaseAllAsync();

        return new CryptoVectors
        {
            Password = password,
            PasswordDecomposed = password.Normalize(NormalizationForm.FormD),
            Salt = Hex(salt),
            Argon2 = parameters,
            AuthHash = Hex(authHash),
            RecoveryKey = Hex(recoveryKey),
            WrappedVmkByKek = Hex(vmk.WrappedKeys[0]),
            WrappedVmkByRecovery = Hex(vmk.WrappedKeys[1]),
            WrappedAreaKey = Hex(area.WrappedKeys[0]),
            Data = new(dataContext, Hex(plaintext), Hex(blob)),
            File = new(fileKeyContext, Hex(file.WrappedKeys[0]), fileContext, chunks),
            Ecdh = new(Hex(ecdh.PublicKey), Hex(ecdh.WrappedPrivateKey)),
            Ecdsa = new(Hex(ecdsa.PublicKey), Hex(ecdsa.WrappedPrivateKey)),
            Envelope = new(envelopeContext, Hex(envelope)),
            Signature = new(SignatureContext, Hex(ecdh.PublicKey), Hex(signature)),
            Hmac = new(Hex(hmacKey), Hex(ecdh.PublicKey), Hex(mac)),
        };
    }

    /// <summary>
    /// Prüft einen Vektorsatz vollständig mit der angegebenen Implementierung: jeder Weg zum Klartext muss
    /// funktionieren. Wirft bei der ersten Abweichung.
    /// </summary>
    public async Task VerifyAsync(IMyLifeCrypto crypto)
    {
        Assert.Equal(CryptoFormat.CurrentVersion, Format);

        var master = await crypto.DeriveMasterKeyAsync(Password, Hex(Salt), Argon2);
        Assert.Equal(AuthHash, Hex(await crypto.DeriveAuthHashAsync(master)));
        var kek = await crypto.DeriveKeyEncryptionKeyAsync(master);
        var recovery = await crypto.ImportRecoveryKeyAsync(Hex(RecoveryKey));

        foreach (var vmk in new[]
                 {
                     await crypto.UnwrapKeyAsync(kek, Hex(WrappedVmkByKek), VmkContext),
                     await crypto.UnwrapKeyAsync(recovery, Hex(WrappedVmkByRecovery), VmkContext),
                 })
        {
            var area = await crypto.UnwrapKeyAsync(vmk, Hex(WrappedAreaKey), AreaContext);
            Assert.Equal(Data.Plaintext, Hex(await crypto.DecryptAsync(area, Hex(Data.Blob), Data.Context)));

            var file = await crypto.UnwrapKeyAsync(area, Hex(File.WrappedFileKey), File.KeyContext);
            foreach (var chunk in File.Chunks)
            {
                var plaintext = await crypto.DecryptChunkAsync(file, chunk.Index, chunk.IsLast, Hex(chunk.Ciphertext), File.Context);
                Assert.Equal(chunk.Plaintext, Hex(plaintext));
            }

            var ecdh = await crypto.UnwrapPrivateKeyAsync(KeyPairKind.Ecdh, vmk, Hex(Ecdh.WrappedPrivateKey), EcdhContext);
            var opened = await crypto.OpenSealedKeyAsync(ecdh, Hex(Envelope.Envelope), Envelope.Context);
            Assert.Equal(Data.Plaintext, Hex(await crypto.DecryptAsync(opened, Hex(Data.Blob), Data.Context)));

            var ecdsa = await crypto.UnwrapPrivateKeyAsync(KeyPairKind.Ecdsa, vmk, Hex(Ecdsa.WrappedPrivateKey), EcdsaContext);
            var ownSignature = await crypto.SignAsync(ecdsa, Hex(Signature.Data), Signature.Context);
            Assert.True(await crypto.VerifyAsync(Hex(Ecdsa.PublicKey), Hex(Signature.Data), ownSignature, Signature.Context));
        }

        Assert.True(await crypto.VerifyAsync(Hex(Ecdsa.PublicKey), Hex(Signature.Data), Hex(Signature.Signature), Signature.Context));
        Assert.Equal(Hmac.Mac, Hex(await crypto.HmacSha256Async(Hex(Hmac.Key), Hex(Hmac.Data))));

        await crypto.ReleaseAllAsync();
    }
}
