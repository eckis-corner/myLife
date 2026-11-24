namespace MyLife.Shared;

/// <summary>Öffentliche Basisinformationen der Instanz (ohne Umgebungs- oder Konfigurationsdetails).</summary>
/// <param name="Version">Version der Anwendung.</param>
/// <param name="CryptoFormatVersion">Version des Verschlüsselungsformats, das diese Instanz schreibt.</param>
public sealed record AppInfoDto(string Version, int CryptoFormatVersion);
