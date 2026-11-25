using System.Net.Mail;

namespace MyLife.Shared.Admin;

/// <summary>
/// Prüft die globalen Einstellungen, gleich in Browser und Server. Ergebnis: Feld → Fehlercode aus
/// <see cref="SettingsErrors"/>; leer, wenn alles stimmt.
/// </summary>
public static class AdminSettingsValidation
{
    public static Dictionary<string, string> Validate(AdminSettingsDto settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var errors = new Dictionary<string, string>(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(settings.PublicUrl) && !IsHttpUrl(settings.PublicUrl))
        {
            errors["publicUrl"] = SettingsErrors.Url;
        }

        var smtp = settings.Smtp ?? new SmtpSettingsDto();
        if (!string.IsNullOrWhiteSpace(smtp.Host))
        {
            if (smtp.Host.Trim().Length > 253 || smtp.Host.Trim().Contains(' ', StringComparison.Ordinal))
            {
                errors["smtp.host"] = SettingsErrors.Url;
            }

            if (smtp.Port is < 1 or > 65535)
            {
                errors["smtp.port"] = SettingsErrors.Range;
            }

            if (string.IsNullOrWhiteSpace(smtp.FromAddress))
            {
                errors["smtp.fromAddress"] = SettingsErrors.Required;
            }
            else if (!IsEmail(smtp.FromAddress))
            {
                errors["smtp.fromAddress"] = SettingsErrors.Email;
            }

            if (smtp.FromName is { Length: > 100 } || smtp.Username is { Length: > 254 } || smtp.Password is { Length: > 500 })
            {
                errors["smtp.fromName"] = SettingsErrors.TooLong;
            }
        }

        var ntfy = settings.Ntfy ?? new NtfySettingsDto();
        if (!string.IsNullOrWhiteSpace(ntfy.ServerUrl) && !IsHttpUrl(ntfy.ServerUrl))
        {
            errors["ntfy.serverUrl"] = SettingsErrors.Url;
        }

        if (ntfy.AccessToken is { Length: > 500 })
        {
            errors["ntfy.accessToken"] = SettingsErrors.TooLong;
        }

        var deadlines = settings.Deadlines ?? new DeadlineSettingsDto();
        CheckRange(errors, "deadlines.lifesign", deadlines.LifesignIntervalDays, AdminLimits.LifesignMinDays, AdminLimits.LifesignMaxDays);
        CheckRange(errors, "deadlines.waiting", deadlines.WaitingDays, AdminLimits.WaitingMinDays, AdminLimits.WaitingMaxDays);
        CheckRange(errors, "deadlines.request", deadlines.RequestWaitingDays, AdminLimits.RequestWaitingMinDays, AdminLimits.RequestWaitingMaxDays);

        var reminders = deadlines.ReminderDays ?? [];
        if (reminders.Count is 0 or > AdminLimits.MaxReminders
            || reminders.Any(d => d is < AdminLimits.ReminderMinDays or > AdminLimits.ReminderMaxDays))
        {
            errors["deadlines.reminders"] = SettingsErrors.Range;
        }
        else if (reminders.Zip(reminders.Skip(1)).Any(pair => pair.Second <= pair.First))
        {
            errors["deadlines.reminders"] = SettingsErrors.Order;
        }

        if (settings.IdleTimeoutMinutes is < AdminLimits.IdleMinMinutes or > AdminLimits.IdleMaxMinutes)
        {
            errors["idleTimeout"] = SettingsErrors.Range;
        }

        if (settings.AuditRetentionDays is < AdminLimits.RetentionMinDays or > AdminLimits.RetentionMaxDays)
        {
            errors["retention"] = SettingsErrors.Range;
        }

        var backup = settings.Backup ?? new BackupSettingsDto();
        if (backup.Hour is < 0 or > 23)
        {
            errors["backup.hour"] = SettingsErrors.Range;
        }

        if (backup.KeepDaily is < 1 or > AdminLimits.BackupMaxDaily || backup.KeepWeekly is < 0 or > AdminLimits.BackupMaxWeekly
            || backup.KeepMonthly is < 0 or > AdminLimits.BackupMaxMonthly)
        {
            errors["backup.keep"] = SettingsErrors.Range;
        }

        if (settings.PrivacyNotice is { Length: > AdminLimits.PrivacyMaxLength })
        {
            errors["privacy"] = SettingsErrors.TooLong;
        }

        return errors;
    }

    /// <summary>Erinnerungen als Text „3, 7, 14“ lesen; null bei ungültiger Eingabe.</summary>
    public static IReadOnlyList<int>? ParseReminders(string? text)
    {
        var parts = (text ?? string.Empty).Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var days = new List<int>();
        foreach (var part in parts)
        {
            if (!int.TryParse(part, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var day))
            {
                return null;
            }

            days.Add(day);
        }

        return days;
    }

    public static bool IsEmail(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 254 && MailAddress.TryCreate(value.Trim(), out var address) && address.Address == value.Trim();

    public static bool IsHttpUrl(string? value) =>
        Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
        && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);

    private static void CheckRange(Dictionary<string, string> errors, string key, DayRange? range, int hardMin, int hardMax)
    {
        if (range is null || range.Min < hardMin || range.Max > hardMax || range.Min > range.Default || range.Default > range.Max)
        {
            errors[key] = SettingsErrors.Range;
        }
    }
}
