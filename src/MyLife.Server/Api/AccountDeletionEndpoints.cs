using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using MyLife.Server.Data;
using MyLife.Server.Infrastructure;
using MyLife.Server.Infrastructure.Admin;
using MyLife.Server.Infrastructure.Mail;
using MyLife.Shared.Admin;
using MyLife.Shared.Vault;

namespace MyLife.Server.Api;

/// <summary>
/// Ohne Anmeldung: der Datenschutzhinweis und die Bestätigung einer Kontolöschung über den Link aus der Mail
/// (Entscheidung 44). Das Token steht im Fragment des Links und kommt nur im Rumpf eines POST zum Server.
/// </summary>
internal static partial class AccountDeletionEndpoints
{
    public static IEndpointRouteBuilder MapAccountDeletionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/privacy", GetPrivacyAsync);
        var group = app.MapGroup("/api/account-deletion").RequireRateLimiting(AuthEndpoints.RateLimitPolicy);
        group.MapPost("/info", InfoAsync);
        group.MapPost("/confirm", ConfirmAsync);
        return app;
    }

    private static async Task<Ok<PrivacyNoticeDto>> GetPrivacyAsync(SettingsStore settings, CancellationToken cancellationToken)
    {
        var text = (await settings.GetAsync(cancellationToken)).PrivacyNotice;
        return TypedResults.Ok(new PrivacyNoticeDto(text, text is null));
    }

    private static async Task<Results<Ok<AccountDeletionDto>, ProblemHttpResult>> InfoAsync(
        AccountDeletionRequest request, MyLifeDbContext db, TimeProvider time, CancellationToken cancellationToken)
    {
        var token = await OneTimeTokens.FindAsync(db, TokenPurpose.AccountDeletion, request.Token, time.GetUtcNow(), cancellationToken);
        var user = token is null ? null : await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == token.UserId, cancellationToken);
        return user is null
            ? ApiProblems.Create(StatusCodes.Status404NotFound, AdminErrors.TokenInvalid)
            : TypedResults.Ok(new AccountDeletionDto(user.DisplayName, user.Email, token!.ExpiresAt));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ConfirmAsync(
        AccountDeletionRequest request, MyLifeDbContext db, FileStore files, MailOutbox outbox, TimeProvider time, ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var token = await OneTimeTokens.FindAsync(db, TokenPurpose.AccountDeletion, request.Token, time.GetUtcNow(), cancellationToken);
        var user = token is null ? null : await db.Users.SingleOrDefaultAsync(u => u.Id == token.UserId, cancellationToken);
        if (user is null)
        {
            return ApiProblems.Create(StatusCodes.Status404NotFound, AdminErrors.TokenInvalid);
        }

        // Der letzte aktive Admin kann sich nicht selbst entfernen; die Instanz bliebe sonst ohne Verwaltung.
        if (user.IsAdmin && !await db.Users.AnyAsync(u => u.IsAdmin && u.Status == UserStatus.Active && u.Id != user.Id, cancellationToken))
        {
            return ApiProblems.Create(StatusCodes.Status400BadRequest, AdminErrors.LastAdmin);
        }

        await AccountDeletion.DeleteAsync(db, files, user, cancellationToken);
        db.AuditLog.Add(new AuditEntry
        {
            At = time.GetUtcNow(), ActorId = token!.CreatedBy, Action = AuditActions.AccountDeleted, TargetType = "user", TargetId = user.Id,
        });
        outbox.Enqueue(user, MailTemplate.AccountDeleted);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        LogDeleted(loggers.CreateLogger(typeof(AccountDeletionEndpoints)), user.Id);
        return TypedResults.NoContent();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Konto {UserId} mit Tresor gelöscht (vom Nutzer bestätigt)")]
    private static partial void LogDeleted(ILogger logger, Guid userId);
}
