namespace MyLife.Server.Data;

/// <summary>
/// Einladung (Tabelle <c>invitations</c>, M2.2). Der Server kennt nur den Hash des Link-Tokens; das
/// Einladungsgeheimnis für die Schlüsselbindung liegt mit dem Tresorschlüssel der einladenden Person verschlüsselt
/// vor und steht im Link nur im Fragment (Architektur 4.3).
/// </summary>
public sealed class Invitation
{
    public Guid Id { get; set; }

    public Guid InviterId { get; set; }

    /// <summary>Adresse der eingeladenen Person, getrimmt und klein geschrieben.</summary>
    public required string Email { get; set; }

    public required string Language { get; set; }

    public required byte[] TokenHash { get; set; }

    /// <summary>Einladungsgeheimnis, verschlüsselt mit dem Tresorschlüssel der einladenden Person.</summary>
    public required byte[] EncryptedSecret { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public DateTimeOffset? AcceptedAt { get; set; }

    public Guid? InviteeId { get; set; }

    /// <summary>HMAC(Geheimnis, Fingerprint der eingeladenen Person), vom Browser der eingeladenen Person.</summary>
    public byte[]? Binding { get; set; }

    public DateTimeOffset? VerifiedAt { get; set; }

    public DateTimeOffset? MismatchAt { get; set; }
}

/// <summary>Gegenzeichnung (Tabelle <c>key_endorsements</c>): Wer bestätigt welchen Fingerprint von wem.</summary>
public sealed class KeyEndorsement
{
    public Guid SignerId { get; set; }

    public Guid SubjectId { get; set; }

    /// <summary>SHA-256 über beide öffentlichen Schlüssel der Person zum Zeitpunkt der Gegenzeichnung.</summary>
    public required byte[] Fingerprint { get; set; }

    /// <summary>ECDSA-Signatur des Unterzeichners über den Fingerprint (Kontext <c>endorsement:{SubjectId}</c>).</summary>
    public required byte[] Signature { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// Gepinnter Fingerprint (Tabelle <c>pinned_keys</c>), verschlüsselt mit dem Tresorschlüssel des Eigentümers.
/// Der Server kann ihn weder lesen noch unbemerkt austauschen.
/// </summary>
public sealed class PinnedKey
{
    public Guid OwnerId { get; set; }

    public Guid SubjectId { get; set; }

    public required byte[] EncryptedPin { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
