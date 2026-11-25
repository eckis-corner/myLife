namespace MyLife.Server.Data;

/// <summary>
/// Erinnerungen eines Nutzers (Tabelle <c>reminder_settings</c>, M3.1). Vom Inhalt kennt der Server nur
/// <see cref="NextDueOn"/>: das früheste Datum, an dem ein Ablaufdatum (mit Vorlauf) oder eine Wiedervorlage ansteht,
/// ohne zu wissen, wozu es gehört (Entscheidung 39b).
/// </summary>
public sealed class ReminderSetting
{
    public Guid UserId { get; set; }

    /// <summary>Prüfintervall der Bereiche in Monaten; 0 = keine Erinnerung zum Prüfen.</summary>
    public int ReviewMonths { get; set; }

    /// <summary>So viele Tage vor einem Ablaufdatum wird erinnert.</summary>
    public int LeadDays { get; set; }

    public bool MailEnabled { get; set; }

    public DateOnly? NextDueOn { get; set; }

    /// <summary>Für diesen Termin ist die Mail schon verschickt; erst ein neuer Termin löst die nächste aus.</summary>
    public DateOnly? NotifiedDueOn { get; set; }

    public DateTimeOffset? LastReviewMailAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
