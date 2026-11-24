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
}
