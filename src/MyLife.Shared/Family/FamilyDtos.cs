using MyLife.Shared.Setup;

namespace MyLife.Shared.Family;

/// <summary>
/// Neue Einladung (M2.2, Architektur 4.3). Die ID vergibt der Browser, weil sie im Kontext des verschlüsselten
/// Geheimnisses steckt. <see cref="MailFragment"/> (Geheimnis und eigener Fingerprint) schickt der Browser nur mit,
/// wenn der Server die Mail verschicken soll; sonst baut der Browser den Link selbst.
/// </summary>
public sealed record CreateInvitationRequest(Guid Id, string Email, string Language, byte[] EncryptedSecret, string? MailFragment);

/// <summary>Angelegt; <see cref="Token"/> gehört in den Link (<c>/invite#t=…</c>).</summary>
public sealed record InvitationCreatedDto(Guid Id, string Token, DateTimeOffset ExpiresAt, bool MailQueued);

/// <summary>Link neu erzeugen und optional erneut per Mail schicken; der alte Link wird ungültig.</summary>
public sealed record RenewInvitationRequest(string? MailFragment);

public enum InvitationStatus
{
    Pending = 0,
    Accepted = 1,
    Verified = 2,
    Mismatch = 3,
    Expired = 4,
    Revoked = 5,
}

public sealed record InvitationDto(
    Guid Id,
    string Email,
    InvitationStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? AcceptedAt,
    Guid? InviteeId,
    string? InviteeName,
    string InviterName,
    byte[] EncryptedSecret);

public sealed record InvitationTokenRequest(string Token);

/// <summary>Was die eingeladene Person vor dem Anlegen sieht.</summary>
public sealed record InvitationInfoDto(Guid Id, Guid InviterId, string InviterName, string Email, string Language, DateTimeOffset ExpiresAt);

public sealed record InviteTotpVerifyRequest(string Token, string Enrollment, string Code);

/// <summary>
/// Einladung annehmen: dasselbe Schlüsselmaterial wie bei der Einrichtung (Token = Einladungs-Token), dazu die
/// Bindung HMAC(Geheimnis, Fingerprint) und der gepinnte Fingerprint der einladenden Person (verschlüsselt).
/// </summary>
public sealed record AcceptInvitationRequest(SetupCompleteRequest Account, byte[] Binding, byte[]? InviterPin);

/// <summary>Angenommene Einladung, deren Bindung der Browser der einladenden Person noch prüfen muss.</summary>
public sealed record PendingVerificationDto(
    Guid InvitationId, Guid InviteeId, string InviteeName, byte[] PublicEncryptionKey, byte[] PublicSigningKey, byte[] Binding, byte[] EncryptedSecret);

/// <summary>Ergebnis der Prüfung: bei Erfolg Gegenzeichnung und Pin, sonst nur die Meldung.</summary>
public sealed record VerifyInvitationRequest(bool Matches, byte[]? Endorsement, byte[]? Pin);

public sealed record EndorsementDto(Guid SignerId, byte[] Fingerprint, byte[] Signature, DateTimeOffset CreatedAt);

/// <summary>Eine andere Person der Instanz mit ihren aktuellen öffentlichen Schlüsseln und allen Gegenzeichnungen.</summary>
public sealed record PersonDto(
    Guid Id, string DisplayName, string Email, byte[] PublicEncryptionKey, byte[] PublicSigningKey, IReadOnlyList<EndorsementDto> Endorsements);

public sealed record PinDto(Guid SubjectId, byte[] EncryptedPin);

public sealed record PinRequest(byte[] EncryptedPin);

public sealed record EndorseRequest(byte[] Signature);

public static class FamilyLimits
{
    public const int InvitationDays = 14;
    public const int SecretBytes = 32;
    public const int MaxBlobBytes = 256;
}

/// <summary>Feste Fehlercodes (Problem-Titel), im Client als Ressourcenschlüssel FamilyError_&lt;code&gt;.</summary>
public static class FamilyErrors
{
    public const string AccountExists = "family-account-exists";
    public const string Pending = "family-pending";
    public const string Invalid = "family-invalid";
    public const string TokenInvalid = "family-token-invalid";
    public const string NotFound = "family-not-found";
    public const string MailNotConfigured = "family-mail-not-configured";
    public const string Fingerprint = "family-fingerprint";
    public const string TotpExpired = "family-totp-expired";
    public const string TotpInvalid = "family-totp-invalid";
    public const string Failed = "family-failed";
}
