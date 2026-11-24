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

    /// <summary>TOTP-Geheimnis, mit ASP.NET Data Protection verschlüsselt.</summary>
    public string? TotpSecret { get; set; }

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
