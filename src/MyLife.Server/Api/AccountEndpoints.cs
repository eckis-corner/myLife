using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using MyLife.Server.Data;
using MyLife.Server.Infrastructure;
using MyLife.Server.Infrastructure.Auth;
using MyLife.Shared.Auth;

namespace MyLife.Server.Api;

/// <summary>Eigenes Konto: Passwort ändern und Passkeys verwalten. Nur angemeldet.</summary>
internal static class AccountEndpoints
{
    public const int MaxPasskeyNameLength = 100;

    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/account").RequireAuthorization().RequireRateLimiting(AuthEndpoints.RateLimitPolicy);
        group.MapPost("/password", ChangePasswordAsync);
        group.MapGet("/passkeys", ListPasskeysAsync);
        group.MapPost("/passkeys/options", PasskeyOptionsAsync);
        group.MapPost("/passkeys", RegisterPasskeyAsync);
        group.MapDelete("/passkeys/{id:guid}", DeletePasskeyAsync);
        return app;
    }

    private static async Task<Results<NoContent, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult>> ChangePasswordAsync(
        ChangePasswordRequest request, HttpContext context, ClaimsPrincipal principal, MyLifeDbContext db, LoginAttempts attempts,
        CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(principal, db, cancellationToken);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        if (attempts.IsLocked(user))
        {
            return ApiProblems.Create(StatusCodes.Status429TooManyRequests, AuthErrors.Locked);
        }

        if (!KeyMaterialValidation.IsAuthHash(request.CurrentAuthHash) || !await AuthHashHasher.VerifyAsync(user.AuthHash, request.CurrentAuthHash))
        {
            attempts.Failed(user);
            await db.SaveChangesAsync(cancellationToken);
            return ApiProblems.Create(StatusCodes.Status400BadRequest, AuthErrors.InvalidCredentials);
        }

        var errors = KeyMaterialValidation.Validate(request.NewPassword);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        await AuthEndpoints.ApplyPasswordAsync(user, request.NewPassword);
        LoginAttempts.Succeeded(user);
        await db.SaveChangesAsync(cancellationToken);
        // Diese Sitzung bleibt mit dem neuen Stempel bestehen, alle anderen enden.
        await UserSignIn.SignInAsync(context, user);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<List<PasskeyDto>>> ListPasskeysAsync(ClaimsPrincipal principal, MyLifeDbContext db, CancellationToken cancellationToken)
    {
        var id = MyLifeClaims.UserId(principal);
        var passkeys = await db.Passkeys.AsNoTracking().Where(p => p.UserId == id).OrderBy(p => p.CreatedAt)
            .Select(p => new PasskeyDto(p.Id, p.Name, p.CreatedAt, p.LastUsedAt))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(passkeys);
    }

    private static async Task<Results<Ok<PasskeyOptionsDto>, UnauthorizedHttpResult>> PasskeyOptionsAsync(
        HttpContext context, ClaimsPrincipal principal, MyLifeDbContext db, PasskeyService passkeys, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(principal, db, cancellationToken);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        var (json, token) = passkeys.BeginRegistration(context, user);
        return TypedResults.Ok(new PasskeyOptionsDto(json, token));
    }

    private static async Task<Results<Created<PasskeyDto>, ProblemHttpResult, UnauthorizedHttpResult>> RegisterPasskeyAsync(
        PasskeyRegisterRequest request, HttpContext context, ClaimsPrincipal principal, MyLifeDbContext db, PasskeyService passkeys,
        TimeProvider time, CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(principal, db, cancellationToken);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length is 0 or > MaxPasskeyNameLength)
        {
            return ApiProblems.Create(StatusCodes.Status400BadRequest, AuthErrors.InvalidInput);
        }

        var passkey = await passkeys.CompleteRegistrationAsync(
            context, user, request.OptionsToken, request.AttestationJson,
            async (credentialId, ct) => !await db.Passkeys.IgnoreQueryFilters().AnyAsync(p => p.CredentialId == credentialId, ct),
            cancellationToken);
        if (passkey is null)
        {
            return ApiProblems.Create(StatusCodes.Status400BadRequest, AuthErrors.PasskeyFailed);
        }

        passkey.Name = name;
        passkey.CreatedAt = time.GetUtcNow();
        db.Passkeys.Add(passkey);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/account/passkeys/{passkey.Id}", new PasskeyDto(passkey.Id, passkey.Name, passkey.CreatedAt, null));
    }

    private static async Task<Results<NoContent, NotFound>> DeletePasskeyAsync(
        Guid id, ClaimsPrincipal principal, MyLifeDbContext db, CancellationToken cancellationToken)
    {
        var userId = MyLifeClaims.UserId(principal);
        var deleted = await db.Passkeys.Where(p => p.Id == id && p.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        return deleted == 0 ? TypedResults.NotFound() : TypedResults.NoContent();
    }

    private static async Task<User?> CurrentUserAsync(ClaimsPrincipal principal, MyLifeDbContext db, CancellationToken cancellationToken)
    {
        var id = MyLifeClaims.UserId(principal);
        return id is null ? null : await db.Users.Include(u => u.Passkeys).SingleOrDefaultAsync(u => u.Id == id, cancellationToken);
    }
}
