namespace MyLife.Shared.Reminders;

/// <summary>
/// Eigene Erinnerungen (M3.1): Prüfintervall in Monaten (0 = aus), Vorlauf vor einem Ablaufdatum in Tagen und ob
/// Erinnerungen per Mail kommen.
/// </summary>
public sealed record ReminderSettingsDto(int ReviewMonths, int LeadDays, bool MailEnabled)
{
    public static ReminderSettingsDto Default { get; } = new(ReminderLimits.DefaultReviewMonths, ReminderLimits.DefaultLeadDays, true);

    public bool IsValid =>
        ReviewMonths is >= 0 and <= ReminderLimits.MaxReviewMonths && LeadDays is >= ReminderLimits.MinLeadDays and <= ReminderLimits.MaxLeadDays;
}

/// <summary>
/// Stand aus Sicht des Servers: die Einstellungen und der nächste Termin, den der Browser gemeldet hat. Der Server
/// kennt nur dieses Datum, nicht wozu es gehört (Entscheidung 39b).
/// </summary>
public sealed record ReminderStatusDto(ReminderSettingsDto Settings, DateOnly? NextDueOn);

/// <summary>Nächster Termin aus Ablaufdaten und Wiedervorlagen, im Browser berechnet; null = keiner.</summary>
public sealed record NextDueRequest(DateOnly? NextDueOn);

public static class ReminderLimits
{
    public const int DefaultReviewMonths = 6;
    public const int MaxReviewMonths = 24;
    public const int DefaultLeadDays = 60;
    public const int MinLeadDays = 7;
    public const int MaxLeadDays = 365;

    /// <summary>Solange Bereiche zur Prüfung fällig sind, kommt höchstens so oft eine Mail.</summary>
    public const int ReviewMailEveryDays = 30;
}

public static class ReminderErrors
{
    public const string Invalid = "reminder-invalid";
    public const string Failed = "reminder-failed";
}
