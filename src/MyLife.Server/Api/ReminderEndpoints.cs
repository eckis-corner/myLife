using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using MyLife.Server.Data;
using MyLife.Server.Infrastructure;
using MyLife.Server.Infrastructure.Auth;
using MyLife.Server.Infrastructure.Reminders;
using MyLife.Shared.Reminders;
using MyLife.Shared.Vault;

namespace MyLife.Server.Api;

/// <summary>Erinnerungen (M3.1): eigene Einstellungen und der nächste Termin, den der Browser aus dem Tresor berechnet.</summary>
internal static class ReminderEndpoints
{
    public static IEndpointRouteBuilder MapReminderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reminders").RequireAuthorization().RequireRateLimiting(VaultEndpoints.RateLimitPolicy);
        group.MapGet("", GetAsync);
        group.MapPut("/settings", SaveSettingsAsync);
        group.MapPut("/next", SetNextAsync);
        return app;
    }

    private static async Task<Ok<ReminderStatusDto>> GetAsync(ClaimsPrincipal principal, MyLifeDbContext db, ReminderEngine engine, CancellationToken cancellationToken)
    {
        var reminder = await engine.EnsureAsync(Me(principal), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToDto(reminder));
    }

    private static async Task<Results<Ok<ReminderStatusDto>, ProblemHttpResult>> SaveSettingsAsync(
        ReminderSettingsDto request, ClaimsPrincipal principal, MyLifeDbContext db, ReminderEngine engine, AuditLog audit, TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (request is null || !request.IsValid)
        {
            return ApiProblems.Create(StatusCodes.Status400BadRequest, ReminderErrors.Invalid);
        }

        var me = Me(principal);
        var reminder = await engine.EnsureAsync(me, cancellationToken);
        reminder.ReviewMonths = request.ReviewMonths;
        reminder.LeadDays = request.LeadDays;
        reminder.MailEnabled = request.MailEnabled;
        reminder.UpdatedAt = time.GetUtcNow();
        audit.Own(AuditActions.ReminderSettingsChanged, me);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToDto(reminder));
    }

    /// <summary>Nur ein Datum ohne Zusammenhang (39b); der Server weiß nicht, ob es ein Ausweis, ein Vertrag oder eine Wiedervorlage ist.</summary>
    private static async Task<NoContent> SetNextAsync(
        NextDueRequest request, ClaimsPrincipal principal, MyLifeDbContext db, ReminderEngine engine, TimeProvider time, CancellationToken cancellationToken)
    {
        var reminder = await engine.EnsureAsync(Me(principal), cancellationToken);
        if (reminder.NextDueOn != request?.NextDueOn)
        {
            reminder.NextDueOn = request?.NextDueOn;
            reminder.UpdatedAt = time.GetUtcNow();
            await db.SaveChangesAsync(cancellationToken);
        }

        return TypedResults.NoContent();
    }

    private static ReminderStatusDto ToDto(ReminderSetting reminder) =>
        new(new ReminderSettingsDto(reminder.ReviewMonths, reminder.LeadDays, reminder.MailEnabled), reminder.NextDueOn);

    private static Guid Me(ClaimsPrincipal principal) => MyLifeClaims.UserId(principal) ?? throw new InvalidOperationException("Nicht angemeldet.");
}
