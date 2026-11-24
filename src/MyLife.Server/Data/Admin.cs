namespace MyLife.Server.Data;

/// <summary>
/// Eine Mail in der Warteschlange (Tabelle <c>mail_outbox</c>). Alle Mails gehen über die Outbox, damit ein
/// SMTP-Ausfall nichts verliert. Die Parameter (etwa Links mit Tokens) liegen mit Data Protection verschlüsselt vor
/// und werden nach dem Versand gelöscht.
/// </summary>
public sealed class MailOutboxEntry
{
    public Guid Id { get; set; }

    public required string Recipient { get; set; }

    /// <summary>Schlüssel der Vorlage, etwa <c>account-locked</c>.</summary>
    public required string Template { get; set; }

    public required string Language { get; set; }

    /// <summary>Parameter der Vorlage als JSON, mit Data Protection geschützt; nach dem Versand null.</summary>
    public string? Parameters { get; set; }

    public int Attempts { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset NextAttemptAt { get; set; }

    public DateTimeOffset? SentAt { get; set; }

    /// <summary>Endgültig aufgegeben nach zu vielen Versuchen.</summary>
    public DateTimeOffset? FailedAt { get; set; }

    public string? LastError { get; set; }
}

/// <summary>Zweck eines Einmal-Tokens. Werte nie umnummerieren.</summary>
public enum TokenPurpose
{
    AccountDeletion = 0,
}

/// <summary>
/// Einmal-Token für Links in Mails (Tabelle <c>one_time_tokens</c>): 256 Bit, nur der SHA-256-Hash wird
/// gespeichert, einmal verwendbar und zeitlich begrenzt (Architektur 5.1).
/// </summary>
public sealed class OneTimeToken
{
    public Guid Id { get; set; }

    public TokenPurpose Purpose { get; set; }

    public required byte[] TokenHash { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Wer das Token ausgelöst hat (bei der Kontolöschung der Admin).</summary>
    public Guid? CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? UsedAt { get; set; }
}
