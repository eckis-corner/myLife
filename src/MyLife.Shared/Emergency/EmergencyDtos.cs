using MyLife.Shared.Admin;
using MyLife.Shared.Trust;

namespace MyLife.Shared.Emergency;

/// <summary>Zustand des Notfallzugangs eines Eigentümers (Architektur 5.1).</summary>
public enum EmergencyState
{
    /// <summary>Lebenszeichen rechtzeitig, alles normal.</summary>
    Active = 0,

    /// <summary>Intervall abgelaufen; Erinnerungen laufen.</summary>
    Overdue = 1,

    /// <summary>Alle Erinnerungen ohne Reaktion; die Wartezeit bis zur Freigabe läuft.</summary>
    Waiting = 2,

    /// <summary>Mindestens ein Modus ist freigegeben.</summary>
    Released = 3,
}

/// <summary>Freigegebene Modi als Bitmaske.</summary>
[Flags]
public enum ReleasedModes
{
    None = 0,
    Death = 1,
    Incapacity = 2,
}

public enum RequestState
{
    /// <summary>Läuft; die Frist beginnt mit dem Versand der Mail an den Eigentümer.</summary>
    Pending = 0,

    /// <summary>Über den Link in der Mail angehalten; der Eigentümer entscheidet nach der Anmeldung.</summary>
    Paused = 1,

    /// <summary>Der Eigentümer hat endgültig widersprochen.</summary>
    Objected = 2,

    /// <summary>Von der Vertrauensperson zurückgezogen.</summary>
    Withdrawn = 3,

    /// <summary>Frist abgelaufen, der Modus ist freigegeben.</summary>
    Released = 4,
}

public sealed record AccessRequestDto(
    Guid Id, Guid OwnerId, string OwnerName, Guid TrusteeId, string TrusteeName, GrantMode Mode, RequestState State,
    DateTimeOffset RequestedAt, DateTimeOffset? DeadlineAt, DateTimeOffset? PausedAt, DateTimeOffset? DecidedAt);

/// <summary>Eigene Einstellungen innerhalb der Grenzen des Admins (Entscheidung 43).</summary>
public sealed record EmergencySettingsDto(int IntervalDays, int WaitingDays, int RequestWaitingDays, string? SecondEmail, string? NtfyTopic);

/// <summary>Notfallzugang aus Sicht des Eigentümers.</summary>
public sealed record EmergencyStatusDto(
    bool Enabled,
    EmergencyState State,
    DateTimeOffset LastLifesignAt,
    DateTimeOffset? OverdueAt,
    DateTimeOffset? NextReminderAt,
    DateTimeOffset? ReleaseEarliestAt,
    ReleasedModes ReleasedModes,
    DateTimeOffset? ReleasedAt,
    int RemindersSent,
    bool MailDelayed,
    EmergencySettingsDto Settings,
    DeadlineSettingsDto Limits,
    bool NtfyAvailable,
    IReadOnlyList<AccessRequestDto> Requests);

/// <summary>Ein Eigentümer, für den ich Vertrauensperson bin.</summary>
public sealed record TrustedOwnerDto(
    Guid OwnerId, string OwnerName, bool CanRequestAccess, bool HasDeathGrants, bool HasIncapacityGrants, ReleasedModes ReleasedModes,
    IReadOnlyList<AccessRequestDto> Requests);

public sealed record CreateAccessRequest(GrantMode Mode);

public sealed record EmergencyTokenRequest(string Token);

/// <summary>Antwort auf einen Link aus einer Mail (Lebenszeichen oder Widerspruch).</summary>
public sealed record EmergencyLinkResultDto(string OwnerName, string? TrusteeName, GrantMode? Mode);

public static class EmergencyErrors
{
    public const string NotFound = "emergency-not-found";
    public const string Invalid = "emergency-invalid";
    public const string NotAllowed = "emergency-not-allowed";
    public const string Pending = "emergency-pending";
    public const string AlreadyReleased = "emergency-already-released";
    public const string TokenInvalid = "emergency-token-invalid";
    public const string Failed = "emergency-failed";
}
