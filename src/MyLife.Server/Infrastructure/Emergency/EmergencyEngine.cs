using Microsoft.EntityFrameworkCore;
using MyLife.Server.Data;
using MyLife.Server.Infrastructure.Admin;
using MyLife.Server.Infrastructure.Mail;
using MyLife.Shared.Admin;
using MyLife.Shared.Emergency;
using MyLife.Shared.Trust;
using MyLife.Shared.Vault;

namespace MyLife.Server.Infrastructure.Emergency;

/// <summary>Adresse der Instanz für Links in Mails, die ohne Anfrage entstehen (Hintergrunddienst).</summary>
internal sealed class KnownBaseUrl
{
    public string? Value { get; private set; }

    public void Remember(HttpRequest request)
    {
        if (Value is null && request.Host.HasValue)
        {
            Value = $"{request.Scheme}://{request.Host}";
        }
    }
}

/// <summary>
/// Zustandsautomat des Notfallzugangs (Architektur 5.1). Jeder Schritt ist idempotent und wird gespeichert; nach
/// einem Neustart setzt der Dienst einfach fort. Zeiten laufen über <see cref="TimeProvider"/>.
/// <list type="bullet">
/// <item>Kein Lebenszeichen: Mail „Lebenszeichen fällig“, dann die Erinnerungen (Standard 3, 7, 14 Tage), dann
/// die Wartezeit; danach sind beide Modi freigegeben (Entscheidung 32).</item>
/// <item>Antrag: Frist ab Versand der Mail an den Eigentümer; danach ist der beantragte Modus frei.</item>
/// <item>Mailausfall: Jede Frist läuft erst ab dem tatsächlichen Versand der zugehörigen Mail. Fällt der Versand
/// aus, bleibt alles stehen; ein Mailausfall führt nie zu einer Freigabe.</item>
/// </list>
/// </summary>
internal sealed partial class EmergencyEngine(
    MyLifeDbContext db, MailOutbox outbox, SettingsStore settings, NtfyClient ntfy, KnownBaseUrl baseUrl, TimeProvider time, ILogger<EmergencyEngine> logger)
{
    private static readonly TimeSpan MailDelayThreshold = TimeSpan.FromHours(1);
    private static readonly TimeSpan LinkLifetime = TimeSpan.FromDays(120);

    /// <summary>Hat der Eigentümer Vertrauenspersonen? Ohne sie gibt es keinen Notfallzugang und keine Erinnerungen.</summary>
    public Task<bool> IsEnabledAsync(Guid ownerId, CancellationToken cancellationToken) =>
        db.TrustRelations.AnyAsync(r => r.OwnerId == ownerId, cancellationToken);

    /// <summary>Liefert den Notfallzugang des Eigentümers; legt ihn mit den Standardwerten des Admins an.</summary>
    public async Task<EmergencySwitch> EnsureAsync(User owner, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var existing = await db.EmergencySwitches.SingleOrDefaultAsync(s => s.OwnerId == owner.Id, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var deadlines = (await settings.GetAsync(cancellationToken)).Deadlines;
        var created = new EmergencySwitch
        {
            OwnerId = owner.Id,
            IntervalDays = deadlines.LifesignIntervalDays.Default,
            WaitingDays = deadlines.WaitingDays.Default,
            RequestWaitingDays = deadlines.RequestWaitingDays.Default,
            LastLifesignAt = owner.LastLoginAt ?? owner.CreatedAt,
            State = EmergencyState.Active,
            UpdatedAt = time.GetUtcNow(),
        };
        db.EmergencySwitches.Add(created);
        return created;
    }

    /// <summary>
    /// Lebenszeichen (Anmeldung, Knopf oder Link): beendet Erinnerungen und Wartezeit. Eine Freigabe hebt es nicht
    /// auf (Rücknahme mit M2.6), Anträge auch nicht (dafür gibt es den Widerspruch).
    /// </summary>
    public async Task LifesignAsync(User owner, string source, CancellationToken cancellationToken)
    {
        var state = await EnsureAsync(owner, cancellationToken);
        var now = time.GetUtcNow();
        state.LastLifesignAt = now;
        state.UpdatedAt = now;
        if (state.State is EmergencyState.Overdue or EmergencyState.Waiting)
        {
            state.State = EmergencyState.Active;
            state.OverdueAt = null;
            state.Stage = 0;
            state.StageMailId = null;
            state.StageSentAt = null;
        }

        await db.OneTimeTokens.Where(t => t.UserId == owner.Id && t.Purpose == TokenPurpose.Lifesign && t.UsedAt == null)
            .ExecuteUpdateAsync(t => t.SetProperty(x => x.UsedAt, now), cancellationToken);
        db.AuditLog.Add(AuditLog.Entry(AuditActions.Lifesign, owner.Id, owner.Id, now, null, details: source));
    }

    /// <summary>Ein Durchlauf für alle Eigentümer und Anträge; speichert nach jedem Eigentümer.</summary>
    public async Task<int> ProcessAsync(CancellationToken cancellationToken)
    {
        var changes = 0;
        var owners = await db.Users.Where(u => u.Status == UserStatus.Active && db.TrustRelations.Any(r => r.OwnerId == u.Id)).ToListAsync(cancellationToken);
        foreach (var owner in owners)
        {
            var state = await EnsureAsync(owner, cancellationToken);
            if (await StepAsync(owner, state, cancellationToken))
            {
                changes++;
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        foreach (var request in await db.AccessRequests.Where(r => r.State == RequestState.Pending).ToListAsync(cancellationToken))
        {
            if (await StepRequestAsync(request, cancellationToken))
            {
                changes++;
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        return changes;
    }

    /// <summary>
    /// Stockt der Versand? Die Stufe ist noch nicht zugestellt und eine Mail dieser Runde wartet seit über einer
    /// Stunde oder wurde aufgegeben (Hinweis im Dashboard, Architektur 5.1).
    /// </summary>
    public async Task<bool> IsMailDelayedAsync(EmergencySwitch state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.State is not (EmergencyState.Overdue or EmergencyState.Waiting) || state.StageSentAt is not null || state.OverdueAt is not { } since)
        {
            return false;
        }

        var limit = time.GetUtcNow() - MailDelayThreshold;
        var email = await db.Users.Where(u => u.Id == state.OwnerId).Select(u => u.Email).SingleAsync(cancellationToken);
        return await db.MailOutbox.AnyAsync(m => m.Recipient == email && m.CreatedAt >= since && m.SentAt == null
            && (m.Template == MailTemplate.LifesignDue || m.Template == MailTemplate.LifesignReminder)
            && (m.FailedAt != null || m.CreatedAt < limit), cancellationToken);
    }

    /// <summary>Erinnerungstage aus den Einstellungen des Admins (aufsteigend, Tage nach dem Fälligwerden).</summary>
    public async Task<IReadOnlyList<int>> ReminderDaysAsync(CancellationToken cancellationToken) =>
        (await settings.GetAsync(cancellationToken)).Deadlines.ReminderDays;

    /// <summary>Frühester Zeitpunkt der Freigabe, wenn ab jetzt jede Mail pünktlich ankommt und niemand reagiert.</summary>
    public static DateTimeOffset? EarliestRelease(EmergencySwitch state, IReadOnlyList<int> reminders, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(reminders);
        var last = reminders.Count == 0 ? 0 : reminders[^1];
        return state.State switch
        {
            EmergencyState.Active => state.LastLifesignAt.AddDays(state.IntervalDays + last + state.WaitingDays),
            EmergencyState.Overdue => (state.OverdueAt ?? now).AddDays(last + state.WaitingDays),
            EmergencyState.Waiting => (state.StageSentAt ?? now).AddDays(state.WaitingDays),
            _ => null,
        };
    }

    /// <summary>Wann die nächste Erinnerung fällig ist (nur zur Anzeige).</summary>
    public static DateTimeOffset? NextReminder(EmergencySwitch state, IReadOnlyList<int> reminders)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(reminders);
        return state.State switch
        {
            EmergencyState.Active => state.LastLifesignAt.AddDays(state.IntervalDays),
            EmergencyState.Overdue when state.StageSentAt is { } sent && state.Stage - 1 < reminders.Count =>
                sent.AddDays(reminders[state.Stage - 1] - (state.Stage >= 2 ? reminders[state.Stage - 2] : 0)),
            _ => null,
        };
    }

    // ---------- Anträge ----------

    /// <summary>Legt einen Antrag an und benachrichtigt den Eigentümer (mit Widerspruchslink) und alle anderen Vertrauenspersonen.</summary>
    public async Task<AccessRequest> RequestAsync(User owner, User trustee, GrantMode mode, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(trustee);
        var state = await EnsureAsync(owner, cancellationToken);
        var now = time.GetUtcNow();
        var request = new AccessRequest
        {
            Id = Guid.CreateVersion7(now), OwnerId = owner.Id, TrusteeId = trustee.Id, Mode = mode, State = RequestState.Pending, RequestedAt = now,
        };
        db.AccessRequests.Add(request);
        await SendRequestMailAsync(request, owner, trustee, state, cancellationToken);

        foreach (var other in await OtherTrusteesAsync(owner.Id, trustee.Id, cancellationToken))
        {
            outbox.Enqueue(other, MailTemplate.RequestInfo, new Dictionary<string, string>
            {
                ["owner"] = owner.DisplayName,
                ["trustee"] = trustee.DisplayName,
                ["mode"] = MailTemplates.Phrase($"Mail_mode_{mode.ToString().ToLowerInvariant()}", other.Language),
            });
        }

        db.AuditLog.Add(AuditLog.Entry(AuditActions.AccessRequested, trustee.Id, owner.Id, now, null, "request", request.Id, mode.ToString()));
        return request;
    }

    /// <summary>Mail an den Eigentümer mit Widerspruchslink (auch erneut, wenn die Outbox aufgegeben hat).</summary>
    private async Task SendRequestMailAsync(AccessRequest request, User owner, User trustee, EmergencySwitch state, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var (token, entity) = OneTimeTokens.Create(TokenPurpose.Objection, owner.Id, trustee.Id, now, LinkLifetime);
        entity.ReferenceId = request.Id;
        db.OneTimeTokens.Add(entity);
        var parameters = new Dictionary<string, string>
        {
            ["trustee"] = trustee.DisplayName,
            ["mode"] = MailTemplates.Phrase($"Mail_mode_{request.Mode.ToString().ToLowerInvariant()}", owner.Language),
            ["link"] = await LinkAsync("objection", token, cancellationToken),
            ["date"] = MailTemplates.Date(now.AddDays(state.RequestWaitingDays), owner.Language),
        };
        request.OwnerMailId = outbox.Enqueue(owner, MailTemplate.RequestOwner, parameters);
        await CopyToSecondChannelsAsync(owner, state, MailTemplate.RequestOwner, parameters, cancellationToken);
    }

    /// <summary>Widerspruchslink: hält den Antrag sofort an (Entscheidung 15). Entscheiden kann der Eigentümer nur nach Anmeldung.</summary>
    public async Task PauseAsync(AccessRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.State != RequestState.Pending)
        {
            return;
        }

        var now = time.GetUtcNow();
        request.State = RequestState.Paused;
        request.PausedAt = now;
        var trustee = await db.Users.SingleAsync(u => u.Id == request.TrusteeId, cancellationToken);
        var owner = await db.Users.SingleAsync(u => u.Id == request.OwnerId, cancellationToken);
        outbox.Enqueue(trustee, MailTemplate.RequestPaused, new Dictionary<string, string> { ["owner"] = owner.DisplayName });
        db.AuditLog.Add(AuditLog.Entry(AuditActions.AccessRequestPaused, null, request.OwnerId, now, null, "request", request.Id));
    }

    /// <summary>Angehaltenen Antrag weiterlaufen lassen; die Frist verlängert sich um die Zeit des Anhaltens.</summary>
    public void Continue(AccessRequest request, Guid ownerId)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = time.GetUtcNow();
        if (request.DeadlineAt is { } deadline && request.PausedAt is { } paused)
        {
            request.DeadlineAt = deadline + (now - paused);
        }

        request.State = RequestState.Pending;
        request.PausedAt = null;
        db.AuditLog.Add(AuditLog.Entry(AuditActions.AccessRequestContinued, ownerId, ownerId, now, null, "request", request.Id));
    }

    /// <summary>Endgültiger Widerspruch nach Anmeldung; Antragsteller und andere Vertrauenspersonen erfahren es.</summary>
    public async Task ObjectAsync(AccessRequest request, User owner, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(owner);
        var now = time.GetUtcNow();
        request.State = RequestState.Objected;
        request.DecidedAt = now;
        await db.OneTimeTokens.Where(t => t.Purpose == TokenPurpose.Objection && t.ReferenceId == request.Id && t.UsedAt == null)
            .ExecuteUpdateAsync(t => t.SetProperty(x => x.UsedAt, now), cancellationToken);
        var trustee = await db.Users.SingleAsync(u => u.Id == request.TrusteeId, cancellationToken);
        foreach (var recipient in (await OtherTrusteesAsync(owner.Id, trustee.Id, cancellationToken)).Append(trustee))
        {
            outbox.Enqueue(recipient, MailTemplate.RequestObjected, new Dictionary<string, string> { ["owner"] = owner.DisplayName, ["trustee"] = trustee.DisplayName });
        }

        db.AuditLog.Add(AuditLog.Entry(AuditActions.AccessRequestObjected, owner.Id, owner.Id, now, null, "request", request.Id));
    }

    // ---------- Zustandsautomat ----------

    private async Task<bool> StepAsync(User owner, EmergencySwitch state, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var reminders = await ReminderDaysAsync(cancellationToken);
        switch (state.State)
        {
            case EmergencyState.Active when now >= state.LastLifesignAt.AddDays(state.IntervalDays):
                state.State = EmergencyState.Overdue;
                state.OverdueAt = now;
                state.Stage = 1;
                await SendStageAsync(owner, state, reminders, cancellationToken);
                LogOverdue(logger, owner.Id);
                return true;

            case EmergencyState.Overdue or EmergencyState.Waiting:
                // Erst wenn die Mail der Stufe wirklich verschickt ist, läuft die nächste Frist (Schutz bei Mailausfall).
                if (state.StageSentAt is null)
                {
                    var mail = state.StageMailId is { } mailId
                        ? await db.MailOutbox.Where(m => m.Id == mailId).Select(m => new { m.SentAt, m.FailedAt }).SingleOrDefaultAsync(cancellationToken)
                        : null;
                    if (mail?.SentAt is not { } sent)
                    {
                        // Hat die Outbox aufgegeben, geht dieselbe Stufe erneut raus: Nach einem Ausfall wird weiter erinnert.
                        if (mail is null || mail.FailedAt is not null)
                        {
                            await SendStageAsync(owner, state, reminders, cancellationToken);
                            return true;
                        }

                        return false;
                    }

                    state.StageSentAt = sent;
                    state.UpdatedAt = now;
                }

                if (state.State == EmergencyState.Overdue)
                {
                    if (state.Stage - 1 < reminders.Count)
                    {
                        var due = NextReminder(state, reminders)!.Value;
                        if (now < due)
                        {
                            return false;
                        }

                        state.Stage++;
                        await SendStageAsync(owner, state, reminders, cancellationToken);
                        return true;
                    }

                    state.State = EmergencyState.Waiting;
                    state.UpdatedAt = now;
                    db.AuditLog.Add(AuditLog.Entry(AuditActions.EmergencyWaiting, null, owner.Id, now, null));
                    return true;
                }

                if (now >= state.StageSentAt!.Value.AddDays(state.WaitingDays))
                {
                    await ReleaseAsync(owner, state, ReleasedModes.Death | ReleasedModes.Incapacity, "timeout", cancellationToken);
                    return true;
                }

                return false;

            default:
                return false;
        }
    }

    private async Task<bool> StepRequestAsync(AccessRequest request, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var owner = await db.Users.SingleOrDefaultAsync(u => u.Id == request.OwnerId, cancellationToken);
        if (owner is null)
        {
            return false;
        }

        var state = await EnsureAsync(owner, cancellationToken);
        if (request.DeadlineAt is null)
        {
            // Die Antragsfrist beginnt erst, wenn der Eigentümer die Mail mit dem Widerspruchslink bekommen hat.
            var mail = request.OwnerMailId is { } mailId
                ? await db.MailOutbox.Where(m => m.Id == mailId).Select(m => new { m.SentAt, m.FailedAt }).SingleOrDefaultAsync(cancellationToken)
                : null;
            if (mail?.SentAt is not { } sent)
            {
                if (mail is null || mail.FailedAt is not null)
                {
                    var trustee = await db.Users.SingleAsync(u => u.Id == request.TrusteeId, cancellationToken);
                    await SendRequestMailAsync(request, owner, trustee, state, cancellationToken);
                    return true;
                }

                return false;
            }

            request.DeadlineAt = sent.AddDays(state.RequestWaitingDays);
            return true;
        }

        if (now < request.DeadlineAt)
        {
            return false;
        }

        request.State = RequestState.Released;
        request.DecidedAt = now;
        await ReleaseAsync(owner, state, request.Mode == GrantMode.Death ? ReleasedModes.Death : ReleasedModes.Incapacity, "request", cancellationToken);
        return true;
    }

    private async Task ReleaseAsync(User owner, EmergencySwitch state, ReleasedModes modes, string reason, CancellationToken cancellationToken)
    {
        var added = modes & ~state.ReleasedModes;
        if (added == ReleasedModes.None)
        {
            return;
        }

        var now = time.GetUtcNow();
        state.ReleasedModes |= added;
        state.ReleasedAt ??= now;
        state.UpdatedAt = now;
        if (state.ReleasedModes == (ReleasedModes.Death | ReleasedModes.Incapacity))
        {
            state.State = EmergencyState.Released;
        }

        // Offene Anträge auf einen jetzt freigegebenen Modus sind erledigt.
        foreach (var open in await db.AccessRequests.Where(r => r.OwnerId == owner.Id && (r.State == RequestState.Pending || r.State == RequestState.Paused)).ToListAsync(cancellationToken))
        {
            if ((open.Mode == GrantMode.Death && added.HasFlag(ReleasedModes.Death)) || (open.Mode == GrantMode.Incapacity && added.HasFlag(ReleasedModes.Incapacity)))
            {
                open.State = RequestState.Released;
                open.DecidedAt = now;
            }
        }

        var modeKeys = Modes(added).ToList();
        outbox.Enqueue(owner, MailTemplate.ReleasedOwner, new Dictionary<string, string>
        {
            ["mode"] = string.Join(", ", modeKeys.Select(m => MailTemplates.Phrase($"Mail_mode_{m}", owner.Language))),
        });

        var grantModes = new List<GrantMode>();
        if (added.HasFlag(ReleasedModes.Death))
        {
            grantModes.Add(GrantMode.Death);
        }

        if (added.HasFlag(ReleasedModes.Incapacity))
        {
            grantModes.Add(GrantMode.Incapacity);
        }

        var trusteeIds = await db.Grants.Where(g => g.OwnerId == owner.Id && grantModes.Contains(g.Mode)).Select(g => g.TrusteeId).Distinct().ToListAsync(cancellationToken);
        foreach (var trustee in await db.Users.Where(u => trusteeIds.Contains(u.Id) && u.Status == UserStatus.Active).ToListAsync(cancellationToken))
        {
            outbox.Enqueue(trustee, MailTemplate.ReleasedTrustee, new Dictionary<string, string>
            {
                ["owner"] = owner.DisplayName,
                ["mode"] = string.Join(", ", modeKeys.Select(m => MailTemplates.Phrase($"Mail_mode_{m}", trustee.Language))),
            });
        }

        db.AuditLog.Add(AuditLog.Entry(AuditActions.EmergencyReleased, null, owner.Id, now, null, details: $"{added}:{reason}"));
        db.AuditLog.Add(AuditLog.Entry(AuditActions.EmergencyReleased, null, null, now, null, "user", owner.Id, $"{added}:{reason}"));
        LogReleased(logger, owner.Id, added.ToString(), reason);
    }

    private async Task SendStageAsync(User owner, EmergencySwitch state, IReadOnlyList<int> reminders, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var (token, entity) = OneTimeTokens.Create(TokenPurpose.Lifesign, owner.Id, null, now, LinkLifetime);
        db.OneTimeTokens.Add(entity);
        var template = state.Stage == 1 ? MailTemplate.LifesignDue : MailTemplate.LifesignReminder;
        var parameters = new Dictionary<string, string>
        {
            ["link"] = await LinkAsync("lifesign", token, cancellationToken),
            ["stage"] = (state.Stage - 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["count"] = reminders.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["date"] = MailTemplates.Date(EarliestRelease(state, reminders, now) ?? now, owner.Language),
        };
        state.StageMailId = outbox.Enqueue(owner, template, parameters);
        state.StageSentAt = null;
        state.UpdatedAt = now;
        await CopyToSecondChannelsAsync(owner, state, template, parameters, cancellationToken);
        db.AuditLog.Add(AuditLog.Entry(AuditActions.LifesignRequested, null, owner.Id, now, null, details: (state.Stage - 1).ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }

    /// <summary>Zweite Adresse und ntfy (Entscheidung 13). Sie ergänzen die Mail, ersetzen sie aber nicht: Fristen hängen nur an der ersten Adresse.</summary>
    private async Task CopyToSecondChannelsAsync(User owner, EmergencySwitch state, string template, Dictionary<string, string> parameters, CancellationToken cancellationToken)
    {
        if (state.SecondEmail is { Length: > 0 } second)
        {
            outbox.Enqueue(second, owner.Language, template, new Dictionary<string, string>(parameters) { ["name"] = owner.DisplayName });
        }

        var stored = await settings.GetAsync(cancellationToken);
        if (state.NtfyTopic is { Length: > 0 } topic && stored.Ntfy.ServerUrl is not null)
        {
            try
            {
                var mail = MailTemplates.Render(template, owner.Language, new Dictionary<string, string>(parameters) { ["name"] = owner.DisplayName, ["instance"] = "myLife" });
                await ntfy.SendAsync(stored, topic, mail.Subject, parameters.GetValueOrDefault("link") ?? mail.Subject, cancellationToken);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                LogNtfyFailed(logger, owner.Id, exception.GetType().Name);
            }
        }
    }

    private async Task<List<User>> OtherTrusteesAsync(Guid ownerId, Guid exceptId, CancellationToken cancellationToken)
    {
        var ids = await db.TrustRelations.Where(r => r.OwnerId == ownerId && r.TrusteeId != exceptId).Select(r => r.TrusteeId).ToListAsync(cancellationToken);
        return await db.Users.Where(u => ids.Contains(u.Id) && u.Status == UserStatus.Active).ToListAsync(cancellationToken);
    }

    private async Task<string> LinkAsync(string page, string token, CancellationToken cancellationToken)
    {
        var root = (await settings.GetAsync(cancellationToken)).PublicUrl ?? baseUrl.Value ?? string.Empty;
        return $"{root}/{page}#{token}";
    }

    private static IEnumerable<string> Modes(ReleasedModes modes)
    {
        if (modes.HasFlag(ReleasedModes.Death))
        {
            yield return "death";
        }

        if (modes.HasFlag(ReleasedModes.Incapacity))
        {
            yield return "incapacity";
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Lebenszeichen von {OwnerId} überfällig, Erinnerungen beginnen")]
    private static partial void LogOverdue(ILogger logger, Guid ownerId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Notfallzugang von {OwnerId} freigegeben: {Modes} ({Reason})")]
    private static partial void LogReleased(ILogger logger, Guid ownerId, string modes, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "ntfy-Nachricht an {OwnerId} fehlgeschlagen: {Error}")]
    private static partial void LogNtfyFailed(ILogger logger, Guid ownerId, string error);
}

/// <summary>Prüft alle 15 Minuten den Notfallzugang aller Eigentümer (Architektur 5.1) und nach jedem Start.</summary>
internal sealed partial class EmergencySwitchService(IServiceScopeFactory scopes, TimeProvider time, ILogger<EmergencySwitchService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
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
            return await scope.ServiceProvider.GetRequiredService<EmergencyEngine>().ProcessAsync(cancellationToken);
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Notfallzugang konnte nicht geprüft werden")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
