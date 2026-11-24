namespace MyLife.Shared.Auth;

/// <summary>Name des Headers, den jeder schreibende API-Aufruf tragen muss (Schutz gegen CSRF zusätzlich zu SameSite=Strict).</summary>
public static class ApiHeaders
{
    public const string Csrf = "X-MyLife-Request";
    public const string CsrfValue = "1";
}

public sealed record PreloginRequest(string Email);

/// <summary>Salt und Argon2-Parameter zur Adresse. Für unbekannte Adressen gibt es gleichbleibende Scheinwerte.</summary>
public sealed record PreloginDto(byte[] KdfSalt, int KdfMemoryKiB, int KdfIterations, int KdfParallelism);

public sealed record LoginRequest(string Email, byte[] AuthHash);

/// <summary>
/// Passwort stimmt; jetzt fehlt der zweite Faktor. <see cref="Pending"/> ist 5 Minuten gültig. Hat der Admin die
/// 2FA zurückgesetzt, gibt es weder TOTP noch Passkey: Dann richtet der Nutzer jetzt eine Authenticator-App ein.
/// </summary>
public sealed record LoginChallengeDto(string Pending, bool Totp, bool Passkey)
{
    public bool Enroll => !Totp && !Passkey;
}

public sealed record TotpLoginRequest(string Pending, string Code);

public sealed record TotpLoginSetupRequest(string Pending);

/// <summary>Neues TOTP-Geheimnis bei der Anmeldung: Klartext für den QR-Code, geschützt für die Bestätigung.</summary>
public sealed record TotpLoginSetupDto(string Secret, string Enrollment);

public sealed record TotpLoginEnrollRequest(string Pending, string Enrollment, string Code);

public sealed record PasskeyLoginOptionsRequest(string Pending);

/// <summary>WebAuthn-Optionen als JSON für den Browser und dieselben Optionen geschützt für die Prüfung danach.</summary>
public sealed record PasskeyOptionsDto(string OptionsJson, string OptionsToken);

public sealed record PasskeyLoginRequest(string Pending, string OptionsToken, string AssertionJson);

/// <summary>
/// Angemeldete Sitzung. Enthält das verpackte Schlüsselmaterial, damit der Browser den Tresor mit dem Passwort
/// entsperren kann, auch nach dem Neuladen der Seite.
/// </summary>
public sealed record SessionDto
{
    public required Guid UserId { get; init; }
    public required string Email { get; init; }
    public required string DisplayName { get; init; }
    public required bool IsAdmin { get; init; }
    public required string Language { get; init; }
    public required int IdleTimeoutMinutes { get; init; }
    public required byte[] KdfSalt { get; init; }
    public required int KdfMemoryKiB { get; init; }
    public required int KdfIterations { get; init; }
    public required int KdfParallelism { get; init; }
    public required byte[] WrappedVmk { get; init; }

    /// <summary>Tresorschlüssel, verpackt mit dem Recovery-Key; für den JSON-Export (öffnen auch mit den 24 Wörtern).</summary>
    public byte[]? WrappedVmkByRecovery { get; init; }

    public required byte[] PublicEncryptionKey { get; init; }
    public required byte[] WrappedEncryptionPrivateKey { get; init; }
    public required byte[] PublicSigningKey { get; init; }
    public required byte[] WrappedSigningPrivateKey { get; init; }
}

/// <summary>Neues Passwort: neuer Salt, neuer Anmeldehash und der mit dem neuen KEK verpackte Tresorschlüssel.</summary>
public sealed record PasswordMaterial(byte[] KdfSalt, int KdfMemoryKiB, int KdfIterations, int KdfParallelism, byte[] AuthHash, byte[] WrappedVmk);

public sealed record ChangePasswordRequest(byte[] CurrentAuthHash, PasswordMaterial NewPassword);

public sealed record RecoveryRequest(string Email, byte[] RecoveryAuthHash);

/// <summary>Recovery-Key stimmt. Der Browser verpackt den Tresorschlüssel mit dem neuen Passwort neu.</summary>
public sealed record RecoveryChallengeDto(string Pending, byte[] WrappedVmkByRecovery);

public sealed record RecoveryCompleteRequest(string Pending, PasswordMaterial NewPassword);

public sealed record PasskeyDto(Guid Id, string Name, DateTimeOffset CreatedAt, DateTimeOffset? LastUsedAt);

public sealed record PasskeyRegisterRequest(string Name, string OptionsToken, string AttestationJson);

/// <summary>Feste Fehlercodes der Anmeldung (Problem-Titel), im Client als Ressourcenschlüssel AuthError_&lt;code&gt;.</summary>
public static class AuthErrors
{
    public const string InvalidCredentials = "auth-invalid-credentials";
    public const string Locked = "auth-locked";

    /// <summary>Vom Administrator gesperrt (erst nach richtigem Passwort gemeldet).</summary>
    public const string Disabled = "auth-disabled";
    public const string Expired = "auth-expired";
    public const string InvalidCode = "auth-invalid-code";
    public const string PasskeyFailed = "auth-passkey-failed";
    public const string InvalidInput = "auth-invalid-input";
    public const string RateLimited = "auth-rate-limited";
    public const string Failed = "auth-failed";
}
