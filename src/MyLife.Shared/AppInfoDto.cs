namespace MyLife.Shared;

/// <summary>Öffentliche Basisinformationen der Instanz (ohne Umgebungs- oder Konfigurationsdetails).</summary>
/// <param name="Version">Version der Anwendung.</param>
/// <param name="CryptoFormatVersion">Version des Verschlüsselungsformats, das diese Instanz schreibt.</param>
/// <param name="MyDocsUrl">Adresse von myDocs, die die CSP für den Abgleich freigibt (M5, 166); null ohne Brücke.</param>
public sealed record AppInfoDto(string Version, int CryptoFormatVersion, string? MyDocsUrl = null);
