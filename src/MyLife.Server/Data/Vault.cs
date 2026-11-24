using MyLife.Shared.Vault;

namespace MyLife.Server.Data;

/// <summary>Bereich eines Tresors mit verpacktem Bereichsschlüssel (Architektur 6.1, Tabelle <c>areas</c>).</summary>
public sealed class VaultArea
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    /// <summary>Nummer des Bereichs, 0–13.</summary>
    public int AreaKey { get; set; }

    /// <summary>Bereichsschlüssel, verpackt mit dem Tresorschlüssel (Kontext <c>area:{n}</c>).</summary>
    public required byte[] WrappedAreaKey { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastReviewedAt { get; set; }
}

/// <summary>Ein Objekt (Tabelle <c>objects</c>). Der Server kennt Typ, Bereich, Status und Zeiten, nicht den Inhalt.</summary>
public sealed class VaultObject
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public int AreaKey { get; set; }

    public required string TypeKey { get; set; }

    /// <summary>Verschlüsseltes JSON (<see cref="VaultPayload"/>).</summary>
    public required byte[] Payload { get; set; }

    /// <summary>Eigener Objektschlüssel, verpackt mit dem Bereichsschlüssel (Briefe, M2.3); sonst null.</summary>
    public byte[]? WrappedKey { get; set; }

    public int SchemaVersion { get; set; }

    public ObjectStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public List<VaultDocument> Documents { get; } = [];

    /// <summary>PostgreSQL-Systemspalte <c>xmin</c>.</summary>
    public uint Version { get; set; }
}

/// <summary>Dokument an einem Objekt (Tabelle <c>documents</c>). Der Inhalt liegt verschlüsselt unter <c>files/{StorageKey}</c>.</summary>
public sealed class VaultDocument
{
    public Guid Id { get; set; }

    public Guid ObjectId { get; set; }

    public VaultObject? Parent { get; set; }

    public Guid OwnerId { get; set; }

    /// <summary>Dateischlüssel, verpackt mit dem Bereichsschlüssel.</summary>
    public required byte[] WrappedFileKey { get; set; }

    /// <summary>Verschlüsselte <see cref="DocumentMetadata"/>.</summary>
    public required byte[] Metadata { get; set; }

    public long CipherSize { get; set; }

    public required string StorageKey { get; set; }

    /// <summary>SHA-256 des Chiffrats, gesetzt nach dem Hochladen.</summary>
    public byte[]? CipherSha256 { get; set; }

    public bool IsComplete { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}

/// <summary>Eintrag im Audit-Log (Tabelle <c>audit_log</c>). Ohne Inhalte, nur wer, wann, was.</summary>
public sealed class AuditEntry
{
    public long Id { get; set; }

    public DateTimeOffset At { get; set; }

    /// <summary>Wer gehandelt hat; null bei Ereignissen ohne Anmeldung (etwa ein fehlgeschlagener Versuch).</summary>
    public Guid? ActorId { get; set; }

    /// <summary>Wessen Tresor betroffen ist; null bei reinen Systemereignissen.</summary>
    public Guid? OwnerId { get; set; }

    public required string Action { get; set; }

    public string? TargetType { get; set; }

    public Guid? TargetId { get; set; }

    /// <summary>Gekürzte IP-Adresse (IPv4 /24, IPv6 /48).</summary>
    public string? Ip { get; set; }

    public string? Details { get; set; }
}
