using System.Security.Cryptography;
using Microsoft.JSInterop;

namespace MyLife.Crypto;

/// <summary>
/// C#-Fassade über <c>wwwroot/js/mylife.crypto.js</c> (Web Crypto API und Argon2id über hash-wasm).
/// Schlüsselmaterial bleibt im JavaScript-Modul; hier kommen nur Handles, Chiffrat und öffentliche Daten an.
/// </summary>
public sealed class JsMyLifeCrypto(IJSRuntime js) : IMyLifeCrypto
{
    internal const string ModulePath = "./js/mylife.crypto.js";

    private readonly SemaphoreSlim _moduleLock = new(1, 1);
    private IJSObjectReference? _module;

    public Task<byte[]> RandomBytesAsync(int length) =>
        CallAsync<byte[]>(JsFunctions.RandomBytes, length);

    public async Task<CryptoKeyHandle> DeriveMasterKeyAsync(string password, byte[] salt, Argon2Parameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        parameters.EnsureValid();
        var jsParameters = new { memoryKiB = parameters.MemoryKiB, iterations = parameters.Iterations, parallelism = parameters.Parallelism };
        return new(await CallAsync<string>(JsFunctions.DeriveMasterKey, password, salt, jsParameters));
    }

    public Task<byte[]> DeriveAuthHashAsync(CryptoKeyHandle masterKey) =>
        CallAsync<byte[]>(JsFunctions.DeriveAuthHash, masterKey.Id);

    public async Task<CryptoKeyHandle> DeriveKeyEncryptionKeyAsync(CryptoKeyHandle masterKey) =>
        new(await CallAsync<string>(JsFunctions.DeriveKeyEncryptionKey, masterKey.Id));

    public async Task<CryptoKeyHandle> ImportRecoveryKeyAsync(byte[] recoveryKey) =>
        new(await CallAsync<string>(JsFunctions.ImportRecoveryKey, recoveryKey));

    public async Task<CryptoKeyHandle> ImportPackageKeyAsync(byte[] packageKey) =>
        new(await CallAsync<string>(JsFunctions.ImportPackageKey, packageKey));

    public Task<byte[]> DeriveRecoveryAuthHashAsync(byte[] recoveryKey) =>
        CallAsync<byte[]>(JsFunctions.DeriveRecoveryAuthHash, recoveryKey);

    public async Task<CreatedKey> CreateKeyAsync(IReadOnlyList<KeyWrapping> wrappings)
    {
        ArgumentNullException.ThrowIfNull(wrappings);
        var jsWrappings = wrappings.Select(w => new { wrapper = w.Wrapper.Id, context = w.Context }).ToArray();
        var result = await CallAsync<JsCreatedKey>(JsFunctions.CreateKey, [jsWrappings]);
        return new(new(result.Handle), result.WrappedKeys);
    }

    public async Task<CryptoKeyHandle> UnwrapKeyAsync(CryptoKeyHandle wrapper, byte[] wrappedKey, string context) =>
        new(await CallAsync<string>(JsFunctions.UnwrapKey, wrapper.Id, wrappedKey, context));

    public Task<byte[]> RewrapKeyAsync(CryptoKeyHandle oldWrapper, byte[] wrappedKey, string oldContext, CryptoKeyHandle newWrapper, string newContext) =>
        CallAsync<byte[]>(JsFunctions.RewrapKey, oldWrapper.Id, wrappedKey, oldContext, newWrapper.Id, newContext);

    public Task<byte[]> EncryptAsync(CryptoKeyHandle key, byte[] plaintext, string context) =>
        CallAsync<byte[]>(JsFunctions.Encrypt, key.Id, plaintext, context);

    public Task<byte[]> DecryptAsync(CryptoKeyHandle key, byte[] blob, string context) =>
        CallAsync<byte[]>(JsFunctions.Decrypt, key.Id, blob, context);

    public Task<byte[]> EncryptChunkAsync(CryptoKeyHandle fileKey, int index, bool isLast, byte[] data, string context) =>
        CallAsync<byte[]>(JsFunctions.EncryptChunk, fileKey.Id, index, isLast, data, context);

    public Task<byte[]> DecryptChunkAsync(CryptoKeyHandle fileKey, int index, bool isLast, byte[] data, string context) =>
        CallAsync<byte[]>(JsFunctions.DecryptChunk, fileKey.Id, index, isLast, data, context);

    public async Task<CreatedKeyPair> CreateKeyPairAsync(KeyPairKind kind, CryptoKeyHandle wrapper, string context)
    {
        var result = await CallAsync<JsCreatedKeyPair>(JsFunctions.CreateKeyPair, KindName(kind), wrapper.Id, context);
        return new(new(result.Handle), result.PublicKey, result.WrappedPrivateKey);
    }

    public async Task<CryptoKeyHandle> UnwrapPrivateKeyAsync(KeyPairKind kind, CryptoKeyHandle wrapper, byte[] wrappedPrivateKey, string context) =>
        new(await CallAsync<string>(JsFunctions.UnwrapPrivateKey, KindName(kind), wrapper.Id, wrappedPrivateKey, context));

    public Task<byte[]> SealKeyAsync(CryptoKeyHandle wrapper, byte[] wrappedKey, string wrappedContext, byte[] recipientPublicKey, string envelopeContext) =>
        CallAsync<byte[]>(JsFunctions.SealKey, wrapper.Id, wrappedKey, wrappedContext, recipientPublicKey, envelopeContext);

    public async Task<CryptoKeyHandle> OpenSealedKeyAsync(CryptoKeyHandle privateKey, byte[] envelope, string context) =>
        new(await CallAsync<string>(JsFunctions.OpenSealedKey, privateKey.Id, envelope, context));

    public Task<byte[]> SignAsync(CryptoKeyHandle signingKey, byte[] data, string context) =>
        CallAsync<byte[]>(JsFunctions.Sign, signingKey.Id, data, context);

    public Task<bool> VerifyAsync(byte[] publicKey, byte[] data, byte[] signature, string context) =>
        CallAsync<bool>(JsFunctions.Verify, publicKey, data, signature, context);

    public Task<byte[]> Sha256Async(byte[] data) =>
        CallAsync<byte[]>(JsFunctions.Sha256, data);

    public Task<byte[]> HmacSha256Async(byte[] key, byte[] data) =>
        CallAsync<byte[]>(JsFunctions.HmacSha256, key, data);

    public async Task ReleaseAsync(CryptoKeyHandle key) =>
        await CallAsync<object?>(JsFunctions.Release, key.Id);

    public async Task ReleaseAllAsync() =>
        await CallAsync<object?>(JsFunctions.ReleaseAll);

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync(JsFunctions.ReleaseAll);
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Seite wird geschlossen; das Register verschwindet mit ihr.
            }
            catch (JSException)
            {
                // Aufräumen darf das Beenden nicht verhindern; das Register lebt ohnehin nur so lange wie die Seite.
            }
        }

        _moduleLock.Dispose();
    }

    private static string KindName(KeyPairKind kind) => kind switch
    {
        KeyPairKind.Ecdh => "ecdh",
        KeyPairKind.Ecdsa => "ecdsa",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private async Task<T> CallAsync<T>(string function, params object?[] args)
    {
        var module = await GetModuleAsync();
        try
        {
            return await module.InvokeAsync<T>(function, args);
        }
        catch (JSException exception)
        {
            // Die Meldungen des Moduls haben die Form "mylife:<code>" und enthalten keine Daten.
            throw new CryptographicException(ExtractCode(exception.Message), exception);
        }
    }

    internal static string ExtractCode(string message)
    {
        var start = message.IndexOf("mylife:", StringComparison.Ordinal);
        if (start < 0)
        {
            return "mylife:js-error";
        }

        var end = start + "mylife:".Length;
        while (end < message.Length && (char.IsAsciiLetterOrDigit(message[end]) || message[end] == '-'))
        {
            end++;
        }

        return message[start..end];
    }

    private async Task<IJSObjectReference> GetModuleAsync()
    {
        if (_module is not null)
        {
            return _module;
        }

        await _moduleLock.WaitAsync();
        try
        {
            return _module ??= await js.InvokeAsync<IJSObjectReference>("import", ModulePath);
        }
        finally
        {
            _moduleLock.Release();
        }
    }

    private sealed record JsCreatedKey(string Handle, byte[][] WrappedKeys);

    private sealed record JsCreatedKeyPair(string Handle, byte[] PublicKey, byte[] WrappedPrivateKey);
}

/// <summary>Namen der exportierten Funktionen in myLife.crypto.js. Ein Test prüft, dass es sie dort gibt.</summary>
internal static class JsFunctions
{
    public const string RandomBytes = "randomBytes";
    public const string DeriveMasterKey = "deriveMasterKey";
    public const string DeriveAuthHash = "deriveAuthHash";
    public const string DeriveKeyEncryptionKey = "deriveKeyEncryptionKey";
    public const string ImportRecoveryKey = "importRecoveryKey";
    public const string ImportPackageKey = "importPackageKey";
    public const string DeriveRecoveryAuthHash = "deriveRecoveryAuthHash";
    public const string CreateKey = "createKey";
    public const string UnwrapKey = "unwrapKey";
    public const string RewrapKey = "rewrapKey";
    public const string Encrypt = "encrypt";
    public const string Decrypt = "decrypt";
    public const string EncryptChunk = "encryptChunk";
    public const string DecryptChunk = "decryptChunk";
    public const string CreateKeyPair = "createKeyPair";
    public const string UnwrapPrivateKey = "unwrapPrivateKey";
    public const string SealKey = "sealKey";
    public const string OpenSealedKey = "openSealedKey";
    public const string Sign = "sign";
    public const string Verify = "verify";
    public const string Sha256 = "sha256";
    public const string HmacSha256 = "hmacSha256";
    public const string Release = "release";
    public const string ReleaseAll = "releaseAll";
}
