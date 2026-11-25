namespace MyLife.Shared.Checklist;

/// <summary>Erledigt-Markierung einer Aufgabe: wer und wann, gemeinsam für alle sichtbar (Entscheidung 21).</summary>
public sealed record ChecklistProgressDto(Guid TaskId, Guid DoneById, string DoneByName, DateTimeOffset DoneAt);

/// <summary>Eine Aufgabe der Vorlage für Deutschland: Phase und Regel für die verknüpften Einträge.</summary>
public sealed record ChecklistTemplateTask(string Id, string Phase, string? LinkedArea = null, string? LinkedWish = null);

/// <summary>
/// Vorlage der Checkliste für Deutschland (M3.2). Titel und Beschreibung stehen in den Ressourcen unter
/// <c>ChecklistTemplate_{Id}_title</c> und <c>_text</c>. Beim Übernehmen entstehen daraus Aufgaben im eigenen Tresor,
/// die der Eigentümer danach frei ändert. Keine Rechtsberatung; Fristen sind Richtwerte.
/// </summary>
public static class ChecklistTemplate
{
    public static IReadOnlyList<ChecklistTemplateTask> Tasks { get; } =
    [
        // Erste 24 Stunden
        new("doctor", "first24h"),
        new("family", "first24h", "contacts"),
        new("undertaker", "first24h", "wishes"),
        new("lifeInsurance", "first24h", "insurance", "claim"),
        new("home", "first24h", "property"),

        // Erste Woche
        new("documents", "firstWeek", "documents"),
        new("deathCertificate", "firstWeek", "aboutMe"),
        new("will", "firstWeek", "documents"),
        new("employer", "firstWeek", "income"),
        new("healthInsurance", "firstWeek", "insurance"),
        new("banks", "firstWeek", "banking"),
        new("passwordManager", "firstWeek", "passwordManager"),
        new("funeral", "firstWeek", "wishes"),

        // Erster Monat
        new("pension", "firstMonth", "income"),
        new("inheritance", "firstMonth", "documents"),
        new("cancelContracts", "firstMonth", "contracts", "cancel"),
        new("takeOverContracts", "firstMonth", "contracts", "takeOver"),
        new("cancelInsurance", "firstMonth", "insurance", "cancel"),
        new("cards", "firstMonth", "cards"),
        new("loans", "firstMonth", "loans"),

        // Später
        new("digital", "later", "digital"),
        new("property", "later", "property"),
        new("taxes", "later", "income"),
        new("grave", "later", "wishes"),
    ];
}

public static class ChecklistErrors
{
    public const string NotFound = "checklist-not-found";
    public const string Failed = "checklist-failed";
}
