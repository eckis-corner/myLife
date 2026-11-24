namespace MyLife.Shared.Vault;

/// <summary>Status eines Objekts. Beendete Objekte bleiben als Historie und sind standardmäßig ausgeblendet.</summary>
public enum ObjectStatus
{
    Active = 0,
    Ended = 1,
}

/// <summary>Bereich mit verpacktem Bereichsschlüssel (fehlt, solange der Bereich nie benutzt wurde).</summary>
public sealed record AreaDto(int Area, byte[]? WrappedKey, DateTimeOffset? LastReviewedAt);

public sealed record AreaKeyRequest(byte[] WrappedKey);

/// <summary>Ein Objekt, wie es der Server kennt: Metadaten im Klartext, Inhalt als Chiffrat.</summary>
public sealed record VaultObjectDto(
    Guid Id,
    int Area,
    string TypeKey,
    byte[] Payload,
    int SchemaVersion,
    ObjectStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    uint Version,
    bool IsDeleted,
    DateTimeOffset? DeletedAt);

/// <summary>Die ID vergibt der Browser, weil sie im Kontext der Verschlüsselung steckt.</summary>
public sealed record CreateObjectRequest(Guid Id, int Area, string TypeKey, byte[] Payload, int SchemaVersion, ObjectStatus Status);

/// <summary><see cref="Version"/> ist der zuletzt gelesene Stand; hat ihn inzwischen jemand geändert, antwortet der Server mit 409.</summary>
public sealed record UpdateObjectRequest(byte[] Payload, int SchemaVersion, ObjectStatus Status, uint Version);

public sealed record DocumentDto(
    Guid Id,
    Guid ObjectId,
    byte[] WrappedFileKey,
    byte[] Metadata,
    long CipherSize,
    bool IsComplete,
    DateTimeOffset CreatedAt,
    bool IsDeleted,
    DateTimeOffset? DeletedAt);

public sealed record CreateDocumentRequest(Guid Id, Guid ObjectId, byte[] WrappedFileKey, byte[] Metadata, long CipherSize);

public sealed record AuditEntryDto(long Id, DateTimeOffset At, string Action, string? TargetType, Guid? TargetId, string? Ip, string? ActorName, string? Details);

public sealed record AuditPageDto(IReadOnlyList<AuditEntryDto> Items, int Total);

/// <summary>Ereignisse, die nur der Browser kennt, etwa ein Export.</summary>
public sealed record AuditEventRequest(string Action, string? Details);

/// <summary>Grenzen für Inhalte (Architektur 6.3, Entscheidung 18).</summary>
public static class VaultLimits
{
    public const int MaxPayloadBytes = 256 * 1024;
    public const int MaxMetadataBytes = 8 * 1024;
    public const long MaxDocumentBytes = 25L * 1024 * 1024;
    public const int ChunkSize = 1024 * 1024;
    public const int ChunkOverhead = 16;
    public const int TrashDays = 30;

    /// <summary>Anzahl der Blöcke einer Datei; auch eine leere Datei hat einen (leeren) Block.</summary>
    public static int ChunkCount(long plainSize) => plainSize <= 0 ? 1 : (int)((plainSize + ChunkSize - 1) / ChunkSize);

    /// <summary>Größe des Chiffrats: Klartext plus 16 Byte Tag je Block.</summary>
    public static long CipherSize(long plainSize) => Math.Max(plainSize, 0) + ((long)ChunkCount(plainSize) * ChunkOverhead);

    public static long MaxCipherSize => CipherSize(MaxDocumentBytes);
}

/// <summary>Fehlercodes der Tresor-API (Titel der Problem-Antwort, Ressource <c>VaultError_{Code}</c>).</summary>
public static class VaultErrors
{
    public const string Conflict = "vault-conflict";
    public const string NotFound = "vault-not-found";
    public const string Invalid = "vault-invalid";
    public const string AreaKeyExists = "vault-area-key-exists";
    public const string AreaKeyMissing = "vault-area-key-missing";
    public const string TooLarge = "vault-too-large";
    public const string Failed = "vault-failed";
}

/// <summary>Aktionen im Audit-Log (Ressource <c>Audit_{Action}</c>).</summary>
public static class AuditActions
{
    public const string Login = "login";
    public const string LoginFailed = "login-failed";
    public const string Logout = "logout";
    public const string PasswordChanged = "password-changed";
    public const string Recovered = "recovered";
    public const string PasskeyAdded = "passkey-added";
    public const string PasskeyRemoved = "passkey-removed";
    public const string AreaCreated = "area-created";
    public const string AreaReviewed = "area-reviewed";
    public const string ObjectCreated = "object-created";
    public const string ObjectUpdated = "object-updated";
    public const string ObjectDeleted = "object-deleted";
    public const string ObjectRestored = "object-restored";
    public const string ObjectPurged = "object-purged";
    public const string DocumentUploaded = "document-uploaded";
    public const string DocumentDownloaded = "document-downloaded";
    public const string DocumentDeleted = "document-deleted";
    public const string DocumentRestored = "document-restored";
    public const string DocumentPurged = "document-purged";
    public const string ExportJson = "export-json";
    public const string ExportPdf = "export-pdf";
    public const string ExportPrint = "export-print";
    public const string ImportJson = "import-json";

    /// <summary>Was der Browser selbst melden darf.</summary>
    public static IReadOnlySet<string> ClientReported { get; } = new HashSet<string>(StringComparer.Ordinal) { ExportJson, ExportPdf, ExportPrint, ImportJson };
}
