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
}
