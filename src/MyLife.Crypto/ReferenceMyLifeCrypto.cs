using System.Collections.Concurrent;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace MyLife.Crypto;

/// <summary>
/// .NET-Referenzimplementierung der Formate aus myLife.crypto.js. Dient den Kreuztests und dem Werkzeug
/// mylife-decrypt. Läuft nicht im Browser (AES-GCM und ECDH fehlen dort); im Client gilt <see cref="JsMyLifeCrypto"/>.
/// </summary>
[UnsupportedOSPlatform("browser")]
public sealed class ReferenceMyLifeCrypto : IMyLifeCrypto
{
    private readonly ConcurrentDictionary<string, KeyEntry> _keys = new();

    public Task<byte[]> RandomBytesAsync(int length)
    {
        if (length is < 1 or > 1024)
        {
            throw new CryptographicException("mylife:invalid-length");
        }

        return Task.FromResult(RandomNumberGenerator.GetBytes(length));
    }

    public async Task<CryptoKeyHandle> DeriveMasterKeyAsync(string password, byte[] salt, Argon2Parameters parameters)
    {
        ArgumentNullException.ThrowIfNull(salt);
        ArgumentNullException.ThrowIfNull(parameters);
        if (string.IsNullOrEmpty(password))
        {
            throw new CryptographicException("mylife:invalid-password");
        }

        if (salt.Length is < CryptoFormat.MinSaltBytes or > CryptoFormat.MaxSaltBytes)
        {
            throw new CryptographicException("mylife:invalid-salt");
        }

        parameters.EnsureValid();
        var passwordBytes = Encoding.UTF8.GetBytes(password.Normalize(NormalizationForm.FormC));
        try
        {
            var raw = await Argon2.HashAsync(passwordBytes, salt, parameters.MemoryKiB, parameters.Iterations, parameters.Parallelism, CryptoFormat.KeyLength);
            return Store(KeyEntry.Secret(KeyType.Master, raw));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    public Task<byte[]> DeriveAuthHashAsync(CryptoKeyHandle masterKey) =>
        Task.FromResult(Hkdf(Lookup(masterKey, KeyType.Master).SecretBytes, CryptoFormat.InfoAuth));

    public Task<CryptoKeyHandle> DeriveKeyEncryptionKeyAsync(CryptoKeyHandle masterKey)
    {
        var kek = Hkdf(Lookup(masterKey, KeyType.Master).SecretBytes, CryptoFormat.InfoKek);
        return Task.FromResult(Store(KeyEntry.Secret(KeyType.Wrap, kek)));
    }

    public Task<CryptoKeyHandle> ImportRecoveryKeyAsync(byte[] recoveryKey)
    {
        if (recoveryKey is not { Length: CryptoFormat.KeyLength })
        {
            throw new CryptographicException("mylife:invalid-recovery-key");
        }

        var key = Hkdf(recoveryKey, CryptoFormat.InfoRecovery);
        return Task.FromResult(Store(KeyEntry.Secret(KeyType.Wrap, key)));
    }

    public Task<byte[]> DeriveRecoveryAuthHashAsync(byte[] recoveryKey)
    {
        if (recoveryKey is not { Length: CryptoFormat.KeyLength })
        {
            throw new CryptographicException("mylife:invalid-recovery-key");
        }

        return Task.FromResult(Hkdf(recoveryKey, CryptoFormat.InfoRecoveryAuth));
    }

    public Task<CreatedKey> CreateKeyAsync(IReadOnlyList<KeyWrapping> wrappings)
    {
        if (wrappings is not { Count: > 0 })
        {
            throw new CryptographicException("mylife:no-wrapping");
        }

        var wrappers = wrappings.Select(w => (Key: Lookup(w.Wrapper, KeyType.Wrap, KeyType.Aes).SecretBytes, w.Context)).ToList();
        var key = RandomNumberGenerator.GetBytes(CryptoFormat.KeyLength);
        var wrapped = wrappers.Select(w => SealAead(w.Key, CryptoKind.WrappedKey, key, w.Context)).ToList();
        return Task.FromResult(new CreatedKey(Store(KeyEntry.Secret(KeyType.Aes, key)), wrapped));
    }

    public Task<CryptoKeyHandle> UnwrapKeyAsync(CryptoKeyHandle wrapper, byte[] wrappedKey, string context)
    {
        var key = UnwrapSecret(Lookup(wrapper, KeyType.Wrap, KeyType.Aes).SecretBytes, wrappedKey, context);
        return Task.FromResult(Store(KeyEntry.Secret(KeyType.Aes, key)));
    }

    public Task<byte[]> RewrapKeyAsync(CryptoKeyHandle oldWrapper, byte[] wrappedKey, string oldContext, CryptoKeyHandle newWrapper, string newContext)
    {
        var source = Lookup(oldWrapper, KeyType.Wrap, KeyType.Aes).SecretBytes;
        var target = Lookup(newWrapper, KeyType.Wrap, KeyType.Aes).SecretBytes;
        CryptoFormat.ContextBytes(newContext);
        var key = UnwrapSecret(source, wrappedKey, oldContext);
        try
        {
            return Task.FromResult(SealAead(target, CryptoKind.WrappedKey, key, newContext));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public Task<byte[]> EncryptAsync(CryptoKeyHandle key, byte[] plaintext, string context)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        return Task.FromResult(SealAead(Lookup(key, KeyType.Aes).SecretBytes, CryptoKind.Data, plaintext, context));
    }

    public Task<byte[]> DecryptAsync(CryptoKeyHandle key, byte[] blob, string context) =>
        Task.FromResult(OpenAead(Lookup(key, KeyType.Aes).SecretBytes, CryptoKind.Data, blob, context));

    public Task<byte[]> EncryptChunkAsync(CryptoKeyHandle fileKey, int index, bool isLast, byte[] data, string context)
    {
        var key = Lookup(fileKey, KeyType.Aes).SecretBytes;
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length > CryptoFormat.MaxChunkBytes)
        {
            throw new CryptographicException("mylife:chunk-too-large");
        }

        var (nonce, aad) = ChunkParameters(index, isLast, context);
        return Task.FromResult(AesGcmSeal(key, nonce, data, aad));
    }

    public Task<byte[]> DecryptChunkAsync(CryptoKeyHandle fileKey, int index, bool isLast, byte[] data, string context)
    {
        var key = Lookup(fileKey, KeyType.Aes).SecretBytes;
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length is < CryptoFormat.TagLength or > CryptoFormat.MaxChunkBytes + CryptoFormat.TagLength)
        {
            throw new CryptographicException("mylife:format");
        }

        var (nonce, aad) = ChunkParameters(index, isLast, context);
        return Task.FromResult(AesGcmOpen(key, nonce, data, aad));
    }

    public Task<CreatedKeyPair> CreateKeyPairAsync(KeyPairKind kind, CryptoKeyHandle wrapper, string context)
    {
        var wrapKey = Lookup(wrapper, KeyType.Wrap, KeyType.Aes).SecretBytes;
        CryptoFormat.ContextBytes(context);
        var curve = ECCurve.NamedCurves.nistP256;
        AsymmetricAlgorithm algorithm = kind switch
        {
            KeyPairKind.Ecdh => ECDiffieHellman.Create(curve),
            KeyPairKind.Ecdsa => ECDsa.Create(curve),
            _ => throw new CryptographicException("mylife:invalid-key-pair-kind"),
        };

        var pkcs8 = algorithm.ExportPkcs8PrivateKey();
        try
        {
            var publicKey = EncodePublicKey(((ECAlgorithm)algorithm).ExportParameters(false).Q);
            var wrapped = SealAead(wrapKey, WrapKind(kind), pkcs8, context);
            var handle = Store(KeyEntry.Asymmetric(kind, algorithm));
            return Task.FromResult(new CreatedKeyPair(handle, publicKey, wrapped));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pkcs8);
        }
    }

    public Task<CryptoKeyHandle> UnwrapPrivateKeyAsync(KeyPairKind kind, CryptoKeyHandle wrapper, byte[] wrappedPrivateKey, string context)
    {
        var wrapKey = Lookup(wrapper, KeyType.Wrap, KeyType.Aes).SecretBytes;
        var pkcs8 = OpenAead(wrapKey, WrapKind(kind), wrappedPrivateKey, context);
        try
        {
            AsymmetricAlgorithm algorithm = kind == KeyPairKind.Ecdh ? ECDiffieHellman.Create() : ECDsa.Create();
            try
            {
                algorithm.ImportPkcs8PrivateKey(pkcs8, out _);
                if (((ECAlgorithm)algorithm).ExportParameters(false).Curve.Oid.Value != ECCurve.NamedCurves.nistP256.Oid.Value)
                {
                    throw new CryptographicException("mylife:invalid-private-key");
                }
            }
            catch
            {
                algorithm.Dispose();
                throw;
            }

            return Task.FromResult(Store(KeyEntry.Asymmetric(kind, algorithm)));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pkcs8);
        }
    }

    public Task<byte[]> SealKeyAsync(CryptoKeyHandle wrapper, byte[] wrappedKey, string wrappedContext, byte[] recipientPublicKey, string envelopeContext)
    {
        var wrapKey = Lookup(wrapper, KeyType.Wrap, KeyType.Aes).SecretBytes;
        var aad = CryptoFormat.Aad(CryptoKind.Envelope, envelopeContext);
        using var recipient = ImportPublicKey<ECDiffieHellman>(recipientPublicKey);
        var key = UnwrapSecret(wrapKey, wrappedKey, wrappedContext);
        try
        {
            using var ephemeral = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
            var ephemeralPublicKey = EncodePublicKey(ephemeral.ExportParameters(false).Q);
            var sharedSecret = ephemeral.DeriveRawSecretAgreement(recipient.PublicKey);
            var envelopeKey = EciesKey(sharedSecret, ephemeralPublicKey);
            var nonce = RandomNumberGenerator.GetBytes(CryptoFormat.NonceLength);
            var ciphertext = AesGcmSeal(envelopeKey, nonce, key, aad);
            CryptographicOperations.ZeroMemory(sharedSecret);
            CryptographicOperations.ZeroMemory(envelopeKey);
            return Task.FromResult<byte[]>([.. CryptoFormat.Header(CryptoKind.Envelope), .. ephemeralPublicKey, .. nonce, .. ciphertext]);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public Task<CryptoKeyHandle> OpenSealedKeyAsync(CryptoKeyHandle privateKey, byte[] envelope, string context)
    {
        var entry = Lookup(privateKey, KeyType.Ecdh);
        if (envelope is not { Length: CryptoFormat.EnvelopeLength })
        {
            throw new CryptographicException("mylife:format");
        }

        CheckHeader(envelope, CryptoKind.Envelope);
        var aad = CryptoFormat.Aad(CryptoKind.Envelope, context);
        var ephemeralPublicKey = envelope[2..(2 + CryptoFormat.PublicKeyLength)];
        var nonce = envelope[(2 + CryptoFormat.PublicKeyLength)..(2 + CryptoFormat.PublicKeyLength + CryptoFormat.NonceLength)];
        var ciphertext = envelope[(2 + CryptoFormat.PublicKeyLength + CryptoFormat.NonceLength)..];

        using var ephemeral = ImportPublicKey<ECDiffieHellman>(ephemeralPublicKey, "mylife:decrypt-failed");
        var sharedSecret = ((ECDiffieHellman)entry.Algorithm!).DeriveRawSecretAgreement(ephemeral.PublicKey);
        var envelopeKey = EciesKey(sharedSecret, ephemeralPublicKey);
        try
        {
            var key = AesGcmOpen(envelopeKey, nonce, ciphertext, aad);
            return Task.FromResult(Store(KeyEntry.Secret(KeyType.Aes, key)));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(sharedSecret);
            CryptographicOperations.ZeroMemory(envelopeKey);
        }
    }

    public Task<byte[]> SignAsync(CryptoKeyHandle signingKey, byte[] data, string context)
    {
        var entry = Lookup(signingKey, KeyType.Ecdsa);
        ArgumentNullException.ThrowIfNull(data);
        var message = CryptoFormat.SignedMessage(context, data);
        var signature = ((ECDsa)entry.Algorithm!).SignData(message, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return Task.FromResult(signature);
    }

    public Task<bool> VerifyAsync(byte[] publicKey, byte[] data, byte[] signature, string context)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(signature);
        var message = CryptoFormat.SignedMessage(context, data);
        using var key = ImportPublicKey<ECDsa>(publicKey);
        if (signature.Length != CryptoFormat.SignatureLength)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(key.VerifyData(message, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
    }

    public Task<byte[]> Sha256Async(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return Task.FromResult(SHA256.HashData(data));
    }

    public Task<byte[]> HmacSha256Async(byte[] key, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (key is not { Length: >= 16 })
        {
            throw new CryptographicException("mylife:invalid-key");
        }

        return Task.FromResult(HMACSHA256.HashData(key, data));
    }

    public Task ReleaseAsync(CryptoKeyHandle key)
    {
        if (key.Id is not null && _keys.TryRemove(key.Id, out var entry))
        {
            entry.Dispose();
        }

        return Task.CompletedTask;
    }

    public Task ReleaseAllAsync()
    {
        foreach (var id in _keys.Keys)
        {
            if (_keys.TryRemove(id, out var entry))
            {
                entry.Dispose();
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>Anzahl der Schlüssel im Register (für Tests).</summary>
    internal int KeyCount => _keys.Count;

    public async ValueTask DisposeAsync() => await ReleaseAllAsync();

    // ---------------------------------------------------------------- Formate

    internal static byte[] SealAead(byte[] key, CryptoKind kind, byte[] plaintext, string context)
    {
        var aad = CryptoFormat.Aad(kind, context);
        var nonce = RandomNumberGenerator.GetBytes(CryptoFormat.NonceLength);
        return [.. CryptoFormat.Header(kind), .. nonce, .. AesGcmSeal(key, nonce, plaintext, aad)];
    }

    internal static byte[] OpenAead(byte[] key, CryptoKind kind, byte[] blob, string context)
    {
        ArgumentNullException.ThrowIfNull(blob);
        if (blob.Length < 2 + CryptoFormat.NonceLength + CryptoFormat.TagLength)
        {
            throw new CryptographicException("mylife:format");
        }

        CheckHeader(blob, kind);
        var aad = CryptoFormat.Aad(kind, context);
        return AesGcmOpen(key, blob[2..(2 + CryptoFormat.NonceLength)], blob[(2 + CryptoFormat.NonceLength)..], aad);
    }

    private static void CheckHeader(byte[] data, CryptoKind kind)
    {
        if (data[0] != CryptoFormat.CurrentVersion)
        {
            throw new CryptographicException("mylife:unsupported-version");
        }

        if (data[1] != (byte)kind)
        {
            throw new CryptographicException("mylife:wrong-kind");
        }
    }

    private static byte[] UnwrapSecret(byte[] wrapKey, byte[] wrappedKey, string context)
    {
        var key = OpenAead(wrapKey, CryptoKind.WrappedKey, wrappedKey, context);
        if (key.Length != CryptoFormat.KeyLength)
        {
            CryptographicOperations.ZeroMemory(key);
            throw new CryptographicException("mylife:decrypt-failed");
        }

        return key;
    }

    private static (byte[] Nonce, byte[] Aad) ChunkParameters(int index, bool isLast, string context)
    {
        if (index < 0)
        {
            throw new CryptographicException("mylife:invalid-chunk-index");
        }

        return (CryptoFormat.ChunkNonce((uint)index), CryptoFormat.ChunkAad((uint)index, isLast, context));
    }

    private static byte[] AesGcmSeal(byte[] key, byte[] nonce, byte[] plaintext, byte[] aad)
    {
        using var aes = new AesGcm(key, CryptoFormat.TagLength);
        var output = new byte[plaintext.Length + CryptoFormat.TagLength];
        aes.Encrypt(nonce, plaintext, output.AsSpan(0, plaintext.Length), output.AsSpan(plaintext.Length), aad);
        return output;
    }

    private static byte[] AesGcmOpen(byte[] key, byte[] nonce, byte[] data, byte[] aad)
    {
        using var aes = new AesGcm(key, CryptoFormat.TagLength);
        var plaintext = new byte[data.Length - CryptoFormat.TagLength];
        try
        {
            aes.Decrypt(nonce, data.AsSpan(0, plaintext.Length), data.AsSpan(plaintext.Length), plaintext, aad);
        }
        catch (AuthenticationTagMismatchException exception)
        {
            throw new CryptographicException("mylife:decrypt-failed", exception);
        }

        return plaintext;
    }

    private static byte[] Hkdf(byte[] inputKey, string info) =>
        HKDF.DeriveKey(HashAlgorithmName.SHA256, inputKey, CryptoFormat.KeyLength, [], Encoding.UTF8.GetBytes(info));

    private static byte[] EciesKey(byte[] sharedSecret, byte[] ephemeralPublicKey) =>
        HKDF.DeriveKey(HashAlgorithmName.SHA256, sharedSecret, CryptoFormat.KeyLength, ephemeralPublicKey, Encoding.UTF8.GetBytes(CryptoFormat.InfoEcies));

    private static CryptoKind WrapKind(KeyPairKind kind) => kind switch
    {
        KeyPairKind.Ecdh => CryptoKind.WrappedEcdhKey,
        KeyPairKind.Ecdsa => CryptoKind.WrappedEcdsaKey,
        _ => throw new CryptographicException("mylife:invalid-key-pair-kind"),
    };

    internal static byte[] EncodePublicKey(ECPoint point) => [0x04, .. point.X!, .. point.Y!];

    private static T ImportPublicKey<T>(byte[] publicKey, string error = "mylife:invalid-public-key")
        where T : ECAlgorithm
    {
        if (publicKey is not { Length: CryptoFormat.PublicKeyLength } || publicKey[0] != 0x04)
        {
            throw new CryptographicException(error);
        }

        var parameters = new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = publicKey[1..33], Y = publicKey[33..65] },
        };

        ECAlgorithm algorithm = typeof(T) == typeof(ECDsa) ? ECDsa.Create() : ECDiffieHellman.Create();
        try
        {
            algorithm.ImportParameters(parameters);
            return (T)algorithm;
        }
        catch (CryptographicException exception)
        {
            algorithm.Dispose();
            throw new CryptographicException(error, exception);
        }
    }

    // ---------------------------------------------------------------- Register

    private CryptoKeyHandle Store(KeyEntry entry)
    {
        var id = Guid.NewGuid().ToString();
        _keys[id] = entry;
        return new CryptoKeyHandle(id);
    }

    private KeyEntry Lookup(CryptoKeyHandle handle, params KeyType[] types)
    {
        if (handle.Id is null || !_keys.TryGetValue(handle.Id, out var entry))
        {
            throw new CryptographicException("mylife:unknown-key");
        }

        return types.Contains(entry.Type) ? entry : throw new CryptographicException("mylife:wrong-key-type");
    }

    private enum KeyType
    {
        Master,
        Wrap,
        Aes,
        Ecdh,
        Ecdsa,
    }

    private sealed class KeyEntry : IDisposable
    {
        private KeyEntry(KeyType type, byte[]? secret, AsymmetricAlgorithm? algorithm)
        {
            Type = type;
            SecretBytesOrNull = secret;
            Algorithm = algorithm;
        }

        public KeyType Type { get; }

        private byte[]? SecretBytesOrNull { get; }

        public byte[] SecretBytes => SecretBytesOrNull ?? throw new CryptographicException("mylife:wrong-key-type");

        public AsymmetricAlgorithm? Algorithm { get; }

        public static KeyEntry Secret(KeyType type, byte[] secret) => new(type, secret, null);

        public static KeyEntry Asymmetric(KeyPairKind kind, AsymmetricAlgorithm algorithm) =>
            new(kind == KeyPairKind.Ecdh ? KeyType.Ecdh : KeyType.Ecdsa, null, algorithm);

        public void Dispose()
        {
            if (SecretBytesOrNull is not null)
            {
                CryptographicOperations.ZeroMemory(SecretBytesOrNull);
            }

            Algorithm?.Dispose();
        }
    }
}

/// <summary>Argon2id über Konscious.Security.Cryptography (RFC 9106, Version 0x13). Auch für den Anmeldehash auf dem Server.</summary>
[UnsupportedOSPlatform("browser")]
public static class Argon2
{
    public static async Task<byte[]> HashAsync(
        byte[] password, byte[] salt, int memoryKiB, int iterations, int parallelism, int length,
        byte[]? secret = null, byte[]? associatedData = null)
    {
        using var argon2 = new Argon2id(password)
        {
            Salt = salt,
            MemorySize = memoryKiB,
            Iterations = iterations,
            DegreeOfParallelism = parallelism,
            KnownSecret = secret,
            AssociatedData = associatedData,
        };
        return await argon2.GetBytesAsync(length);
    }
}
