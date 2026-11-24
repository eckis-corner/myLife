using System.Globalization;

namespace MyLife.Crypto;

/// <summary>
/// Kontexte, unter denen myLife Schlüssel und Daten verschlüsselt (Teil der AAD, siehe docs/kryptoformat.md).
/// Wer einen Wert ändert, macht vorhandene Tresore unlesbar.
/// </summary>
public static class KeyContexts
{
    /// <summary>Tresorschlüssel, verpackt mit KEK und mit dem Recovery-Schlüssel.</summary>
    public const string Vmk = "vmk";

    /// <summary>Privater ECDH-Schlüssel für Umschläge, verpackt mit dem Tresorschlüssel.</summary>
    public const string EncryptionPrivateKey = "private-key:ecdh";

    /// <summary>Privater ECDSA-Schlüssel für Signaturen, verpackt mit dem Tresorschlüssel.</summary>
    public const string SigningPrivateKey = "private-key:ecdsa";

    /// <summary>Bereichsschlüssel 0–13, verpackt mit dem Tresorschlüssel.</summary>
    public static string Area(int area) => string.Create(CultureInfo.InvariantCulture, $"area:{area}");

    /// <summary>Inhalt eines Objekts (JSON), verschlüsselt mit dem Bereichsschlüssel. Typ und ID binden das Chiffrat.</summary>
    public static string VaultObject(string typeKey, Guid id) => string.Create(CultureInfo.InvariantCulture, $"object:{typeKey}:{id:D}");

    /// <summary>Dateischlüssel eines Dokuments, verpackt mit dem Bereichsschlüssel.</summary>
    public static string FileKey(Guid documentId) => string.Create(CultureInfo.InvariantCulture, $"file-key:{documentId:D}");

    /// <summary>Blöcke eines Dokuments, verschlüsselt mit dem Dateischlüssel.</summary>
    public static string Document(Guid documentId) => string.Create(CultureInfo.InvariantCulture, $"document:{documentId:D}");

    /// <summary>Metadaten eines Dokuments (Bezeichnung, Dateiname, Typ), verschlüsselt mit dem Bereichsschlüssel.</summary>
    public static string DocumentMetadata(Guid documentId) => string.Create(CultureInfo.InvariantCulture, $"document-meta:{documentId:D}");

    /// <summary>Einladungsgeheimnis der einladenden Person, verschlüsselt mit ihrem Tresorschlüssel (Architektur 4.3).</summary>
    public static string InviteSecret(Guid invitationId) => string.Create(CultureInfo.InvariantCulture, $"invite-secret:{invitationId:D}");

    /// <summary>Gepinnter Fingerprint einer anderen Person, verschlüsselt mit dem eigenen Tresorschlüssel.</summary>
    public static string Pin(Guid subjectId) => string.Create(CultureInfo.InvariantCulture, $"pin:{subjectId:D}");

    /// <summary>Gegenzeichnung (ECDSA) des Fingerprints einer Person.</summary>
    public static string Endorsement(Guid subjectId) => string.Create(CultureInfo.InvariantCulture, $"endorsement:{subjectId:D}");
}

/// <summary>Fingerprint und Einladungsbindung (Architektur 4.3, docs/kryptoformat.md 7).</summary>
public static class KeyBinding
{
    /// <summary>Daten des Fingerprints: öffentlicher ECDH- und ECDSA-Schlüssel hintereinander; Fingerprint = SHA-256 davon.</summary>
    public static byte[] FingerprintData(byte[] publicEncryptionKey, byte[] publicSigningKey)
    {
        ArgumentNullException.ThrowIfNull(publicEncryptionKey);
        ArgumentNullException.ThrowIfNull(publicSigningKey);
        return [.. publicEncryptionKey, .. publicSigningKey];
    }

    /// <summary>Daten der Bindung: HMAC(Einladungsgeheimnis, "invite:{ID}:" | Fingerprint).</summary>
    public static byte[] InviteBindingData(Guid invitationId, byte[] fingerprint)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);
        return [.. System.Text.Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"invite:{invitationId:D}:")), .. fingerprint];
    }

    /// <summary>Kurzform zum Vergleichen: 64 Hex-Zeichen in Vierergruppen.</summary>
    public static string Format(byte[] fingerprint) =>
        string.Join(' ', Convert.ToHexString(fingerprint ?? []).Chunk(4).Select(c => new string(c)));
}
