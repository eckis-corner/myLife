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
    DateTimeOffset? DeletedAt,
    byte[]? WrappedKey = null);

/// <summary>
/// Die ID vergibt der Browser, weil sie im Kontext der Verschlüsselung steckt. <see cref="WrappedKey"/>: eigener
/// Objektschlüssel, verpackt mit dem Bereichsschlüssel (Briefe, M2.3); sonst gilt der Bereichsschlüssel.
/// </summary>
public sealed record CreateObjectRequest(Guid Id, int Area, string TypeKey, byte[] Payload, int SchemaVersion, ObjectStatus Status, byte[]? WrappedKey = null);

/// <summary><see cref="Version"/> ist der zuletzt gelesene Stand; hat ihn inzwischen jemand geändert, antwortet der Server mit 409.</summary>
public sealed record UpdateObjectRequest(byte[] Payload, int SchemaVersion, ObjectStatus Status, uint Version, byte[]? WrappedKey = null);

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
    public const string ExportPackage = "export-package";
    public const string ImportJson = "import-json";
    public const string TotpEnrolled = "totp-enrolled";

    // Verwaltung (M2.1): Akteur ist der Admin, Eigentümer das betroffene Konto bzw. null bei Systemereignissen.
    public const string AdminSettingsChanged = "admin-settings-changed";
    public const string AdminMailTested = "admin-mail-tested";
    public const string AdminNtfyTested = "admin-ntfy-tested";
    public const string AdminMailRetried = "admin-mail-retried";
    public const string AdminUserLocked = "admin-user-locked";
    public const string AdminUserUnlocked = "admin-user-unlocked";
    public const string AdminTwoFactorReset = "admin-2fa-reset";
    public const string AdminRoleGranted = "admin-role-granted";
    public const string AdminRoleRevoked = "admin-role-revoked";
    public const string AdminDeletionRequested = "admin-deletion-requested";
    public const string AdminDeletionCancelled = "admin-deletion-cancelled";
    public const string AdminInvitationRevoked = "admin-invitation-revoked";
    public const string AccountDeleted = "account-deleted";
    public const string MailFailed = "mail-failed";

    // Familie (M2.2)
    public const string InvitationCreated = "invitation-created";
    public const string InvitationRenewed = "invitation-renewed";
    public const string InvitationRevoked = "invitation-revoked";
    public const string InvitationAccepted = "invitation-accepted";
    public const string AccountCreated = "account-created";
    public const string KeyVerified = "key-verified";
    public const string KeyMismatch = "key-mismatch";
    public const string KeyEndorsed = "key-endorsed";

    // Vertrauenspersonen (M2.3)
    public const string TrusteeAdded = "trustee-added";
    public const string TrusteeChanged = "trustee-changed";
    public const string TrusteeRemoved = "trustee-removed";
    public const string AreaKeyRotated = "area-key-rotated";
    public const string SharedRead = "shared-read";
    public const string SharedDocumentRead = "shared-document-read";

    // Notfallzugang (M2.4)
    public const string Lifesign = "lifesign";
    public const string LifesignRequested = "lifesign-requested";
    public const string EmergencyWaiting = "emergency-waiting";
    public const string EmergencyReleased = "emergency-released";
    public const string EmergencySettingsChanged = "emergency-settings-changed";
    public const string AccessRequested = "access-requested";
    public const string AccessRequestPaused = "access-request-paused";
    public const string AccessRequestContinued = "access-request-continued";
    public const string AccessRequestObjected = "access-request-objected";
    public const string AccessRequestWithdrawn = "access-request-withdrawn";

    // Rücknahme und gemeinsames Löschen (M2.6)
    public const string EmergencyRevoked = "emergency-revoked";
    public const string LetterKeyRotated = "letter-key-rotated";
    public const string VaultDeletionAgreed = "vault-deletion-agreed";
    public const string VaultDeletionWithdrawn = "vault-deletion-withdrawn";
    public const string VaultDeletedByTrustees = "vault-deleted-by-trustees";

    // Erinnerungen (M3.1)
    public const string ReminderSent = "reminder-sent";
    public const string ReminderSettingsChanged = "reminder-settings-changed";

    // Checkliste (M3.2)
    public const string ChecklistDone = "checklist-done";
    public const string ChecklistUndone = "checklist-undone";

    // Server-Sicherung (M3.4), im System-Protokoll
    public const string AdminBackupCreated = "admin-backup-created";
    public const string AdminBackupFailed = "admin-backup-failed";

    /// <summary>Gehört ins System-Protokoll des Administrators.</summary>
    public static bool IsSystem(string action) =>
        action.StartsWith("admin-", StringComparison.Ordinal) || action is AccountDeleted or MailFailed or AccountCreated or KeyMismatch or EmergencyReleased or EmergencyRevoked
            or VaultDeletedByTrustees;

    /// <summary>Was der Browser selbst melden darf.</summary>
    // Brücke zu myDocs (M5)
    public const string MyDocsConnected = "mydocs-connected";
    public const string MyDocsDisconnected = "mydocs-disconnected";
    public const string MyDocsSynced = "mydocs-synced";

    public static IReadOnlySet<string> ClientReported { get; } = new HashSet<string>(StringComparer.Ordinal) { ExportJson, ExportPdf, ExportPrint, ExportPackage, ImportJson, MyDocsSynced };
}

/// <summary>Verbindung zu myDocs (M5, 167): nur Chiffrat, mit dem Tresorschlüssel verschlüsselt; null ohne Verbindung.</summary>
public sealed record MyDocsBridgeDto(byte[]? Cipher);

public sealed record MyDocsBridgeRequest(byte[] Cipher);
