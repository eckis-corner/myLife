using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using MyLife.Server.Data;
using MyLife.Server.Infrastructure;
using MyLife.Server.Infrastructure.Admin;
using MyLife.Server.Infrastructure.Auth;
using MyLife.Server.Infrastructure.Emergency;
using MyLife.Shared.Admin;
using MyLife.Shared.Emergency;
using MyLife.Shared.Trust;
using MyLife.Shared.Vault;

namespace MyLife.Server.Api;

/// <summary>
/// Notfallzugang (M2.4, Architektur 5): Stand und Einstellungen des Eigentümers, Lebenszeichen, Anträge von
/// Vertrauenspersonen mit Widerspruch, dazu die Links aus den Mails (ohne Anmeldung).
/// </summary>
internal static class EmergencyEndpoints
{
    public static IEndpointRouteBuilder MapEmergencyEndpoints(this IEndpointRouteBuilder app)
    {
        var mine = app.MapGroup("/api/emergency").RequireAuthorization().RequireRateLimiting(VaultEndpoints.RateLimitPolicy);
        mine.MapGet("", StatusAsync);
        mine.MapPost("/lifesign", LifesignAsync);
        mine.MapPut("/settings", SaveSettingsAsync);
        mine.MapPost("/requests/{id:guid}/object", ObjectAsync);
        mine.MapPost("/requests/{id:guid}/continue", ContinueAsync);
        mine.MapGet("/trusted", TrustedAsync);
        mine.MapPost("/trusted/{ownerId:guid}/requests", RequestAsync);
        mine.MapDelete("/requests/{id:guid}", WithdrawAsync);

        var links = app.MapGroup("/api/emergency/link").RequireRateLimiting(AuthEndpoints.RateLimitPolicy);
        links.MapPost("/lifesign", LifesignLinkAsync);
        links.MapPost("/objection", ObjectionLinkAsync);
        return app;
    }

    // ---------- Eigentümer ----------

    private static async Task<Ok<EmergencyStatusDto>> StatusAsync(
        ClaimsPrincipal principal, MyLifeDbContext db, EmergencyEngine engine, SettingsStore settings, TimeProvider time, CancellationToken cancellationToken)
    {
        var me = await db.Users.SingleAsync(u => u.Id == Me(principal), cancellationToken);
        var state = await engine.EnsureAsync(me, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        var stored = await settings.GetAsync(cancellationToken);
        var reminders = stored.Deadlines.ReminderDays;
        var requests = await RequestsQuery(db, r => r.OwnerId == me.Id).ToListAsync(cancellationToken);
        return TypedResults.Ok(new EmergencyStatusDto(
            await engine.IsEnabledAsync(me.Id, cancellationToken),
            state.State,
            state.LastLifesignAt,
            state.OverdueAt,
            EmergencyEngine.NextReminder(state, reminders),
            EmergencyEngine.EarliestRelease(state, reminders, time.GetUtcNow()),
            state.ReleasedModes,
            state.ReleasedAt,
            Math.Max(state.Stage - 1, 0),
            await engine.IsMailDelayedAsync(state, cancellationToken),
            new EmergencySettingsDto(state.IntervalDays, state.WaitingDays, state.RequestWaitingDays, state.SecondEmail, state.NtfyTopic),
            stored.Deadlines,
            stored.Ntfy.ServerUrl is not null,
            requests));
    }

    private static async Task<NoContent> LifesignAsync(ClaimsPrincipal principal, MyLifeDbContext db, EmergencyEngine engine, CancellationToken cancellationToken)
    {
        var me = await db.Users.SingleAsync(u => u.Id == Me(principal), cancellationToken);
        await engine.LifesignAsync(me, "button", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>Eigene Fristen innerhalb der Grenzen des Admins (Entscheidung 43), zweite Adresse und ntfy-Thema.</summary>
    private static async Task<Results<NoContent, ProblemHttpResult>> SaveSettingsAsync(
        EmergencySettingsDto request, ClaimsPrincipal principal, MyLifeDbContext db, EmergencyEngine engine, SettingsStore settings, AuditLog audit,
        CancellationToken cancellationToken)
    {
        var limits = (await settings.GetAsync(cancellationToken)).Deadlines;
        static bool Within(int value, DayRange range) => value >= range.Min && value <= range.Max;
        var secondEmail = string.IsNullOrWhiteSpace(request.SecondEmail) ? null : KeyMaterialValidation.NormalizeEmail(request.SecondEmail);
        var topic = string.IsNullOrWhiteSpace(request.NtfyTopic) ? null : request.NtfyTopic.Trim();
        if (!Within(request.IntervalDays, limits.LifesignIntervalDays) || !Within(request.WaitingDays, limits.WaitingDays)
            || !Within(request.RequestWaitingDays, limits.RequestWaitingDays)
            || (secondEmail is not null && !KeyMaterialValidation.IsEmail(secondEmail)) || (topic is not null && !NtfyClient.IsValidTopic(topic)))
        {
            return ApiProblems.Create(StatusCodes.Status400BadRequest, EmergencyErrors.Invalid);
        }

        var me = await db.Users.SingleAsync(u => u.Id == Me(principal), cancellationToken);
        var state = await engine.EnsureAsync(me, cancellationToken);
        state.IntervalDays = request.IntervalDays;
        state.WaitingDays = request.WaitingDays;
        state.RequestWaitingDays = request.RequestWaitingDays;
        state.SecondEmail = secondEmail;
        state.NtfyTopic = topic;
        // Wer seine Einstellungen ändert, lebt: das zählt als Lebenszeichen.
        await engine.LifesignAsync(me, "settings", cancellationToken);
        audit.Own(AuditActions.EmergencySettingsChanged, me.Id);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ObjectAsync(
        Guid id, ClaimsPrincipal principal, MyLifeDbContext db, EmergencyEngine engine, CancellationToken cancellationToken)
    {
        var me = await db.Users.SingleAsync(u => u.Id == Me(principal), cancellationToken);
        var request = await db.AccessRequests.SingleOrDefaultAsync(
            r => r.Id == id && r.OwnerId == me.Id && (r.State == RequestState.Pending || r.State == RequestState.Paused), cancellationToken);
        if (request is null)
        {
            return ApiProblems.Create(StatusCodes.Status404NotFound, EmergencyErrors.NotFound);
        }

        await engine.ObjectAsync(request, me, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ContinueAsync(
        Guid id, ClaimsPrincipal principal, MyLifeDbContext db, EmergencyEngine engine, CancellationToken cancellationToken)
    {
        var me = Me(principal);
        var request = await db.AccessRequests.SingleOrDefaultAsync(r => r.Id == id && r.OwnerId == me && r.State == RequestState.Paused, cancellationToken);
        if (request is null)
        {
            return ApiProblems.Create(StatusCodes.Status404NotFound, EmergencyErrors.NotFound);
        }

        engine.Continue(request, me);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    // ---------- Vertrauensperson ----------

    private static async Task<Ok<List<TrustedOwnerDto>>> TrustedAsync(ClaimsPrincipal principal, MyLifeDbContext db, CancellationToken cancellationToken)
    {
        var me = Me(principal);
        var relations = await db.TrustRelations.AsNoTracking().Where(r => r.TrusteeId == me).ToListAsync(cancellationToken);
        var ownerIds = relations.Select(r => r.OwnerId).ToList();
        var owners = await db.Users.AsNoTracking().Where(u => ownerIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, cancellationToken);
        var grants = await db.Grants.AsNoTracking().Where(g => g.TrusteeId == me && ownerIds.Contains(g.OwnerId))
            .Select(g => new { g.OwnerId, g.Mode }).Distinct().ToListAsync(cancellationToken);
        var switches = await db.EmergencySwitches.AsNoTracking().Where(s => ownerIds.Contains(s.OwnerId)).ToDictionaryAsync(s => s.OwnerId, cancellationToken);
        var requests = await RequestsQuery(db, r => r.TrusteeId == me).ToListAsync(cancellationToken);
        return TypedResults.Ok(relations.Where(r => owners.ContainsKey(r.OwnerId)).OrderBy(r => owners[r.OwnerId].DisplayName).Select(r => new TrustedOwnerDto(
            r.OwnerId,
            owners[r.OwnerId].DisplayName,
            r.CanRequestAccess,
            grants.Any(g => g.OwnerId == r.OwnerId && g.Mode == GrantMode.Death),
            grants.Any(g => g.OwnerId == r.OwnerId && g.Mode == GrantMode.Incapacity),
            switches.TryGetValue(r.OwnerId, out var state) ? state.ReleasedModes : ReleasedModes.None,
            [.. requests.Where(q => q.OwnerId == r.OwnerId)])).ToList());
    }

    /// <summary>Antrag auf einen Modus (Entscheidungen 14, 33): nur mit Antragsrecht, je Modus nur ein offener Antrag.</summary>
    private static async Task<Results<Created<AccessRequestDto>, ProblemHttpResult>> RequestAsync(
        Guid ownerId, CreateAccessRequest request, ClaimsPrincipal principal, MyLifeDbContext db, EmergencyEngine engine, CancellationToken cancellationToken)
    {
        var me = await db.Users.SingleAsync(u => u.Id == Me(principal), cancellationToken);
        var relation = await db.TrustRelations.AsNoTracking().SingleOrDefaultAsync(r => r.OwnerId == ownerId && r.TrusteeId == me.Id, cancellationToken);
        var owner = relation is null ? null : await db.Users.SingleOrDefaultAsync(u => u.Id == ownerId && u.Status == UserStatus.Active, cancellationToken);
        if (owner is null)
        {
            return ApiProblems.Create(StatusCodes.Status404NotFound, EmergencyErrors.NotFound);
        }

        if (!relation!.CanRequestAccess)
        {
            return ApiProblems.Create(StatusCodes.Status403Forbidden, EmergencyErrors.NotAllowed);
        }

        if (request.Mode is not (GrantMode.Death or GrantMode.Incapacity))
        {
            return ApiProblems.Create(StatusCodes.Status400BadRequest, EmergencyErrors.Invalid);
        }

        var flag = request.Mode == GrantMode.Death ? ReleasedModes.Death : ReleasedModes.Incapacity;
        var state = await engine.EnsureAsync(owner, cancellationToken);
        if (state.ReleasedModes.HasFlag(flag))
        {
            return ApiProblems.Create(StatusCodes.Status409Conflict, EmergencyErrors.AlreadyReleased);
        }

        if (await db.AccessRequests.AnyAsync(r => r.OwnerId == ownerId && r.Mode == request.Mode
                && (r.State == RequestState.Pending || r.State == RequestState.Paused), cancellationToken))
        {
            return ApiProblems.Create(StatusCodes.Status409Conflict, EmergencyErrors.Pending);
        }

        var created = await engine.RequestAsync(owner, me, request.Mode, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/emergency/requests/{created.Id}", await RequestsQuery(db, r => r.Id == created.Id).SingleAsync(cancellationToken));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> WithdrawAsync(
        Guid id, ClaimsPrincipal principal, MyLifeDbContext db, TimeProvider time, CancellationToken cancellationToken)
    {
        var me = Me(principal);
        var request = await db.AccessRequests.SingleOrDefaultAsync(
            r => r.Id == id && r.TrusteeId == me && (r.State == RequestState.Pending || r.State == RequestState.Paused), cancellationToken);
        if (request is null)
        {
            return ApiProblems.Create(StatusCodes.Status404NotFound, EmergencyErrors.NotFound);
        }

        var now = time.GetUtcNow();
        request.State = RequestState.Withdrawn;
        request.DecidedAt = now;
        db.AuditLog.Add(AuditLog.Entry(AuditActions.AccessRequestWithdrawn, me, request.OwnerId, now, null, "request", request.Id));
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    // ---------- Links aus Mails ----------

    /// <summary>Lebenszeichen über den Link: setzt nur das Lebenszeichen, sonst nichts (Architektur 5.1).</summary>
    private static async Task<Results<Ok<EmergencyLinkResultDto>, ProblemHttpResult>> LifesignLinkAsync(
        EmergencyTokenRequest request, MyLifeDbContext db, EmergencyEngine engine, TimeProvider time, CancellationToken cancellationToken)
    {
        var token = await OneTimeTokens.FindAsync(db, TokenPurpose.Lifesign, request.Token, time.GetUtcNow(), cancellationToken);
        var owner = token is null ? null : await db.Users.SingleOrDefaultAsync(u => u.Id == token.UserId && u.Status == UserStatus.Active, cancellationToken);
        if (owner is null)
        {
            return ApiProblems.Create(StatusCodes.Status404NotFound, EmergencyErrors.TokenInvalid);
        }

        await engine.LifesignAsync(owner, "link", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(new EmergencyLinkResultDto(owner.DisplayName, null, null));
    }

    /// <summary>Widerspruchslink: hält den Antrag sofort an; endgültig entscheidet der Eigentümer nach der Anmeldung.</summary>
    private static async Task<Results<Ok<EmergencyLinkResultDto>, ProblemHttpResult>> ObjectionLinkAsync(
        EmergencyTokenRequest request, MyLifeDbContext db, EmergencyEngine engine, TimeProvider time, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var token = await OneTimeTokens.FindAsync(db, TokenPurpose.Objection, request.Token, now, cancellationToken);
        var access = token?.ReferenceId is { } requestId ? await db.AccessRequests.SingleOrDefaultAsync(r => r.Id == requestId, cancellationToken) : null;
        if (token is null || access is null || access.State is not (RequestState.Pending or RequestState.Paused))
        {
            return ApiProblems.Create(StatusCodes.Status404NotFound, EmergencyErrors.TokenInvalid);
        }

        token.UsedAt = now;
        await engine.PauseAsync(access, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        var owner = await db.Users.AsNoTracking().SingleAsync(u => u.Id == access.OwnerId, cancellationToken);
        var trustee = await db.Users.AsNoTracking().SingleAsync(u => u.Id == access.TrusteeId, cancellationToken);
        return TypedResults.Ok(new EmergencyLinkResultDto(owner.DisplayName, trustee.DisplayName, access.Mode));
    }

    private static IQueryable<AccessRequestDto> RequestsQuery(MyLifeDbContext db, System.Linq.Expressions.Expression<Func<AccessRequest, bool>> filter) =>
        from r in db.AccessRequests.AsNoTracking().Where(filter)
        join owner in db.Users on r.OwnerId equals owner.Id
        join trustee in db.Users on r.TrusteeId equals trustee.Id
        orderby r.RequestedAt descending
        select new AccessRequestDto(r.Id, r.OwnerId, owner.DisplayName, r.TrusteeId, trustee.DisplayName, r.Mode, r.State, r.RequestedAt, r.DeadlineAt, r.PausedAt, r.DecidedAt);

    private static Guid Me(ClaimsPrincipal principal) => MyLifeClaims.UserId(principal) ?? throw new InvalidOperationException("Nicht angemeldet.");
}
