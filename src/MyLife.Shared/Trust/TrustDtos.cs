using MyLife.Shared.Vault;

namespace MyLife.Shared.Trust;

/// <summary>Wann ein Umschlag ausgeliefert wird (Architektur 4.2, 46b).</summary>
public enum GrantMode
{
    /// <summary>Sofort teilen: der Server liefert sofort aus, nur Lesen.</summary>
    Live = 0,

    /// <summary>Nach Freigabe im Modus „verstorben“.</summary>
    Death = 1,

    /// <summary>Nach Freigabe im Modus „handlungsunfähig“.</summary>
    Incapacity = 2,
}

/// <summary>Was ein Umschlag öffnet: einen Bereichsschlüssel oder den Schlüssel eines einzelnen Briefs.</summary>
public enum GrantKind
{
    Area = 0,
    Letter = 1,
}

/// <summary>
/// Ein Umschlag für eine Vertrauensperson: der Schlüssel per ECIES an ihren öffentlichen Schlüssel versiegelt
/// und vom Eigentümer signiert. <see cref="Fingerprint"/> ist der Fingerprint der Person beim Versiegeln.
/// </summary>
public sealed record GrantRequest(GrantKind Kind, int? Area, Guid? ObjectId, GrantMode Mode, byte[] Envelope, byte[] Signature, byte[] Fingerprint);

/// <summary>Vertrauensperson anlegen oder ändern; die Umschläge ersetzen alle bisherigen dieser Person.</summary>
public sealed record SaveTrusteeRequest(byte[] EncryptedProfile, bool CanRequestAccess, IReadOnlyList<GrantRequest> Grants);

/// <summary>Ein gespeicherter Umschlag aus Sicht des Eigentümers (ohne Inhalt).</summary>
public sealed record GrantDto(Guid Id, GrantKind Kind, int? Area, Guid? ObjectId, GrantMode Mode, byte[] Fingerprint);

public sealed record TrusteeDto(Guid TrusteeId, byte[] EncryptedProfile, bool CanRequestAccess, DateTimeOffset CreatedAt, IReadOnlyList<GrantDto> Grants);

/// <summary>Beziehung und Notiz, verschlüsselt im Tresor des Eigentümers (Kontext <c>trustee-profile:{ID}</c>).</summary>
public sealed record TrusteeProfile(string Relationship, string? Note);

/// <summary>Neuer Bereichsschlüssel (46b): alle Objekte, Dokumente und Umschläge des Bereichs neu, in einem Schritt.</summary>
public sealed record RotateAreaRequest(
    byte[] WrappedAreaKey, IReadOnlyList<RotatedObject> Objects, IReadOnlyList<RotatedDocument> Documents, IReadOnlyList<RotatedGrant> Grants);

/// <summary>Objekt mit neuem Chiffrat; bei eigenem Objektschlüssel (Brief) nur dieser neu verpackt.</summary>
public sealed record RotatedObject(Guid Id, byte[] Payload, byte[]? WrappedKey, uint Version);

public sealed record RotatedDocument(Guid Id, byte[] WrappedFileKey, byte[] Metadata);

public sealed record RotatedGrant(Guid Id, byte[] Envelope, byte[] Signature);

/// <summary>Was ein Eigentümer mit mir teilt: Umschläge, die der Server schon ausliefert.</summary>
public sealed record SharedOwnerDto(Guid OwnerId, string OwnerName, IReadOnlyList<SharedGrantDto> Grants);

public sealed record SharedGrantDto(Guid Id, GrantKind Kind, int? Area, Guid? ObjectId, GrantMode Mode, byte[] Envelope, byte[] Signature);

/// <summary>Inhalt eines geteilten Bereichs: Chiffrat der Objekte und Dokument-Metadaten des Eigentümers.</summary>
public sealed record SharedContentDto(IReadOnlyList<VaultObjectDto> Objects, IReadOnlyList<DocumentDto> Documents);

/// <summary>Umschlag eines Bereichs mit Empfänger und Modus (für den Schlüsselwechsel im Browser).</summary>
public sealed record AreaGrantDto(Guid Id, Guid TrusteeId, GrantMode Mode);

public static class TrustLimits
{
    public const int MaxProfileBytes = 4096;
    public const int MaxEnvelopeBytes = 512;
    public const int MaxGrants = 200;
}

/// <summary>Feste Fehlercodes (Problem-Titel), im Client als Ressourcenschlüssel TrustError_&lt;code&gt;.</summary>
public static class TrustErrors
{
    public const string NotFound = "trust-not-found";
    public const string Invalid = "trust-invalid";
    public const string KeyChanged = "trust-key-changed";
    public const string Stale = "trust-stale";
    public const string Failed = "trust-failed";
}
