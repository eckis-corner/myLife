using Microsoft.EntityFrameworkCore;
using MyLife.Server.Data;
using MyLife.Server.Infrastructure.Admin;
using MyLife.Server.Infrastructure.Emergency;
using MyLife.Server.Infrastructure.Mail;
using MyLife.Shared.Admin;
using MyLife.Shared.Emergency;
using MyLife.Shared.Reminders;
using MyLife.Shared.Vault;

namespace MyLife.Server.Infrastructure.Reminders;

/// <summary>
/// Erinnerungen (M3.1, PROMPT „Erinnerungen“, Entscheidung 39): eine Mail, wenn Bereiche wieder geprüft werden
/// sollten oder der vom Browser gemeldete nächste Termin erreicht ist. Die Mail nennt keine Inhalte; was ansteht,
/// zeigt die Übersicht nach der Anmeldung.
/// </summary>
internal sealed partial class ReminderEngine(
    MyLifeDbContext db, MailOutbox outbox, SettingsStore settings, KnownBaseUrl baseUrl, TimeProvider time, ILogger<ReminderEngine> logger)
{
    /// <summary>
    /// Erinnerungen des Nutzers; legt sie mit den Standardwerten an und speichert sofort. Fragen zwei Anfragen
    /// gleichzeitig zum ersten Mal, gewinnt eine; die andere liest danach deren Zeile.
    /// </summary>
    public async Task<ReminderSetting> EnsureAsync(Guid userId, CancellationToken cancellationToken)
    {
        var existing = await db.ReminderSettings.SingleOrDefaultAsync(r => r.UserId == userId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var defaults = ReminderSettingsDto.Default;
        var created = new ReminderSetting
        {
            UserId = userId, ReviewMonths = defaults.ReviewMonths, LeadDays = defaults.LeadDays, MailEnabled = defaults.MailEnabled, UpdatedAt = time.GetUtcNow(),
        };
        db.ReminderSettings.Add(created);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return created;
        }
        catch (DbUpdateException exception) when (exception.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
        {
            db.Entry(created).State = EntityState.Detached;
            return await db.ReminderSettings.SingleAsync(r => r.UserId == userId, cancellationToken);
        }
    }

    /// <summary>
    /// Bereiche mit aktiven Einträgen, die länger als das Prüfintervall nicht als geprüft markiert wurden (seit dem
    /// Anlegen des Bereichs, wenn noch nie). Das weiß der Server ohnehin (Entscheidung 67).
    /// </summary>
    public async Task<int> DueAreasAsync(Guid userId, int reviewMonths, CancellationToken cancellationToken)
    {
        if (reviewMonths <= 0)
        {
            return 0;
        }

        var limit = time.GetUtcNow().AddMonths(-reviewMonths);
        return await db.Areas.CountAsync(a => a.OwnerId == userId && (a.LastReviewedAt ?? a.CreatedAt) < limit
            && db.Objects.Any(o => o.OwnerId == userId && o.AreaKey == a.AreaKey && !o.IsDeleted && o.Status == ObjectStatus.Active), cancellationToken);
    }

    /// <summary>Ein Durchlauf für alle aktiven Nutzer; höchstens eine Mail je Nutzer und Durchlauf.</summary>
    public async Task<int> ProcessAsync(CancellationToken cancellationToken)
    {
        var sent = 0;
        var now = time.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        foreach (var user in await db.Users.Where(u => u.Status == UserStatus.Active).ToListAsync(cancellationToken))
        {
            var reminder = await EnsureAsync(user.Id, cancellationToken);
            // Nach der Freigabe „verstorben“ wird niemand mehr erinnert.
            var deceased = await db.EmergencySwitches.AnyAsync(
                s => s.OwnerId == user.Id && ((int)s.ReleasedModes & (int)ReleasedModes.Death) != 0, cancellationToken);
            if (!reminder.MailEnabled || deceased)
            {
                await db.SaveChangesAsync(cancellationToken);
                continue;
            }

            var dates = reminder.NextDueOn is { } due && due <= today && reminder.NotifiedDueOn != due;
            var dueAreas = await DueAreasAsync(user.Id, reminder.ReviewMonths, cancellationToken);
            var review = dueAreas > 0 && (reminder.LastReviewMailAt is not { } last || last.AddDays(ReminderLimits.ReviewMailEveryDays) <= now);
            if (dates || review)
            {
                var lines = new List<string>();
                if (review)
                {
                    lines.Add(MailTemplates.Phrase("Mail_reminder_review", user.Language)
                        .Replace("{count}", dueAreas.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
                        .Replace("{months}", reminder.ReviewMonths.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal));
                    reminder.LastReviewMailAt = now;
                }

                if (dates)
                {
                    lines.Add(MailTemplates.Phrase("Mail_reminder_dates", user.Language));
                    reminder.NotifiedDueOn = reminder.NextDueOn;
                }

                outbox.Enqueue(user, MailTemplate.Reminder, new Dictionary<string, string>
                {
                    ["items"] = string.Join("\n", lines.Select(l => $"- {l}")),
                    ["link"] = (await settings.GetAsync(cancellationToken)).PublicUrl ?? baseUrl.Value ?? string.Empty,
                });
                db.AuditLog.Add(AuditLog.Entry(AuditActions.ReminderSent, null, user.Id, now, null,
                    details: string.Join(",", new[] { review ? "review" : null, dates ? "dates" : null }.OfType<string>())));
                LogSent(logger, user.Id, review, dates);
                sent++;
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        return sent;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Erinnerung an {UserId} (prüfen: {Review}, Termine: {Dates})")]
    private static partial void LogSent(ILogger logger, Guid userId, bool review, bool dates);
}

/// <summary>Prüft stündlich und nach jedem Start, ob Erinnerungen fällig sind.</summary>
internal sealed partial class ReminderService(IServiceScopeFactory scopes, TimeProvider time, ILogger<ReminderService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);
    private readonly SemaphoreSlim _gate = new(1, 1);

    public override void Dispose()
    {
        _gate.Dispose();
        base.Dispose();
    }

    public async Task<int> ProcessOnceAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<ReminderEngine>().ProcessAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOnceAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is DbUpdateException or InvalidOperationException or Npgsql.NpgsqlException)
            {
                LogFailed(logger, exception);
            }

            try
            {
                await Task.Delay(Interval, time, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Erinnerungen konnten nicht geprüft werden")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
