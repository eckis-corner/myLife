namespace MyLife.Crypto;

/// <summary>
/// Versionen des Verschlüsselungsformats. Der Krypto-Kern (Argon2id, HKDF, AES-256-GCM, ECIES)
/// folgt in Meilenstein M1; siehe docs/architektur.md, Abschnitt 4.
/// </summary>
public static class CryptoFormat
{
    public const int CurrentVersion = 1;
}
