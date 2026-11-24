namespace MyLife.Shared.Admin;

/// <summary>Verschlüsselung der Verbindung zum SMTP-Server.</summary>
public enum SmtpSecurity
{
    /// <summary>STARTTLS, wenn der Server es anbietet (Port 587).</summary>
    StartTls = 0,

    /// <summary>TLS von Anfang an (Port 465).</summary>
    SslOnConnect = 1,

    /// <summary>Unverschlüsselt, nur im eigenen Netz sinnvoll.</summary>
    None = 2,
}

/// <summary>
/// SMTP-Zugang. Das Passwort verlässt den Server nie: Lesen liefert nur <see cref="HasPassword"/>; beim Speichern
/// bedeutet <see cref="Password"/> = null „unverändert“ und eine leere Zeichenkette „entfernen“.
/// </summary>
public sealed record SmtpSettingsDto
{
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 587;
    public SmtpSecurity Security { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
    public bool HasPassword { get; init; }
    public string FromAddress { get; init; } = string.Empty;
    public string FromName { get; init; } = "myLife";
}

/// <summary>ntfy-Server für Push-Nachrichten. Das Zugriffstoken behandelt der Server wie das SMTP-Passwort.</summary>
public sealed record NtfySettingsDto
{
    public string? ServerUrl { get; init; }
    public string? AccessToken { get; init; }
    public bool HasAccessToken { get; init; }
}

/// <summary>Standardwert und Grenzen einer Frist in Tagen (Entscheidung 43).</summary>
public sealed record DayRange(int Default, int Min, int Max);

/// <summary>Fristen des Notfallzugangs (Architektur 5.1). Die Nutzer wählen ihre Werte ab M2.4 innerhalb der Grenzen.</summary>
public sealed record DeadlineSettingsDto
{
    /// <summary>Abstand der Lebenszeichen.</summary>
    public DayRange LifesignIntervalDays { get; init; } = new(30, 7, 180);

    /// <summary>Erinnerungen, Tage nach Ablauf des Intervalls (aufsteigend).</summary>
    public IReadOnlyList<int> ReminderDays { get; init; } = [3, 7, 14];

    /// <summary>Wartezeit nach der letzten Erinnerung bis zur Freigabe.</summary>
    public DayRange WaitingDays { get; init; } = new(14, 7, 60);

    /// <summary>Frist zwischen Antrag einer Vertrauensperson und Freigabe.</summary>
    public DayRange RequestWaitingDays { get; init; } = new(7, 3, 30);
}

/// <summary>Globale Einstellungen der Instanz, gepflegt vom Administrator (Architektur 2, Tabelle settings).</summary>
public sealed record AdminSettingsDto
{
    /// <summary>Adresse der Instanz für Links in Mails, etwa https://mylife.example.org.</summary>
    public string? PublicUrl { get; init; }

    public SmtpSettingsDto Smtp { get; init; } = new();
    public NtfySettingsDto Ntfy { get; init; } = new();
    public DeadlineSettingsDto Deadlines { get; init; } = new();
    public int IdleTimeoutMinutes { get; init; } = 15;
    public int AuditRetentionDays { get; init; } = 365;

    /// <summary>Text der Datenschutzseite; leer bedeutet den mitgelieferten Standardtext.</summary>
    public string? PrivacyNotice { get; init; }
}

/// <summary>Feste Grenzen, innerhalb derer der Administrator Werte wählt.</summary>
public static class AdminLimits
{
    public const int LifesignMinDays = 7;
    public const int LifesignMaxDays = 365;
    public const int WaitingMinDays = 3;
    public const int WaitingMaxDays = 90;
    public const int RequestWaitingMinDays = 1;
    public const int RequestWaitingMaxDays = 60;
    public const int ReminderMinDays = 1;
    public const int ReminderMaxDays = 60;
    public const int MaxReminders = 5;
    public const int IdleMinMinutes = 5;
    public const int IdleMaxMinutes = 240;
    public const int RetentionMinDays = 30;
    public const int RetentionMaxDays = 3650;
    public const int PrivacyMaxLength = 20000;
    public const int DeletionLinkDays = 7;
}

/// <summary>Zustand der Mail-Warteschlange für den Administrator.</summary>
public sealed record MailQueueDto(int Pending, int Failed, DateTimeOffset? LastSentAt, string? LastError, DateTimeOffset? LastErrorAt);

/// <summary>Ergebnis eines Versandtests (SMTP oder ntfy); <see cref="Error"/> ist die Meldung der Gegenstelle.</summary>
public sealed record SendTestResultDto(bool Success, string? Error);

public sealed record NtfyTestRequest(string Topic);

public enum AccountStatus
{
    Invited = 0,
    Active = 1,
    Locked = 2,
}

/// <summary>Ein Konto in der Benutzerverwaltung: nur Metadaten, nie Tresorinhalte.</summary>
public sealed record AdminUserDto(
    Guid Id,
    string Email,
    string DisplayName,
    bool IsAdmin,
    AccountStatus Status,
    bool HasTotp,
    int PasskeyCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt,
    bool DeletionPending,
    bool IsMe);

public sealed record SetAdminRequest(bool IsAdmin);

/// <summary>Angaben auf der Seite „Konto löschen“, die der Link aus der Mail öffnet.</summary>
public sealed record AccountDeletionDto(string DisplayName, string Email, DateTimeOffset ExpiresAt);

public sealed record AccountDeletionRequest(string Token);

/// <summary>Text der Datenschutzseite. <see cref="IsDefault"/>: Der Admin hat keinen eigenen Text hinterlegt.</summary>
public sealed record PrivacyNoticeDto(string? Text, bool IsDefault);

/// <summary>Feste Fehlercodes der Verwaltung (Problem-Titel), im Client als Ressourcenschlüssel AdminError_&lt;code&gt;.</summary>
public static class AdminErrors
{
    public const string NotFound = "admin-not-found";
    public const string Self = "admin-self";
    public const string LastAdmin = "admin-last-admin";
    public const string MailNotConfigured = "admin-mail-not-configured";
    public const string Invalid = "admin-invalid";
    public const string TokenInvalid = "admin-token-invalid";
    public const string Failed = "admin-failed";
}

/// <summary>Fehlercodes bei der Prüfung der Einstellungen (Werte im ValidationProblem je Feld).</summary>
public static class SettingsErrors
{
    public const string Required = "required";
    public const string Range = "range";
    public const string Order = "order";
    public const string Email = "email";
    public const string Url = "url";
    public const string TooLong = "too-long";
}
