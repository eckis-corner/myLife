namespace MyLife.Shared.Setup;

/// <summary>Ob die Instanz noch eingerichtet werden muss (es gibt noch keinen Nutzer).</summary>
public sealed record SetupStatusDto(bool Required);

/// <summary>Einrichtungscode aus dem Container-Protokoll. Schützt den Assistenten, solange die Instanz offen im Netz steht.</summary>
public sealed record SetupTokenRequest(string Token);

/// <summary>
/// Neues TOTP-Geheimnis (Base32) und dieselbe Angabe geschützt für die nächsten Schritte. Der Server hält sich
/// so keinen Zustand für den Assistenten; die geschützte Form ist 30 Minuten gültig.
/// </summary>
public sealed record TotpEnrollmentDto(string Secret, string Enrollment);

public sealed record TotpVerifyRequest(string Token, string Enrollment, string Code);

/// <summary>Bestätigt, dass die Authenticator-App einen gültigen Code erzeugt hat.</summary>
public sealed record TotpConfirmationDto(string Confirmation);

/// <summary>
/// Legt den ersten Nutzer als Administrator an. Alles Schlüsselmaterial ist im Browser verschlüsselt; der Server
/// bekommt weder Passwort noch Recovery-Key noch einen Klartextschlüssel.
/// </summary>
public sealed record SetupCompleteRequest
{
    public required string Token { get; init; }
    public required string Email { get; init; }
    public required string DisplayName { get; init; }
    public required string Language { get; init; }

    public required byte[] KdfSalt { get; init; }
    public required int KdfMemoryKiB { get; init; }
    public required int KdfIterations { get; init; }
    public required int KdfParallelism { get; init; }

    /// <summary>HKDF(Stammschlüssel, "mylife/auth"); der Server speichert davon nur einen eigenen Argon2id-Hash.</summary>
    public required byte[] AuthHash { get; init; }

    public required byte[] WrappedVmk { get; init; }
    public required byte[] WrappedVmkByRecovery { get; init; }
    public required byte[] PublicEncryptionKey { get; init; }
    public required byte[] WrappedEncryptionPrivateKey { get; init; }
    public required byte[] PublicSigningKey { get; init; }
    public required byte[] WrappedSigningPrivateKey { get; init; }

    public required string TotpConfirmation { get; init; }
}

/// <summary>Grenzen für Eingaben im Assistenten, im Browser und auf dem Server gleich.</summary>
public static class SetupLimits
{
    public const int MaxEmailLength = 254;
    public const int MaxDisplayNameLength = 100;
    public const int MinPasswordLength = 12;
}
