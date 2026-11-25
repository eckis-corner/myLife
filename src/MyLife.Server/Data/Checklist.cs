namespace MyLife.Server.Data;

/// <summary>
/// Gemeinsames Abhaken der Checkliste (Tabelle <c>checklist_progress</c>, M3.2, Entscheidung 21): Eine Aufgabe ist
/// erledigt, wenn hier eine Zeile steht, für alle Vertrauenspersonen mit Freigabe der Checkliste gleich. Der Server
/// kennt nur die ID der Aufgabe, wer sie wann abgehakt hat; den Inhalt der Aufgabe kennt er nicht.
/// </summary>
public sealed class ChecklistProgress
{
    public Guid TaskId { get; set; }

    public Guid OwnerId { get; set; }

    public Guid DoneById { get; set; }

    public DateTimeOffset DoneAt { get; set; }
}
