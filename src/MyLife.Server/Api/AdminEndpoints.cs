using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using MyLife.Server.Data;
using MyLife.Server.Infrastructure;
using MyLife.Server.Infrastructure.Admin;
using MyLife.Server.Infrastructure.Auth;
using MyLife.Server.Infrastructure.Mail;
using MyLife.Shared.Admin;
using MyLife.Shared.Vault;

namespace MyLife.Server.Api;

/// <summary>
/// Verwaltung (M2.1, Architektur 2): globale Einstellungen, Mailversand, Benutzer und System-Protokoll. Nur für
/// Administratoren. Kein Endpunkt hier liefert oder ändert Tresorinhalte.
/// </summary>
internal static partial class AdminEndpoints
{
    public const string Policy = "admin";

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin").RequireAuthorization(Policy).RequireRateLimiting(VaultEndpoints.RateLimitPolicy);
        group.MapGet("/settings", GetSettingsAsync);
        group.MapPut("/settings", SaveSettingsAsync);
        group.MapPost("/settings/test-mail", TestMailAsync);
        group.MapPost("/settings/test-ntfy", TestNtfyAsync);
        group.MapGet("/mail", GetMailQueueAsync);
        group.MapPost("/mail/retry", RetryMailAsync);
        group.MapGet("/users", ListUsersAsync);
        group.MapPost("/users/{id:guid}/lock", LockAsync);
        group.MapPost("/users/{id:guid}/unlock", UnlockAsync);
        group.MapPost("/users/{id:guid}/reset-2fa", ResetTwoFactorAsync);
        group.MapPut("/users/{id:guid}/admin", SetAdminAsync);
        group.MapPost("/users/{id:guid}/deletion", RequestDeletionAsync);
        group.MapDelete("/users/{id:guid}/deletion", CancelDeletionAsync);
        group.MapGet("/audit", ListAuditAsync);
        return app;
    }

    private static async Task<Ok<AdminSettingsDto>> GetSettingsAsync(SettingsStore settings, CancellationToken cancellationToken) =>
        TypedResults.Ok(await settings.GetForAdminAsync(cancellationToken));

    private static async Task<Results<Ok<AdminSettingsDto>, ValidationProblem>> SaveSettingsAsync(
        AdminSettingsDto request, ClaimsPrincipal principal, MyLifeDbContext db, SettingsStore settings, AuditLog audit, CancellationToken cancellationToken)
    {
        var errors = AdminSettingsValidation.Validate(request);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors.ToDictionary(e => e.Key, e => new[] { e.Value }));
        }

        audit.Add(AuditActions.AdminSettingsChanged, Me(principal), null);
        await settings.SaveAsync(db, request, cancellationToken);
        return TypedResults.Ok(await settings.GetForAdminAsync(cancellationToken));
    }

    /// <summary>Testmail an die eigene Adresse, direkt und nicht über die Outbox, damit der Fehler sofort sichtbar ist.</summary>
    private static async Task<Results<Ok<SendTestResultDto>, ProblemHttpResult>> TestMailAsync(
        ClaimsPrincipal principal, MyLifeDbContext db, MailComposer composer, IMailTransport transport, AuditLog audit, ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        var me = await db.Users.SingleAsync(u => u.Id == Me(principal), cancellationToken);
        var config = await composer.ConfigAsync(cancellationToken);
        if (config is null)
        {
            return ApiProblems.Create(StatusCodes.Status400BadRequest, AdminErrors.MailNotConfigured);
        }

        audit.Own(AuditActions.AdminMailTested, me.Id);
        await db.SaveChangesAsync(cancellationToken);
        try
        {
            var parameters = await composer.WithInstanceAsync(new Dictionary<string, string> { ["name"] = me.DisplayName }, cancellationToken);
            var mail = MailTemplates.Render(MailTemplate.Test, me.Language, parameters);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(45));
            await transport.SendAsync(config, MailComposer.Build(config, me.Email, mail), timeout.Token);
            return TypedResults.Ok(new SendTestResultDto(true, null));
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LogTestFailed(loggers.CreateLogger(typeof(AdminEndpoints)), "smtp", exception.GetType().Name);
            return TypedResults.Ok(new SendTestResultDto(false, MailOutboxService.Short(exception.Message)));
        }
    }

    private static async Task<Results<Ok<SendTestResultDto>, ProblemHttpResult>> TestNtfyAsync(
        NtfyTestRequest request, ClaimsPrincipal principal, MyLifeDbContext db, SettingsStore settings, NtfyClient ntfy, AuditLog audit,
        ILoggerFactory loggers, CancellationToken cancellationToken)
    {
        var stored = await settings.GetAsync(cancellationToken);
        if (stored.Ntfy.ServerUrl is null || !NtfyClient.IsValidTopic(request.Topic))
        {
            return ApiProblems.Create(StatusCodes.Status400BadRequest, AdminErrors.Invalid);
        }

        audit.Own(AuditActions.AdminNtfyTested, Me(principal));
        await db.SaveChangesAsync(cancellationToken);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            await ntfy.SendAsync(stored, request.Topic, "myLife", "Test", timeout.Token);
            return TypedResults.Ok(new SendTestResultDto(true, null));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            LogTestFailed(loggers.CreateLogger(typeof(AdminEndpoints)), "ntfy", exception.GetType().Name);
            return TypedResults.Ok(new SendTestResultDto(false, MailOutboxService.Short(exception.Message)));
        }
    }

    private static async Task<Ok<MailQueueDto>> GetMailQueueAsync(MyLifeDbContext db, CancellationToken cancellationToken)
    {
        var open = db.MailOutbox.AsNoTracking().Where(m => m.SentAt == null);
        var pending = await open.CountAsync(m => m.FailedAt == null, cancellationToken);
        var failed = await open.CountAsync(m => m.FailedAt != null, cancellationToken);
        var lastSent = await db.MailOutbox.AsNoTracking().MaxAsync(m => m.SentAt, cancellationToken);
        var lastError = await open.Where(m => m.LastError != null).OrderByDescending(m => m.CreatedAt)
            .Select(m => new { m.LastError, m.CreatedAt }).FirstOrDefaultAsync(cancellationToken);
        return TypedResults.Ok(new MailQueueDto(pending, failed, lastSent, lastError?.LastError, lastError?.CreatedAt));
    }

    private static async Task<NoContent> RetryMailAsync(
        ClaimsPrincipal principal, MyLifeDbContext db, MailSignal signal, AuditLog audit, TimeProvider time, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var failed = await db.MailOutbox.Where(m => m.SentAt == null && (m.FailedAt != null || m.NextAttemptAt > now)).ToListAsync(cancellationToken);
        foreach (var entry in failed.Where(e => e.Parameters is not null || e.FailedAt is null))
        {
            entry.FailedAt = null;
            entry.Attempts = 0;
            entry.NextAttemptAt = now;
        }

        audit.Own(AuditActions.AdminMailRetried, Me(principal));
        await db.SaveChangesAsync(cancellationToken);
        signal.Notify();
        return TypedResults.NoContent();
    }

    private static async Task<Ok<List<AdminUserDto>>> ListUsersAsync(ClaimsPrincipal principal, MyLifeDbContext db, TimeProvider time, CancellationToken cancellationToken)
    {
        var me = Me(principal);
        var now = time.GetUtcNow();
        var users = await db.Users.AsNoTracking().OrderBy(u => u.DisplayName)
            .Select(u => new AdminUserDto(
                u.Id, u.Email, u.DisplayName, u.IsAdmin, (AccountStatus)u.Status, u.TotpSecret != null, u.Passkeys.Count, u.CreatedAt, u.LastLoginAt,
                db.OneTimeTokens.Any(t => t.UserId == u.Id && t.Purpose == TokenPurpose.AccountDeletion && t.UsedAt == null && t.ExpiresAt > now),
                u.Id == me))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(users);
    }

    private static Task<Results<Ok<AdminUserDto>, ProblemHttpResult>> LockAsync(
        Guid id, ClaimsPrincipal principal, MyLifeDbContext db, AuditLog audit, MailOutbox outbox, TimeProvider time, CancellationToken cancellationToken) =>
        ChangeUserAsync(id, principal, db, time, user =>
        {
            if (user.Status == UserStatus.Locked)
            {
                return null;
            }

            user.Status = UserStatus.Locked;
            user.SecurityStamp = SecurityStamps.New();
            audit.Add(AuditActions.AdminUserLocked, Me(principal), user.Id, "user", user.Id);
            outbox.Enqueue(user, MailTemplate.AccountLocked);
            return null;
        }, cancellationToken);

    private static Task<Results<Ok<AdminUserDto>, ProblemHttpResult>> UnlockAsync(
        Guid id, ClaimsPrincipal principal, MyLifeDbContext db, AuditLog audit, MailOutbox outbox, TimeProvider time, CancellationToken cancellationToken) =>
        ChangeUserAsync(id, principal, db, time, user =>
        {
            if (user.Status != UserStatus.Locked)
            {
                return null;
            }

            user.Status = UserStatus.Active;
            user.FailedLoginCount = 0;
            user.LockoutUntil = null;
            audit.Add(AuditActions.AdminUserUnlocked, Me(principal), user.Id, "user", user.Id);
            outbox.Enqueue(user, MailTemplate.AccountUnlocked);
            return null;
        }, cancellationToken);

    /// <summary>Entfernt TOTP und Passkeys; bei der nächsten Anmeldung richtet der Nutzer eine Authenticator-App neu ein.</summary>
    private static Task<Results<Ok<AdminUserDto>, ProblemHttpResult>> ResetTwoFactorAsync(
        Guid id, ClaimsPrincipal principal, MyLifeDbContext db, AuditLog audit, MailOutbox outbox, TimeProvider time, CancellationToken cancellationToken) =>
        ChangeUserAsync(id, principal, db, time, user =>
        {
            user.TotpSecret = null;
            user.TotpLastStep = null;
            db.Passkeys.RemoveRange(user.Passkeys);
            user.SecurityStamp = SecurityStamps.New();
            audit.Add(AuditActions.AdminTwoFactorReset, Me(principal), user.Id, "user", user.Id);
            outbox.Enqueue(user, MailTemplate.TwoFactorReset);
            return null;
        }, cancellationToken);

    /// <summary>Admin-Recht geben oder nehmen. Der neue Stempel beendet die Sitzungen des Kontos, damit die Rolle sofort gilt.</summary>
    private static async Task<Results<Ok<AdminUserDto>, ProblemHttpResult>> SetAdminAsync(
        Guid id, SetAdminRequest request, ClaimsPrincipal principal, MyLifeDbContext db, AuditLog audit, TimeProvider time,
        CancellationToken cancellationToken) =>
        await ChangeUserAsync(id, principal, db, time, user =>
        {
            if (user.IsAdmin == request.IsAdmin)
            {
                return null;
            }

            user.IsAdmin = request.IsAdmin;
            user.SecurityStamp = SecurityStamps.New();
            audit.Add(request.IsAdmin ? AuditActions.AdminRoleGranted : AuditActions.AdminRoleRevoked, Me(principal), user.Id, "user", user.Id);
            return null;
        }, cancellationToken);

    /// <summary>
    /// Löschen beantragen: Der Nutzer bekommt eine Mail mit Link und muss selbst bestätigen (Entscheidung 44).
    /// Ein neuer Antrag ersetzt einen offenen.
    /// </summary>
    private static async Task<Results<Ok<AdminUserDto>, ProblemHttpResult>> RequestDeletionAsync(
        Guid id, HttpContext context, ClaimsPrincipal principal, MyLifeDbContext db, AuditLog audit, MailOutbox outbox, SettingsStore settings,
        TimeProvider time, CancellationToken cancellationToken)
    {
        var stored = await settings.GetAsync(cancellationToken);
        if (!SettingsStore.IsMailConfigured(stored))
        {
            return ApiProblems.Create(StatusCodes.Status400BadRequest, AdminErrors.MailNotConfigured);
        }

        var baseUrl = stored.PublicUrl ?? $"{context.Request.Scheme}://{context.Request.Host}";
        return await ChangeUserAsync(id, principal, db, time, user =>
        {
            db.OneTimeTokens.RemoveRange(db.OneTimeTokens.Where(t => t.UserId == user.Id && t.Purpose == TokenPurpose.AccountDeletion));
            var (token, entity) = OneTimeTokens.Create(
                TokenPurpose.AccountDeletion, user.Id, Me(principal), time.GetUtcNow(), TimeSpan.FromDays(AdminLimits.DeletionLinkDays));
            db.OneTimeTokens.Add(entity);
            audit.Add(AuditActions.AdminDeletionRequested, Me(principal), user.Id, "user", user.Id);
            outbox.Enqueue(user, MailTemplate.DeletionRequest, new Dictionary<string, string>
            {
                ["link"] = $"{baseUrl}/account/delete#{token}",
                ["days"] = AdminLimits.DeletionLinkDays.ToString(System.Globalization.CultureInfo.InvariantCulture),
            });
            return null;
        }, cancellationToken);
    }

    private static Task<Results<Ok<AdminUserDto>, ProblemHttpResult>> CancelDeletionAsync(
        Guid id, ClaimsPrincipal principal, MyLifeDbContext db, AuditLog audit, TimeProvider time, CancellationToken cancellationToken) =>
        ChangeUserAsync(id, principal, db, time, user =>
        {
            db.OneTimeTokens.RemoveRange(db.OneTimeTokens.Where(t => t.UserId == user.Id && t.Purpose == TokenPurpose.AccountDeletion && t.UsedAt == null));
            audit.Add(AuditActions.AdminDeletionCancelled, Me(principal), user.Id, "user", user.Id);
            return null;
        }, cancellationToken);

    /// <summary>System-Protokoll: Ereignisse ohne Tresorbezug und alle Verwaltungsaktionen, nie Tresorinhalte.</summary>
    private static async Task<Ok<AuditPageDto>> ListAuditAsync(MyLifeDbContext db, int? page, int? size, CancellationToken cancellationToken)
    {
        var take = Math.Clamp(size ?? 50, 1, 100);
        var skip = Math.Max(page ?? 0, 0) * take;
        var query = db.AuditLog.AsNoTracking().Where(a =>
            a.OwnerId == null || a.Action.StartsWith("admin-") || a.Action == AuditActions.AccountDeleted || a.Action == AuditActions.MailFailed);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(a => a.At).ThenByDescending(a => a.Id).Skip(skip).Take(take)
            .Select(a => new AuditEntryDto(
                a.Id, a.At, a.Action, a.TargetType, a.TargetId, a.Ip,
                db.Users.IgnoreQueryFilters().Where(u => u.Id == a.ActorId).Select(u => u.DisplayName).FirstOrDefault(),
                a.TargetType == "user"
                    ? db.Users.IgnoreQueryFilters().Where(u => u.Id == a.TargetId).Select(u => u.DisplayName).FirstOrDefault()
                    : a.Details))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(new AuditPageDto(items, total));
    }

    /// <summary>
    /// Gemeinsamer Rahmen für Änderungen an einem Konto: nicht am eigenen Konto, immer bleibt mindestens ein aktiver
    /// Admin. <paramref name="change"/> liefert einen Fehlercode oder null.
    /// </summary>
    private static async Task<Results<Ok<AdminUserDto>, ProblemHttpResult>> ChangeUserAsync(
        Guid id, ClaimsPrincipal principal, MyLifeDbContext db, TimeProvider time, Func<User, string?> change, CancellationToken cancellationToken)
    {
        var me = Me(principal);
        if (id == me)
        {
            return ApiProblems.Create(StatusCodes.Status400BadRequest, AdminErrors.Self);
        }

        var user = await db.Users.Include(u => u.Passkeys).SingleOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
        {
            return ApiProblems.Create(StatusCodes.Status404NotFound, AdminErrors.NotFound);
        }

        if (change(user) is { } error)
        {
            return ApiProblems.Create(StatusCodes.Status400BadRequest, error);
        }

        if (!await db.Users.AnyAsync(u => u.IsAdmin && u.Status == UserStatus.Active && u.Id != user.Id, cancellationToken)
            && !(user.IsAdmin && user.Status == UserStatus.Active))
        {
            return ApiProblems.Create(StatusCodes.Status400BadRequest, AdminErrors.LastAdmin);
        }

        await db.SaveChangesAsync(cancellationToken);
        var now = time.GetUtcNow();
        var pending = await db.OneTimeTokens.AnyAsync(
            t => t.UserId == user.Id && t.Purpose == TokenPurpose.AccountDeletion && t.UsedAt == null && t.ExpiresAt > now, cancellationToken);
        return TypedResults.Ok(new AdminUserDto(
            user.Id, user.Email, user.DisplayName, user.IsAdmin, (AccountStatus)user.Status, user.TotpSecret is not null, user.Passkeys.Count,
            user.CreatedAt, user.LastLoginAt, pending, false));
    }

    private static Guid Me(ClaimsPrincipal principal) => MyLifeClaims.UserId(principal) ?? throw new InvalidOperationException("Nicht angemeldet.");

    [LoggerMessage(Level = LogLevel.Warning, Message = "Versandtest {Channel} fehlgeschlagen: {Error}")]
    private static partial void LogTestFailed(ILogger logger, string channel, string error);
}
