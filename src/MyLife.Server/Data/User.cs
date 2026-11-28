namespace MyLife.Server.Data;

/// <summary>
/// Nutzer der Instanz (Architektur 6.1). Alle Schlüssel liegen nur verpackt vor; der Server kann keinen davon öffnen.
/// </summary>
public sealed class User
{
    public Guid Id { get; set; }

    /// <summary>E-Mail-Adresse, getrimmt und klein geschrieben.</summary>
    public required string Email { get; set; }

    public required string DisplayName { get; set; }

    public bool IsAdmin { get; set; }

    public UserStatus Status { get; set; }

    public required string Language { get; set; }

    /// <summary>Argon2id-Hash (PHC-Format) über den Anmeldehash, den der Browser aus dem Passwort ableitet.</summary>
    public required string AuthHash { get; set; }

    public required byte[] KdfSalt { get; set; }

    public int KdfMemoryKiB { get; set; }

    public int KdfIterations { get; set; }

    public int KdfParallelism { get; set; }

    public required byte[] WrappedVmk { get; set; }

    public required byte[] WrappedVmkByRecovery { get; set; }

    public required byte[] PublicEncryptionKey { get; set; }

    public required byte[] WrappedEncryptionPrivateKey { get; set; }

    public required byte[] PublicSigningKey { get; set; }

    public required byte[] WrappedSigningPrivateKey { get; set; }

    /// <summary>Argon2id-Hash (PHC) über den Prüfwert des Recovery-Keys; fehlt bei Konten aus der Zeit vor M1.3.</summary>
    public string? RecoveryAuthHash { get; set; }

    /// <summary>TOTP-Geheimnis, mit ASP.NET Data Protection verschlüsselt.</summary>
    public string? TotpSecret { get; set; }

    /// <summary>Zuletzt angenommener TOTP-Zeitschritt; ein Code gilt nur einmal.</summary>
    public long? TotpLastStep { get; set; }

    /// <summary>Ändert sich bei Passwortwechsel und Wiederherstellung und macht alle anderen Sitzungen ungültig.</summary>
    public required string SecurityStamp { get; set; }

    public int FailedLoginCount { get; set; }

    public DateTimeOffset? LockoutUntil { get; set; }

    public List<Passkey> Passkeys { get; } = [];

    /// <summary>
    /// 🔒 Verbindung zu myDocs (M5, 167): Adresse und lesendes Token als JSON, verschlüsselt mit dem Tresorschlüssel
    /// (Kontext <c>mydocs-bridge</c>). Gehört zum Konto, nicht zu einem Bereich: Vertrauenspersonen bekommen es nie.
    /// </summary>
    public byte[]? MyDocsBridge { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>PostgreSQL-Systemspalte <c>xmin</c> für optimistische Nebenläufigkeit.</summary>
    public uint Version { get; set; }
}

public enum UserStatus
{
    Invited = 0,
    Active = 1,
    Locked = 2,
}
