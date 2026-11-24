using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using MyLife.Crypto;
using MyLife.Server.Data;
using MyLife.Server.Infrastructure;
using MyLife.Server.Infrastructure.Auth;
using MyLife.Shared;
using MyLife.Shared.Setup;

namespace MyLife.Server.Api;

/// <summary>
/// Einrichtungsassistent (M1.2): legt den ersten Nutzer als Administrator an. Alle Aufrufe außer dem Status
/// brauchen den Einrichtungscode aus dem Protokoll und funktionieren nur, solange es keinen Nutzer gibt.
/// </summary>
internal static partial class SetupEndpoints
{
    public const string RateLimitPolicy = "setup";
    internal const string CompletedSettingKey = "setup:completed-at";

    private const string EnrollmentPurpose = "MyLife.Setup.TotpEnrollment.v1";
    private const string ConfirmationPurpose = "MyLife.Setup.TotpConfirmation.v1";
    internal const string TotpSecretPurpose = "MyLife.Users.TotpSecret.v1";
    private static readonly TimeSpan EnrollmentLifetime = TimeSpan.FromMinutes(30);

    public static IEndpointRouteBuilder MapSetupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/setup").RequireRateLimiting(RateLimitPolicy);
        group.MapGet("/status", GetStatusAsync);
        group.MapPost("/token", VerifyTokenAsync);
        group.MapPost("/totp", CreateTotpAsync);
        group.MapPost("/totp/verify", VerifyTotpAsync);
        group.MapPost("/complete", CompleteAsync);
        return app;
    }

    private static async Task<Ok<SetupStatusDto>> GetStatusAsync(MyLifeDbContext db, CancellationToken cancellationToken) =>
        TypedResults.Ok(new SetupStatusDto(!await AnyUserAsync(db, cancellationToken)));

    private static async Task<Results<NoContent, ProblemHttpResult>> VerifyTokenAsync(
        SetupTokenRequest request, MyLifeDbContext db, SetupTokenService tokens, CancellationToken cancellationToken) =>
        await CheckAccessAsync(request.Token, db, tokens, cancellationToken) is { } problem ? problem : TypedResults.NoContent();

    private static async Task<Results<Ok<TotpEnrollmentDto>, ProblemHttpResult>> CreateTotpAsync(
        SetupTokenRequest request, MyLifeDbContext db, SetupTokenService tokens, IDataProtectionProvider protection,
        CancellationToken cancellationToken)
    {
        if (await CheckAccessAsync(request.Token, db, tokens, cancellationToken) is { } problem)
        {
            return problem;
        }

        var secret = Base32.Encode(Totp.NewSecret());
        var enrollment = protection.CreateProtector(EnrollmentPurpose).ToTimeLimitedDataProtector().Protect(secret, EnrollmentLifetime);
        return TypedResults.Ok(new TotpEnrollmentDto(secret, enrollment));
    }

    private static async Task<Results<Ok<TotpConfirmationDto>, ProblemHttpResult>> VerifyTotpAsync(
        TotpVerifyRequest request, MyLifeDbContext db, SetupTokenService tokens, IDataProtectionProvider protection,
        TimeProvider time, CancellationToken cancellationToken)
    {
        if (await CheckAccessAsync(request.Token, db, tokens, cancellationToken) is { } problem)
        {
            return problem;
        }

        if (Unprotect(protection, EnrollmentPurpose, request.Enrollment) is not { } secret)
        {
            return Problem(StatusCodes.Status400BadRequest, "setup-totp-expired");
        }

        if (Totp.Verify(Base32.Decode(secret), request.Code, time.GetUtcNow()) is null)
        {
            return Problem(StatusCodes.Status400BadRequest, "setup-totp-invalid-code");
        }

        var confirmation = protection.CreateProtector(ConfirmationPurpose).ToTimeLimitedDataProtector().Protect(secret, EnrollmentLifetime);
        return TypedResults.Ok(new TotpConfirmationDto(confirmation));
    }

    private static async Task<Results<Created, ValidationProblem, ProblemHttpResult>> CompleteAsync(
        SetupCompleteRequest request, MyLifeDbContext db, SetupTokenService tokens, IDataProtectionProvider protection,
        TimeProvider time, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
    {
        if (await CheckAccessAsync(request.Token, db, tokens, cancellationToken) is { } problem)
        {
            return problem;
        }

        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        if (Unprotect(protection, ConfirmationPurpose, request.TotpConfirmation) is not { } totpSecret)
        {
            return Problem(StatusCodes.Status400BadRequest, "setup-totp-expired");
        }

        var now = time.GetUtcNow();
        var user = new User
        {
            Id = Guid.CreateVersion7(now),
            Email = KeyMaterialValidation.NormalizeEmail(request.Email),
            DisplayName = request.DisplayName.Trim(),
            IsAdmin = true,
            Status = UserStatus.Active,
            Language = request.Language,
            AuthHash = await AuthHashHasher.HashAsync(request.AuthHash),
            KdfSalt = request.KdfSalt,
            KdfMemoryKiB = request.KdfMemoryKiB,
            KdfIterations = request.KdfIterations,
            KdfParallelism = request.KdfParallelism,
            WrappedVmk = request.WrappedVmk,
            WrappedVmkByRecovery = request.WrappedVmkByRecovery,
            RecoveryAuthHash = await AuthHashHasher.HashAsync(request.RecoveryAuthHash),
            SecurityStamp = SecurityStamps.New(),
            PublicEncryptionKey = request.PublicEncryptionKey,
            WrappedEncryptionPrivateKey = request.WrappedEncryptionPrivateKey,
            PublicSigningKey = request.PublicSigningKey,
            WrappedSigningPrivateKey = request.WrappedSigningPrivateKey,
            TotpSecret = protection.CreateProtector(TotpSecretPurpose).Protect(totpSecret),
            CreatedAt = now,
        };

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (await AnyUserAsync(db, cancellationToken))
        {
            return Problem(StatusCodes.Status409Conflict, "setup-already-completed");
        }

        db.Users.Add(user);
        // Primärschlüssel als Riegel: Zwei gleichzeitige Abschlüsse können nicht beide diesen Eintrag anlegen.
        db.SystemSettings.Add(new SystemSetting { Key = CompletedSettingKey, Value = now.ToString("O"), UpdatedAt = now });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Problem(StatusCodes.Status409Conflict, "setup-already-completed");
        }

        tokens.Revoke();
        var logger = loggerFactory.CreateLogger(typeof(SetupEndpoints));
        LogSetupCompleted(logger, user.Id);
        return TypedResults.Created();
    }

    private static async Task<ProblemHttpResult?> CheckAccessAsync(string? token, MyLifeDbContext db, SetupTokenService tokens, CancellationToken cancellationToken)
    {
        if (await AnyUserAsync(db, cancellationToken))
        {
            return Problem(StatusCodes.Status409Conflict, "setup-already-completed");
        }

        return tokens.Verify(token) ? null : Problem(StatusCodes.Status403Forbidden, "setup-invalid-token");
    }

    private static Task<bool> AnyUserAsync(MyLifeDbContext db, CancellationToken cancellationToken) =>
        db.Users.IgnoreQueryFilters().AnyAsync(cancellationToken);

    private static ProblemHttpResult Problem(int status, string code) => ApiProblems.Create(status, code);

    private static string? Unprotect(IDataProtectionProvider protection, string purpose, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        try
        {
            return protection.CreateProtector(purpose).ToTimeLimitedDataProtector().Unprotect(value);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    /// <summary>Prüft Eingaben und den Aufbau des Schlüsselmaterials. Inhalte kann der Server nicht prüfen, nur die Form.</summary>
    internal static Dictionary<string, string[]> Validate(SetupCompleteRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        void Check(bool valid, string field, string code)
        {
            if (!valid)
            {
                errors[field] = [code];
            }
        }

        Check(KeyMaterialValidation.IsEmail(request.Email), nameof(request.Email), "invalid-email");
        var displayName = request.DisplayName?.Trim() ?? string.Empty;
        Check(displayName.Length is > 0 and <= SetupLimits.MaxDisplayNameLength, nameof(request.DisplayName), "invalid-display-name");
        Check(SupportedCultures.All.Contains(request.Language), nameof(request.Language), "invalid-language");

        Check(KeyMaterialValidation.IsSalt(request.KdfSalt), nameof(request.KdfSalt), "invalid-salt");
        Check(new Argon2Parameters(request.KdfMemoryKiB, request.KdfIterations, request.KdfParallelism).IsValid, nameof(request.KdfMemoryKiB), "invalid-kdf-parameters");
        Check(KeyMaterialValidation.IsAuthHash(request.AuthHash), nameof(request.AuthHash), "invalid-auth-hash");
        Check(KeyMaterialValidation.IsAuthHash(request.RecoveryAuthHash), nameof(request.RecoveryAuthHash), "invalid-auth-hash");

        Check(KeyMaterialValidation.IsWrappedSecretKey(request.WrappedVmk), nameof(request.WrappedVmk), "invalid-wrapped-key");
        Check(KeyMaterialValidation.IsWrappedSecretKey(request.WrappedVmkByRecovery), nameof(request.WrappedVmkByRecovery), "invalid-wrapped-key");
        Check(KeyMaterialValidation.IsWrappedPrivateKey(request.WrappedEncryptionPrivateKey, CryptoKind.WrappedEcdhKey), nameof(request.WrappedEncryptionPrivateKey), "invalid-wrapped-key");
        Check(KeyMaterialValidation.IsWrappedPrivateKey(request.WrappedSigningPrivateKey, CryptoKind.WrappedEcdsaKey), nameof(request.WrappedSigningPrivateKey), "invalid-wrapped-key");
        Check(KeyMaterialValidation.IsPublicKey(request.PublicEncryptionKey), nameof(request.PublicEncryptionKey), "invalid-public-key");
        Check(KeyMaterialValidation.IsPublicKey(request.PublicSigningKey), nameof(request.PublicSigningKey), "invalid-public-key");
        return errors;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Einrichtung abgeschlossen, erster Administrator {UserId} angelegt")]
    private static partial void LogSetupCompleted(ILogger logger, Guid userId);
}
