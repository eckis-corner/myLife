namespace MyLife.Crypto;

/// <summary>
/// Krypto-Kern von myLife. Schlüssel bleiben in der Implementierung; Aufrufer arbeiten nur mit
/// <see cref="CryptoKeyHandle"/>. Im Browser übernimmt das <see cref="JsMyLifeCrypto"/> (Web Crypto API),
/// in .NET <see cref="ReferenceMyLifeCrypto"/> (Tests, Werkzeug mylife-decrypt).
/// Jeder <c>context</c> landet in der AAD bzw. der signierten Nachricht und bindet ein Chiffrat an seinen Zweck,
/// etwa <c>vmk</c>, <c>area:3</c> oder <c>object:bank_account:&lt;id&gt;</c>.
/// Fehler beim Entschlüsseln und ungültige Eingaben lösen eine
/// <see cref="System.Security.Cryptography.CryptographicException"/> aus.
/// </summary>
public interface IMyLifeCrypto : IAsyncDisposable
{
    /// <summary>Kryptografisch sichere Zufallsbytes (1–1024), etwa Salt oder Recovery-Key.</summary>
    Task<byte[]> RandomBytesAsync(int length);

    /// <summary>Stammschlüssel: Argon2id über das Master-Passwort (Unicode NFC, UTF-8).</summary>
    Task<CryptoKeyHandle> DeriveMasterKeyAsync(string password, byte[] salt, Argon2Parameters parameters);

    /// <summary>Anmeldehash für den Server (32 Byte), HKDF mit <c>mylife/auth</c>.</summary>
    Task<byte[]> DeriveAuthHashAsync(CryptoKeyHandle masterKey);

    /// <summary>Key-Encryption-Key, HKDF mit <c>mylife/kek</c>. Kann nur Schlüssel ver- und entpacken.</summary>
    Task<CryptoKeyHandle> DeriveKeyEncryptionKeyAsync(CryptoKeyHandle masterKey);

    /// <summary>Schlüssel aus dem Recovery-Key (32 Byte), HKDF mit <c>mylife/recovery</c>. Kann nur Schlüssel ver- und entpacken.</summary>
    Task<CryptoKeyHandle> ImportRecoveryKeyAsync(byte[] recoveryKey);

    /// <summary>
    /// Schlüssel eines Notfallpakets aus 32 Byte (gedruckt als 24 Wörter), HKDF mit <c>mylife/package</c>. Kann nur
    /// Daten ver- und entschlüsseln (M2.7).
    /// </summary>
    Task<CryptoKeyHandle> ImportPackageKeyAsync(byte[] packageKey);

    /// <summary>Prüfwert des Recovery-Keys für den Server (32 Byte), HKDF mit <c>mylife/recovery-auth</c>.</summary>
    Task<byte[]> DeriveRecoveryAuthHashAsync(byte[] recoveryKey);

    /// <summary>Erzeugt einen AES-256-Schlüssel und verpackt ihn mit jedem angegebenen Schlüssel.</summary>
    Task<CreatedKey> CreateKeyAsync(IReadOnlyList<KeyWrapping> wrappings);

    Task<CryptoKeyHandle> UnwrapKeyAsync(CryptoKeyHandle wrapper, byte[] wrappedKey, string context);

    /// <summary>Verpackt einen Schlüssel neu (Passwort ändern), ohne ihn offenzulegen.</summary>
    Task<byte[]> RewrapKeyAsync(CryptoKeyHandle oldWrapper, byte[] wrappedKey, string oldContext, CryptoKeyHandle newWrapper, string newContext);

    Task<byte[]> EncryptAsync(CryptoKeyHandle key, byte[] plaintext, string context);

    Task<byte[]> DecryptAsync(CryptoKeyHandle key, byte[] blob, string context);

    /// <summary>Ein Block eines Dokuments, höchstens 1 MiB. Ein Dateischlüssel gehört zu genau einer Datei.</summary>
    Task<byte[]> EncryptChunkAsync(CryptoKeyHandle fileKey, int index, bool isLast, byte[] data, string context);

    Task<byte[]> DecryptChunkAsync(CryptoKeyHandle fileKey, int index, bool isLast, byte[] data, string context);

    /// <summary>Schlüsselpaar P-256; der private Schlüssel wird als PKCS#8 verpackt.</summary>
    Task<CreatedKeyPair> CreateKeyPairAsync(KeyPairKind kind, CryptoKeyHandle wrapper, string context);

    Task<CryptoKeyHandle> UnwrapPrivateKeyAsync(KeyPairKind kind, CryptoKeyHandle wrapper, byte[] wrappedPrivateKey, string context);

    /// <summary>Legt einen verpackten Schlüssel in einen Umschlag (ECIES) für den öffentlichen Schlüssel einer anderen Person.</summary>
    Task<byte[]> SealKeyAsync(CryptoKeyHandle wrapper, byte[] wrappedKey, string wrappedContext, byte[] recipientPublicKey, string envelopeContext);

    Task<CryptoKeyHandle> OpenSealedKeyAsync(CryptoKeyHandle privateKey, byte[] envelope, string context);

    /// <summary>ECDSA P-256 / SHA-256, Signatur im Format IEEE P1363 (64 Byte).</summary>
    Task<byte[]> SignAsync(CryptoKeyHandle signingKey, byte[] data, string context);

    Task<bool> VerifyAsync(byte[] publicKey, byte[] data, byte[] signature, string context);

    Task<byte[]> Sha256Async(byte[] data);

    Task<byte[]> HmacSha256Async(byte[] key, byte[] data);

    /// <summary>Verwirft einen Schlüssel.</summary>
    Task ReleaseAsync(CryptoKeyHandle key);

    /// <summary>Verwirft alle Schlüssel, etwa bei der Abmeldung.</summary>
    Task ReleaseAllAsync();
}

/// <summary>Verweis auf einen Schlüssel in der Implementierung. Enthält kein Schlüsselmaterial.</summary>
public readonly record struct CryptoKeyHandle(string Id)
{
    public override string ToString() => "CryptoKeyHandle";
}

/// <summary>Mit welchem Schlüssel und unter welchem Kontext ein neuer Schlüssel verpackt wird.</summary>
public sealed record KeyWrapping(CryptoKeyHandle Wrapper, string Context);

/// <summary>Neuer Schlüssel und seine verpackten Formen in der Reihenfolge der <see cref="KeyWrapping"/>s.</summary>
public sealed record CreatedKey(CryptoKeyHandle Handle, IReadOnlyList<byte[]> WrappedKeys);

/// <summary>Neues Schlüsselpaar: privater Schlüssel als Handle und verpackt, öffentlicher Schlüssel unkomprimiert (65 Byte).</summary>
public sealed record CreatedKeyPair(CryptoKeyHandle Handle, byte[] PublicKey, byte[] WrappedPrivateKey);

public enum KeyPairKind
{
    /// <summary>ECDH P-256 zum Öffnen von Umschlägen.</summary>
    Ecdh,

    /// <summary>ECDSA P-256 zum Signieren.</summary>
    Ecdsa,
}
