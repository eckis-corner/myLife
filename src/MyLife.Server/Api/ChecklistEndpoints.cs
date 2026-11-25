using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using MyLife.Server.Data;
using MyLife.Server.Infrastructure;
using MyLife.Server.Infrastructure.Auth;
using MyLife.Shared.Checklist;
using MyLife.Shared.Trust;
using MyLife.Shared.Vault;

namespace MyLife.Server.Api;

/// <summary>
/// Gemeinsames Abhaken der Checkliste (M3.2, Entscheidung 21). Lesen und abhaken darf der Eigentümer und jede
/// Vertrauensperson, der der Server die Checkliste (Bereich 12) gerade ausliefert: sofort geteilt oder nach der
/// Freigabe. Abgehakt werden nur bestehende Aufgaben des Eigentümers.
/// </summary>
internal static class ChecklistEndpoints
{
    public static IEndpointRouteBuilder MapChecklistEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/checklist").RequireAuthorization().RequireRateLimiting(VaultEndpoints.RateLimitPolicy);
        group.MapGet("/{ownerId:guid}", ListAsync);
        group.MapPut("/{ownerId:guid}/tasks/{taskId:guid}", DoneAsync);
        group.MapDelete("/{ownerId:guid}/tasks/{taskId:guid}", UndoneAsync);
        return app;
    }

    private static async Task<Results<Ok<List<ChecklistProgressDto>>, ProblemHttpResult>> ListAsync(
        Guid ownerId, ClaimsPrincipal principal, MyLifeDbContext db, CancellationToken cancellationToken)
    {
        if (!await AllowedAsync(db, ownerId, Me(principal), cancellationToken))
        {
            return ApiProblems.Create(StatusCodes.Status404NotFound, ChecklistErrors.NotFound);
        }

        return TypedResults.Ok(await (
            from p in db.ChecklistProgress.AsNoTracking()
            join o in db.Objects on p.TaskId equals o.Id
            join u in db.Users on p.DoneById equals u.Id
            where p.OwnerId == ownerId && !o.IsDeleted
            orderby p.DoneAt
            select new ChecklistProgressDto(p.TaskId, p.DoneById, u.DisplayName, p.DoneAt)).ToListAsync(cancellationToken));
    }

    private static async Task<Results<Ok<ChecklistProgressDto>, ProblemHttpResult>> DoneAsync(
        Guid ownerId, Guid taskId, ClaimsPrincipal principal, MyLifeDbContext db, AuditLog audit, TimeProvider time, CancellationToken cancellationToken)
    {
        var me = Me(principal);
        if (!await AllowedAsync(db, ownerId, me, cancellationToken) || !await IsTaskAsync(db, ownerId, taskId, cancellationToken))
        {
            return ApiProblems.Create(StatusCodes.Status404NotFound, ChecklistErrors.NotFound);
        }

        var existing = await db.ChecklistProgress.SingleOrDefaultAsync(p => p.TaskId == taskId, cancellationToken);
        if (existing is null)
        {
            existing = new ChecklistProgress { TaskId = taskId, OwnerId = ownerId, DoneById = me, DoneAt = time.GetUtcNow() };
            db.ChecklistProgress.Add(existing);
            audit.Add(AuditActions.ChecklistDone, me, ownerId, "task", taskId);
            await db.SaveChangesAsync(cancellationToken);
        }

        var name = await db.Users.Where(u => u.Id == existing.DoneById).Select(u => u.DisplayName).SingleAsync(cancellationToken);
        return TypedResults.Ok(new ChecklistProgressDto(existing.TaskId, existing.DoneById, name, existing.DoneAt));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> UndoneAsync(
        Guid ownerId, Guid taskId, ClaimsPrincipal principal, MyLifeDbContext db, AuditLog audit, CancellationToken cancellationToken)
    {
        var me = Me(principal);
        if (!await AllowedAsync(db, ownerId, me, cancellationToken))
        {
            return ApiProblems.Create(StatusCodes.Status404NotFound, ChecklistErrors.NotFound);
        }

        var existing = await db.ChecklistProgress.SingleOrDefaultAsync(p => p.TaskId == taskId && p.OwnerId == ownerId, cancellationToken);
        if (existing is not null)
        {
            db.ChecklistProgress.Remove(existing);
            audit.Add(AuditActions.ChecklistUndone, me, ownerId, "task", taskId);
            await db.SaveChangesAsync(cancellationToken);
        }

        return TypedResults.NoContent();
    }

    /// <summary>Der Eigentümer selbst oder eine Vertrauensperson, der die Checkliste gerade ausgeliefert wird.</summary>
    private static async Task<bool> AllowedAsync(MyLifeDbContext db, Guid ownerId, Guid me, CancellationToken cancellationToken) =>
        ownerId == me || await TrustEndpoints.Deliverable(db, me)
            .AnyAsync(g => g.OwnerId == ownerId && g.Kind == GrantKind.Area && g.AreaKey == ObjectCatalog.ChecklistArea, cancellationToken);

    private static Task<bool> IsTaskAsync(MyLifeDbContext db, Guid ownerId, Guid taskId, CancellationToken cancellationToken) =>
        db.Objects.AnyAsync(o => o.Id == taskId && o.OwnerId == ownerId && o.AreaKey == ObjectCatalog.ChecklistArea && !o.IsDeleted, cancellationToken);

    private static Guid Me(ClaimsPrincipal principal) => MyLifeClaims.UserId(principal) ?? throw new InvalidOperationException("Nicht angemeldet.");
}
