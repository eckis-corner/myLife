using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using MyLife.Crypto;
using MyLife.Server.Data;
using MyLife.Server.Infrastructure;
using MyLife.Server.Infrastructure.Admin;
using MyLife.Server.Infrastructure.Auth;
using MyLife.Shared.Auth;
using MyLife.Shared.Vault;

namespace MyLife.Server.Api;

/// <summary>
/// Anmeldung (Architektur 4.4): Voranmeldung (Salt), Passwort (Anmeldehash), zweiter Faktor (TOTP oder Passkey),
/// Sitzung, Abmeldung und Wiederherstellung mit dem Recovery-Key. Der Server sieht nie Passwort oder Recovery-Key.
/// </summary>
internal static partial class AuthEndpoints
{
    public const string RateLimitPolicy = "auth";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").RequireRateLimiting(RateLimitPolicy);
        group.MapPost("/prelogin", PreloginAsync);
        group.MapPost("/login", LoginAsync);
        group.MapPost("/login/totp", LoginTotpAsync);
        group.MapPost("/login/totp/setup", LoginTotpSetupAsync);
        group.MapPost("/login/totp/enroll", LoginTotpEnrollAsync);
        group.MapPost("/login/passkey/options", PasskeyOptionsAsync);
        group.MapPost("/login/passkey", LoginPasskeyAsync);
        group.MapGet("/session", GetSessionAsync).RequireAuthorization();
        group.MapPost("/logout", (Delegate)LogoutAsync);
        group.MapPost("/recovery", StartRecoveryAsync);
        group.MapPost("/recovery/complete", CompleteRecoveryAsync);
        return app;
    }

    private static async Task<Ok<PreloginDto>> PreloginAsync(PreloginRequest request, MyLifeDbContext db, PreloginDecoy decoy, CancellationToken cancellationToken)
    {
        var email = KeyMaterialValidation.NormalizeEmail(request.Email ?? string.Empty);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email && u.Status != UserStatus.Invited, cancellationToken);
        if (user is null)
        {
            var parameters = Argon2Parameters.Default;
            return TypedResults.Ok(new PreloginDto(await decoy.SaltForAsync(email, cancellationToken), parameters.MemoryKiB, parameters.Iterations, parameters.Parallelism));
        }

        return TypedResults.Ok(new PreloginDto(user.KdfSalt, user.KdfMemoryKiB, user.KdfIterations, user.KdfParallelism));
    }

    private static async Task<Results<Ok<LoginChallengeDto>, ProblemHttpResult>> LoginAsync(
        LoginRequest request, MyLifeDbContext db, LoginAttempts attempts, PendingTokens tokens, ILoggerFactory loggers, CancellationToken cancellationToken)
    {
        if (!KeyMaterialValidation.IsAuthHash(request.AuthHash))
        {
            return ApiProblems.Create(StatusCodes.Status400BadRequest, AuthErrors.InvalidInput);
        }

        var user = await db.Users.Include(u => u.Passkeys)
            .SingleOrDefaultAsync(u => u.Email == KeyMaterialValidation.NormalizeEmail(request.Email ?? string.Empty) && u.Status != UserStatus.Invited, cancellationToken);
        if (user is null)
        {
            await DummyAuthHash.BurnAsync(request.AuthHash);
            return InvalidCredentials();
        }

        if (attempts.IsLocked(user))
        {
            return Locked();
        }

        if (!await AuthHashHasher.VerifyAsync(user.AuthHash, request.AuthHash))
        {
            attempts.Failed(user, db);
            await db.SaveChangesAsync(cancellationToken);
            var logger = loggers.CreateLogger(typeof(AuthEndpoints));
            LogLoginFailed(logger, user.Id);
            return InvalidCredentials();
        }

        // Erst nach dem richtigen Passwort: Ein gesperrtes Konto erfährt nur, wer das Passwort kennt.
        if (user.Status == UserStatus.Locked)
        {
            return ApiProblems.Create(StatusCodes.Status403Forbidden, AuthErrors.Disabled);
        }

        return TypedResults.Ok(new LoginChallengeDto(tokens.Issue(PendingPurpose.SecondFactor, user), user.TotpSecret is not null, user.Passkeys.Count > 0));
    }

    private static async Task<Results<Ok<SessionDto>, ProblemHttpResult>> LoginTotpAsync(
        TotpLoginRequest request, HttpContext context, MyLifeDbContext db, LoginAttempts attempts, PendingTokens tokens,
        IDataProtectionProvider protection, TimeProvider time, SettingsStore settings, CancellationToken cancellationToken)
    {
        var (user, problem) = await ReadPendingAsync(db, attempts, tokens, PendingPurpose.SecondFactor, request.Pending, cancellationToken);
        if (user is null)
        {
            return problem!;
        }

        if (user.TotpSecret is null)
        {
            return ApiProblems.Create(StatusCodes.Status400BadRequest, AuthErrors.InvalidCode);
        }

        var secret = Base32.Decode(protection.CreateProtector(SetupEndpoints.TotpSecretPurpose).Unprotect(user.TotpSecret));
        var step = Totp.Verify(secret, request.Code, time.GetUtcNow());
        // Jeder Code gilt nur einmal: Schritte bis zum zuletzt verwendeten werden abgelehnt.
        if (step is null || step <= user.TotpLastStep)
        {
            attempts.Failed(user, db);
            await db.SaveChangesAsync(cancellationToken);
            return ApiProblems.Create(StatusCodes.Status401Unauthorized, AuthErrors.InvalidCode);
        }

        user.TotpLastStep = step;
        return TypedResults.Ok(await CompleteSignInAsync(context, db, attempts, user, time, settings, cancellationToken));
    }

    private static async Task<Results<Ok<PasskeyOptionsDto>, ProblemHttpResult>> PasskeyOptionsAsync(
        PasskeyLoginOptionsRequest request, HttpContext context, MyLifeDbContext db, LoginAttempts attempts, PendingTokens tokens,
        PasskeyService passkeys, CancellationToken cancellationToken)
    {
        var (user, problem) = await ReadPendingAsync(db, attempts, tokens, PendingPurpose.SecondFactor, request.Pending, cancellationToken);
        if (user is null)
        {
            return problem!;
        }

        if (user.Passkeys.Count == 0)
        {
            return ApiProblems.Create(StatusCodes.Status400BadRequest, AuthErrors.PasskeyFailed);
        }

        var (json, token) = passkeys.BeginLogin(context, user);
        return TypedResults.Ok(new PasskeyOptionsDto(json, token));
    }

    private static async Task<Results<Ok<SessionDto>, ProblemHttpResult>> LoginPasskeyAsync(
        PasskeyLoginRequest request, HttpContext context, MyLifeDbContext db, LoginAttempts attempts, PendingTokens tokens,
        PasskeyService passkeys, TimeProvider time, SettingsStore settings, CancellationToken cancellationToken)
    {
        var (user, problem) = await ReadPendingAsync(db, attempts, tokens, PendingPurpose.SecondFactor, request.Pending, cancellationToken);
        if (user is null)
        {
            return problem!;
        }

        var passkey = await passkeys.CompleteLoginAsync(context, user, request.OptionsToken, request.AssertionJson, cancellationToken);
        if (passkey is null)
        {
            attempts.Failed(user, db);
            await db.SaveChangesAsync(cancellationToken);
            return ApiProblems.Create(StatusCodes.Status401Unauthorized, AuthErrors.PasskeyFailed);
        }

        passkey.LastUsedAt = time.GetUtcNow();
        return TypedResults.Ok(await CompleteSignInAsync(context, db, attempts, user, time, settings, cancellationToken));
    }

    /// <summary>
    /// Nach einem 2FA-Reset durch den Admin (weder TOTP noch Passkey): neues TOTP-Geheimnis für die Einrichtung
    /// während der Anmeldung. Das Geheimnis ist geschützt an Nutzer und Stempel gebunden und 10 Minuten gültig.
    /// </summary>
    private static async Task<Results<Ok<TotpLoginSetupDto>, ProblemHttpResult>> LoginTotpSetupAsync(
        TotpLoginSetupRequest request, MyLifeDbContext db, LoginAttempts attempts, PendingTokens tokens, IDataProtectionProvider protection,
        CancellationToken cancellationToken)
    {
        var (user, problem) = await ReadPendingAsync(db, attempts, tokens, PendingPurpose.SecondFactor, request.Pending, cancellationToken);
        if (user is null)
        {
            return problem!;
        }

        if (user.TotpSecret is not null || user.Passkeys.Count > 0)
        {
            return ApiProblems.Create(StatusCodes.Status400BadRequest, AuthErrors.InvalidInput);
        }

        var secret = Base32.Encode(Totp.NewSecret());
        var enrollment = EnrollmentProtector(protection).Protect($"{user.Id:N}|{user.SecurityStamp}|{secret}", TimeSpan.FromMinutes(10));
        return TypedResults.Ok(new TotpLoginSetupDto(secret, enrollment));
    }

    private static async Task<Results<Ok<SessionDto>, ProblemHttpResult>> LoginTotpEnrollAsync(
        TotpLoginEnrollRequest request, HttpContext context, MyLifeDbContext db, LoginAttempts attempts, PendingTokens tokens,
        IDataProtectionProvider protection, TimeProvider time, SettingsStore settings, AuditLog audit, CancellationToken cancellationToken)
    {
        var (user, problem) = await ReadPendingAsync(db, attempts, tokens, PendingPurpose.SecondFactor, request.Pending, cancellationToken);
        if (user is null)
        {
            return problem!;
        }

        string[] parts;
        try
        {
            parts = EnrollmentProtector(protection).Unprotect(request.Enrollment ?? string.Empty).Split('|', 3);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return ApiProblems.Create(StatusCodes.Status401Unauthorized, AuthErrors.Expired);
        }

        if (parts.Length != 3 || parts[0] != user.Id.ToString("N") || parts[1] != user.SecurityStamp || user.TotpSecret is not null || user.Passkeys.Count > 0)
        {
            return ApiProblems.Create(StatusCodes.Status401Unauthorized, AuthErrors.Expired);
        }

        var step = Totp.Verify(Base32.Decode(parts[2]), request.Code, time.GetUtcNow());
        if (step is null)
        {
            attempts.Failed(user, db);
            await db.SaveChangesAsync(cancellationToken);
            return ApiProblems.Create(StatusCodes.Status401Unauthorized, AuthErrors.InvalidCode);
        }

        user.TotpSecret = protection.CreateProtector(SetupEndpoints.TotpSecretPurpose).Protect(parts[2]);
        user.TotpLastStep = step;
        audit.Own(AuditActions.TotpEnrolled, user.Id);
        return TypedResults.Ok(await CompleteSignInAsync(context, db, attempts, user, time, settings, cancellationToken));
    }

    private static ITimeLimitedDataProtector EnrollmentProtector(IDataProtectionProvider protection) =>
        protection.CreateProtector("MyLife.Auth.TotpReenrollment.v1").ToTimeLimitedDataProtector();

    private static async Task<Results<Ok<SessionDto>, UnauthorizedHttpResult>> GetSessionAsync(
        ClaimsPrincipal principal, MyLifeDbContext db, SettingsStore settings, CancellationToken cancellationToken)
    {
        var id = MyLifeClaims.UserId(principal);
        var user = id is null ? null : await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, cancellationToken);
        return user is null ? TypedResults.Unauthorized() : TypedResults.Ok(ToSession(user, (await settings.GetAsync(cancellationToken)).IdleTimeoutMinutes));
    }

    private static async Task<NoContent> LogoutAsync(HttpContext context, MyLifeDbContext db, AuditLog audit)
    {
        if (MyLifeClaims.UserId(context.User) is { } id)
        {
            audit.Own(AuditActions.Logout, id);
            await db.SaveChangesAsync(context.RequestAborted);
        }

        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<RecoveryChallengeDto>, ProblemHttpResult>> StartRecoveryAsync(
        RecoveryRequest request, MyLifeDbContext db, LoginAttempts attempts, PendingTokens tokens, ILoggerFactory loggers, CancellationToken cancellationToken)
    {
        if (!KeyMaterialValidation.IsAuthHash(request.RecoveryAuthHash))
        {
            return ApiProblems.Create(StatusCodes.Status400BadRequest, AuthErrors.InvalidInput);
        }

        var user = await FindActiveAsync(db, KeyMaterialValidation.NormalizeEmail(request.Email ?? string.Empty), cancellationToken);
        if (user?.RecoveryAuthHash is null)
        {
            await DummyAuthHash.BurnAsync(request.RecoveryAuthHash);
            return InvalidCredentials();
        }

        if (attempts.IsLocked(user))
        {
            return Locked();
        }

        if (!await AuthHashHasher.VerifyAsync(user.RecoveryAuthHash, request.RecoveryAuthHash))
        {
            attempts.Failed(user, db);
            await db.SaveChangesAsync(cancellationToken);
            return InvalidCredentials();
        }

        var logger = loggers.CreateLogger(typeof(AuthEndpoints));
        LogRecoveryStarted(logger, user.Id);
        return TypedResults.Ok(new RecoveryChallengeDto(tokens.Issue(PendingPurpose.Recovery, user), user.WrappedVmkByRecovery));
    }

    private static async Task<Results<NoContent, ValidationProblem, ProblemHttpResult>> CompleteRecoveryAsync(
        RecoveryCompleteRequest request, MyLifeDbContext db, LoginAttempts attempts, PendingTokens tokens, AuditLog audit, ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        var (user, problem) = await ReadPendingAsync(db, attempts, tokens, PendingPurpose.Recovery, request.Pending, cancellationToken);
        if (user is null)
        {
            return problem!;
        }

        var errors = KeyMaterialValidation.Validate(request.NewPassword);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        await ApplyPasswordAsync(user, request.NewPassword);
        LoginAttempts.Succeeded(user);
        audit.Add(AuditActions.Recovered, null, user.Id);
        await db.SaveChangesAsync(cancellationToken);
        var logger = loggers.CreateLogger(typeof(AuthEndpoints));
        LogRecoveryCompleted(logger, user.Id);
        return TypedResults.NoContent();
    }

    /// <summary>Setzt ein neues Passwort und einen neuen Sicherheitsstempel; alle anderen Sitzungen enden damit.</summary>
    internal static async Task ApplyPasswordAsync(User user, PasswordMaterial material)
    {
        user.KdfSalt = material.KdfSalt;
        user.KdfMemoryKiB = material.KdfMemoryKiB;
        user.KdfIterations = material.KdfIterations;
        user.KdfParallelism = material.KdfParallelism;
        user.AuthHash = await AuthHashHasher.HashAsync(material.AuthHash);
        user.WrappedVmk = material.WrappedVmk;
        user.SecurityStamp = SecurityStamps.New();
    }

    internal static SessionDto ToSession(User user, int idleTimeoutMinutes) => new()
    {
        UserId = user.Id,
        Email = user.Email,
        DisplayName = user.DisplayName,
        IsAdmin = user.IsAdmin,
        Language = user.Language,
        IdleTimeoutMinutes = idleTimeoutMinutes,
        KdfSalt = user.KdfSalt,
        KdfMemoryKiB = user.KdfMemoryKiB,
        KdfIterations = user.KdfIterations,
        KdfParallelism = user.KdfParallelism,
        WrappedVmk = user.WrappedVmk,
        WrappedVmkByRecovery = user.WrappedVmkByRecovery,
        PublicEncryptionKey = user.PublicEncryptionKey,
        WrappedEncryptionPrivateKey = user.WrappedEncryptionPrivateKey,
        PublicSigningKey = user.PublicSigningKey,
        WrappedSigningPrivateKey = user.WrappedSigningPrivateKey,
    };

    private static async Task<SessionDto> CompleteSignInAsync(
        HttpContext context, MyLifeDbContext db, LoginAttempts attempts, User user, TimeProvider time, SettingsStore settings, CancellationToken cancellationToken)
    {
        LoginAttempts.Succeeded(user);
        user.LastLoginAt = time.GetUtcNow();
        // Jede Anmeldung ist ein Lebenszeichen (Architektur 5.1).
        await context.RequestServices.GetRequiredService<MyLife.Server.Infrastructure.Emergency.EmergencyEngine>().LifesignAsync(user, "login", cancellationToken);
        db.AuditLog.Add(AuditLog.Entry(AuditActions.Login, user.Id, user.Id, time.GetUtcNow(), context.Connection.RemoteIpAddress));
        await db.SaveChangesAsync(cancellationToken);
        await UserSignIn.SignInAsync(context, user);
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(AuthEndpoints));
        LogLoginSucceeded(logger, user.Id);
        return ToSession(user, (await settings.GetAsync(cancellationToken)).IdleTimeoutMinutes);
    }

    private static async Task<(User? User, ProblemHttpResult? Problem)> ReadPendingAsync(
        MyLifeDbContext db, LoginAttempts attempts, PendingTokens tokens, PendingPurpose purpose, string? token, CancellationToken cancellationToken)
    {
        if (tokens.Read(purpose, token) is not { } pending)
        {
            return (null, ApiProblems.Create(StatusCodes.Status401Unauthorized, AuthErrors.Expired));
        }

        var user = await db.Users.Include(u => u.Passkeys)
            .SingleOrDefaultAsync(u => u.Id == pending.UserId && u.Status == UserStatus.Active, cancellationToken);
        if (user is null || user.SecurityStamp != pending.Stamp)
        {
            return (null, ApiProblems.Create(StatusCodes.Status401Unauthorized, AuthErrors.Expired));
        }

        return attempts.IsLocked(user) ? (null, Locked()) : (user, null);
    }

    private static Task<User?> FindActiveAsync(MyLifeDbContext db, string email, CancellationToken cancellationToken) =>
        db.Users.SingleOrDefaultAsync(u => u.Email == email && u.Status == UserStatus.Active, cancellationToken);

    private static ProblemHttpResult InvalidCredentials() => ApiProblems.Create(StatusCodes.Status401Unauthorized, AuthErrors.InvalidCredentials);

    private static ProblemHttpResult Locked() => ApiProblems.Create(StatusCodes.Status429TooManyRequests, AuthErrors.Locked);

    [LoggerMessage(Level = LogLevel.Information, Message = "Anmeldung von {UserId}")]
    private static partial void LogLoginSucceeded(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falsches Passwort für {UserId}")]
    private static partial void LogLoginFailed(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Wiederherstellung mit Recovery-Key begonnen für {UserId}")]
    private static partial void LogRecoveryStarted(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Neues Passwort per Recovery-Key gesetzt für {UserId}")]
    private static partial void LogRecoveryCompleted(ILogger logger, Guid userId);
}
