using MyLife.Shared.Trust;

namespace MyLife.Server.Data;

/// <summary>Vertrauensperson (Tabelle <c>trust_relations</c>, Architektur 6.1).</summary>
public sealed class TrustRelation
{
    public Guid OwnerId { get; set; }

    public Guid TrusteeId { get; set; }

    /// <summary>Beziehung und Notiz, verschlüsselt mit dem Tresorschlüssel des Eigentümers.</summary>
    public required byte[] EncryptedProfile { get; set; }

    /// <summary>Darf einen Zugang beantragen (Entscheidung 14). Klartext, weil der Server den Antrag prüft.</summary>
    public bool CanRequestAccess { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// Umschlag (Tabelle <c>grants</c>): ein Bereichs- oder Briefschlüssel, per ECIES an die Vertrauensperson
/// versiegelt und vom Eigentümer signiert. Der Server liefert ihn erst aus, wenn der Modus gilt (10a);
/// Live sofort (46b).
/// </summary>
public sealed class Grant
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public Guid TrusteeId { get; set; }

    public GrantKind Kind { get; set; }

    /// <summary>Bereich bei <see cref="GrantKind.Area"/>, sonst der Bereich des Briefs.</summary>
    public int AreaKey { get; set; }

    /// <summary>Brief bei <see cref="GrantKind.Letter"/>.</summary>
    public Guid? ObjectId { get; set; }

    public GrantMode Mode { get; set; }

    public required byte[] Envelope { get; set; }

    public required byte[] Signature { get; set; }

    /// <summary>Fingerprint der Vertrauensperson beim Versiegeln; passt er nicht mehr, ist der Umschlag veraltet.</summary>
    public required byte[] Fingerprint { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
