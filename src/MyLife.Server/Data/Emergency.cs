using MyLife.Shared.Emergency;
using MyLife.Shared.Trust;

namespace MyLife.Server.Data;

/// <summary>
/// Notfallzugang eines Eigentümers (Tabelle <c>emergency_switches</c>, Architektur 5.1). Der Zustand übersteht
/// Neustarts; der Dienst setzt bei jedem Lauf dort fort, wo er steht. Jede Stufe zählt ab dem tatsächlichen
/// Versand ihrer Mail, damit ein Mailausfall nie zu einer Freigabe führt.
/// </summary>
public sealed class EmergencySwitch
{
    public Guid OwnerId { get; set; }

    public int IntervalDays { get; set; }

    public int WaitingDays { get; set; }

    public int RequestWaitingDays { get; set; }

    /// <summary>Optionale zweite Adresse für Erinnerungen (Entscheidung 13).</summary>
    public string? SecondEmail { get; set; }

    /// <summary>Optionales ntfy-Thema für Erinnerungen und Anträge.</summary>
    public string? NtfyTopic { get; set; }

    public DateTimeOffset LastLifesignAt { get; set; }

    public EmergencyState State { get; set; }

    /// <summary>Seit wann überfällig (Beginn der Erinnerungen).</summary>
    public DateTimeOffset? OverdueAt { get; set; }

    /// <summary>Zahl der verschickten Mails dieser Runde: 0 = noch keine, 1 = Lebenszeichen fällig, danach je Erinnerung eine mehr.</summary>
    public int Stage { get; set; }

    /// <summary>Mail der aktuellen Stufe in der Outbox.</summary>
    public Guid? StageMailId { get; set; }

    /// <summary>Wann die Mail der aktuellen Stufe tatsächlich verschickt wurde; erst dann läuft die nächste Frist.</summary>
    public DateTimeOffset? StageSentAt { get; set; }

    public ReleasedModes ReleasedModes { get; set; }

    public DateTimeOffset? ReleasedAt { get; set; }

    /// <summary>Bereiche, die nach einer Rücknahme noch einen neuen Schlüssel brauchen (M2.6).</summary>
    public int[] RekeyAreas { get; set; } = [];

    /// <summary>Briefe, die nach einer Rücknahme noch einen neuen Schlüssel brauchen (M2.6).</summary>
    public Guid[] RekeyLetters { get; set; } = [];

    /// <summary>Alle Vertrauenspersonen haben dem endgültigen Löschen zugestimmt; dann wird zu diesem Zeitpunkt gelöscht.</summary>
    public DateTimeOffset? DeletionScheduledAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public uint Version { get; set; }
}

/// <summary>Antrag einer Vertrauensperson auf einen Modus (Tabelle <c>access_requests</c>, Entscheidungen 14, 15, 33).</summary>
public sealed class AccessRequest
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public Guid TrusteeId { get; set; }

    public GrantMode Mode { get; set; }

    public RequestState State { get; set; }

    public DateTimeOffset RequestedAt { get; set; }

    /// <summary>Mail an den Eigentümer mit dem Link „Widersprechen“; die Frist beginnt mit ihrem Versand.</summary>
    public Guid? OwnerMailId { get; set; }

    public DateTimeOffset? DeadlineAt { get; set; }

    public DateTimeOffset? PausedAt { get; set; }

    public DateTimeOffset? DecidedAt { get; set; }
}

/// <summary>Zustimmung einer Vertrauensperson zum endgültigen Löschen nach dem Tod (Tabelle <c>vault_deletion_votes</c>, Entscheidung 36).</summary>
public sealed class VaultDeletionVote
{
    public Guid OwnerId { get; set; }

    public Guid TrusteeId { get; set; }

    public DateTimeOffset AgreedAt { get; set; }
}
