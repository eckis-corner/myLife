using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using MyLife.Crypto;
using MyLife.Server.Data;
using MyLife.Server.Infrastructure;
using MyLife.Server.Infrastructure.Admin;
using MyLife.Server.Infrastructure.Auth;
using MyLife.Server.Infrastructure.Mail;
using MyLife.Shared.Family;
using MyLife.Shared.Vault;

namespace MyLife.Server.Api;

/// <summary>
/// Familie (M2.2, Architektur 4.3): Einladungen, Annahme mit Schlüsselbindung, Gegenzeichnungen und gepinnte
/// Fingerprints. Der Server verwahrt nur Chiffrat und öffentliche Schlüssel; prüfen und pinnen tun die Browser.
/// </summary>
internal static partial class FamilyEndpoints
{
    private const string EnrollmentPurpose = "MyLife.Invite.TotpEnrollment.v1";
    private const string ConfirmationPurpose = "MyLife.Invite.TotpConfirmation.v1";
    private static readonly TimeSpan EnrollmentLifetime = TimeSpan.FromMinutes(30);

    public static IEndpointRouteBuilder MapFamilyEndpoints(this IEndpointRouteBuilder app)
    {
        var mine = app.MapGroup("/api/family").RequireAuthorization().RequireRateLimiting(VaultEndpoints.RateLimitPolicy);
        mine.MapGet("/invitations", ListInvitationsAsync);
        mine.MapPost("/invitations", CreateInvitationAsync);
        mine.MapPost("/invitations/{id:guid}/renew", RenewInvitationAsync);
        mine.MapDelete("/invitations/{id:guid}", RevokeInvitationAsync);
        mine.MapGet("/verifications", ListVerificationsAsync);
        mine.MapPost("/invitations/{id:guid}/verify", VerifyInvitationAsync);
        mine.MapGet("/people", ListPeopleAsync);
        mine.MapGet("/pins", ListPinsAsync);
        mine.MapPut("/people/{id:guid}/pin", PinAsync);
        mine.MapPut("/people/{id:guid}/endorsement", EndorseAsync);

        var accept = app.MapGroup("/api/invite").RequireRateLimiting(AuthEndpoints.RateLimitPolicy);
        accept.MapPost("/info", InfoAsync);
        accept.MapPost("/totp", CreateTotpAsync);
        accept.MapPost("/totp/verify", VerifyTotpAsync);
        accept.MapPost("/accept", AcceptAsync);
        return app;
    }

    // ---------- Einladungen der angemeldeten Person ----------

    private static async Task<Ok<List<InvitationDto>>> ListInvitationsAsync(ClaimsPrincipal principal, MyLifeDbContext db, TimeProvider time, CancellationToken cancellationToken)
    {
        var me = Me(principal);
        return TypedResults.Ok(await QueryInvitations(db, time.GetUtcNow(), i => i.InviterId == me).ToListAsync(cancellationToken));
    }

    /// <summary>Alle offenen Einladungen der Instanz, für die Benutzerverwaltung.</summary>
    internal static async Task<List<InvitationDto>> ListOpenAsync(MyLifeDbContext db, DateTimeOffset now, CancellationToken cancellationToken) =>
        await QueryInvitations(db, now, i => i.AcceptedAt == null && i.RevokedAt == null && i.ExpiresAt > now).ToListAsync(cancellationToken);

    private static async Task<Results<Created<InvitationCreatedDto>, ProblemHttpResult>> CreateInvitationAsync(
        CreateInvitationRequest request, HttpContext context, ClaimsPrincipal principal, MyLifeDbContext db, AuditLog audit, MailOutbox outbox,
        SettingsStore settings, TimeProvider time, CancellationToken cancellationToken)
    {
        var me = await db.Users.SingleAsync(u => u.Id == Me(principal), cancellationToken);
        var email = KeyMaterialValidation.NormalizeEmail(request.Email ?? string.Empty);
        if (!KeyMaterialValidation.IsEmail(email) || request.Language is not ("de" or "en") || !IsBlob(request.EncryptedSecret)
            || (request.MailFragment is not null && !MailFragmentPattern().IsMatch(request.MailFragment)))
        {
            return Problem(StatusCodes.Status400BadRequest, FamilyErrors.Invalid);
        }

        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email, cancellationToken))
        {
            return Problem(StatusCodes.Status409Conflict, FamilyErrors.AccountExists);
        }

        var now = time.GetUtcNow();
        var open = await db.Invitations
            .Where(i => i.Email == email && i.AcceptedAt == null && i.RevokedAt == null && i.ExpiresAt > now).ToListAsync(cancellationToken);
        if (open.Any(i => i.InviterId != me.Id))
        {
            return Problem(StatusCodes.Status409Conflict, FamilyErrors.Pending);
        }

        if (await db.Invitations.AnyAsync(i => i.Id == request.Id, cancellationToken))
        {
            return Problem(StatusCodes.Status409Conflict, FamilyErrors.Invalid);
        }

        var stored = await settings.GetAsync(cancellationToken);
        if (request.MailFragment is not null && !SettingsStore.IsMailConfigured(stored))
        {
            return Problem(StatusCodes.Status400BadRequest, FamilyErrors.MailNotConfigured);
        }

        // Eine neue Einladung derselben Person an dieselbe Adresse ersetzt die offene.
        foreach (var previous in open)
        {
            previous.RevokedAt = now;
        }

        var (token, hash) = OneTimeTokens.NewToken();
        var invitation = new Invitation
        {
            Id = request.Id,
            InviterId = me.Id,
            Email = email,
            Language = request.Language,
            TokenHash = hash,
            EncryptedSecret = request.EncryptedSecret,
            CreatedAt = now,
            ExpiresAt = now.AddDays(FamilyLimits.InvitationDays),
        };
        db.Invitations.Add(invitation);
        audit.Own(AuditActions.InvitationCreated, me.Id, "invitation", invitation.Id);
        if (request.MailFragment is not null)
        {
            QueueInvitationMail(outbox, me, invitation, BaseUrl(stored.PublicUrl, context), token, request.MailFragment);
        }

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/family/invitations/{invitation.Id}",
            new InvitationCreatedDto(invitation.Id, token, invitation.ExpiresAt, request.MailFragment is not null));
    }

    /// <summary>Neuer Link (der alte wird ungültig), neue Frist, optional neue Mail.</summary>
    private static async Task<Results<Ok<InvitationCreatedDto>, ProblemHttpResult>> RenewInvitationAsync(
        Guid id, RenewInvitationRequest request, HttpContext context, ClaimsPrincipal principal, MyLifeDbContext db, AuditLog audit,
        MailOutbox outbox, SettingsStore settings, TimeProvider time, CancellationToken cancellationToken)
    {
        var me = await db.Users.SingleAsync(u => u.Id == Me(principal), cancellationToken);
        var invitation = await db.Invitations.SingleOrDefaultAsync(i => i.Id == id && i.InviterId == me.Id && i.AcceptedAt == null && i.RevokedAt == null, cancellationToken);
        if (invitation is null)
        {
            return Problem(StatusCodes.Status404NotFound, FamilyErrors.NotFound);
        }

        if (request.MailFragment is not null && !MailFragmentPattern().IsMatch(request.MailFragment))
        {
            return Problem(StatusCodes.Status400BadRequest, FamilyErrors.Invalid);
        }

        var stored = await settings.GetAsync(cancellationToken);
        if (request.MailFragment is not null && !SettingsStore.IsMailConfigured(stored))
        {
            return Problem(StatusCodes.Status400BadRequest, FamilyErrors.MailNotConfigured);
        }

        var (token, hash) = OneTimeTokens.NewToken();
        invitation.TokenHash = hash;
        invitation.ExpiresAt = time.GetUtcNow().AddDays(FamilyLimits.InvitationDays);
        audit.Own(AuditActions.InvitationRenewed, me.Id, "invitation", invitation.Id);
        if (request.MailFragment is not null)
        {
            QueueInvitationMail(outbox, me, invitation, BaseUrl(stored.PublicUrl, context), token, request.MailFragment);
        }

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(new InvitationCreatedDto(invitation.Id, token, invitation.ExpiresAt, request.MailFragment is not null));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> RevokeInvitationAsync(
        Guid id, ClaimsPrincipal principal, MyLifeDbContext db, AuditLog audit, TimeProvider time, CancellationToken cancellationToken)
    {
        var me = Me(principal);
        var invitation = await db.Invitations.SingleOrDefaultAsync(i => i.Id == id && i.InviterId == me && i.AcceptedAt == null && i.RevokedAt == null, cancellationToken);
        if (invitation is null)
        {
            return Problem(StatusCodes.Status404NotFound, FamilyErrors.NotFound);
        }

        invitation.RevokedAt = time.GetUtcNow();
        audit.Own(AuditActions.InvitationRevoked, me, "invitation", invitation.Id);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>Für die Benutzerverwaltung: eine offene Einladung beliebiger Personen zurückziehen.</summary>
    internal static async Task<bool> RevokeAnyAsync(MyLifeDbContext db, AuditLog audit, Guid adminId, Guid id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var invitation = await db.Invitations.SingleOrDefaultAsync(i => i.Id == id && i.AcceptedAt == null && i.RevokedAt == null, cancellationToken);
        if (invitation is null)
        {
            return false;
        }

        invitation.RevokedAt = now;
        audit.Add(AuditActions.AdminInvitationRevoked, adminId, invitation.InviterId, "invitation", invitation.Id);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ---------- Schlüsselprüfung ----------

    private static async Task<Ok<List<PendingVerificationDto>>> ListVerificationsAsync(ClaimsPrincipal principal, MyLifeDbContext db, CancellationToken cancellationToken)
    {
        var me = Me(principal);
        var pending = await (
            from invitation in db.Invitations
            join invitee in db.Users on invitation.InviteeId equals invitee.Id
            where invitation.InviterId == me && invitation.AcceptedAt != null && invitation.VerifiedAt == null && invitation.MismatchAt == null
                  && invitation.Binding != null
            select new PendingVerificationDto(
                invitation.Id, invitee.Id, invitee.DisplayName, invitee.PublicEncryptionKey, invitee.PublicSigningKey, invitation.Binding!, invitation.EncryptedSecret))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(pending);
    }

    /// <summary>
    /// Ergebnis der Prüfung im Browser der einladenden Person. Bei Erfolg prüft der Server die Gegenzeichnung gegen
    /// den aktuellen Schlüssel und speichert sie mit dem Pin; bei einer Abweichung warnt er im System-Protokoll.
    /// </summary>
    private static async Task<Results<NoContent, ProblemHttpResult>> VerifyInvitationAsync(
        Guid id, VerifyInvitationRequest request, ClaimsPrincipal principal, MyLifeDbContext db, AuditLog audit, TimeProvider time,
        ILoggerFactory loggers, CancellationToken cancellationToken)
    {
        var me = await db.Users.SingleAsync(u => u.Id == Me(principal), cancellationToken);
        var invitation = await db.Invitations.SingleOrDefaultAsync(
            i => i.Id == id && i.InviterId == me.Id && i.AcceptedAt != null && i.VerifiedAt == null && i.MismatchAt == null, cancellationToken);
        var invitee = invitation?.InviteeId is { } inviteeId ? await db.Users.SingleOrDefaultAsync(u => u.Id == inviteeId, cancellationToken) : null;
        if (invitation is null || invitee is null)
        {
            return Problem(StatusCodes.Status404NotFound, FamilyErrors.NotFound);
        }

        var now = time.GetUtcNow();
        if (!request.Matches)
        {
            invitation.MismatchAt = now;
            audit.Add(AuditActions.KeyMismatch, me.Id, me.Id, "user", invitee.Id);
            audit.Add(AuditActions.KeyMismatch, me.Id, null, "user", invitee.Id);
            await db.SaveChangesAsync(cancellationToken);
            var logger = loggers.CreateLogger(typeof(FamilyEndpoints));
            LogMismatch(logger, me.Id, invitee.Id);
            return TypedResults.NoContent();
        }

        if (!IsBlob(request.Pin) || await CheckEndorsementAsync(me, invitee, request.Endorsement) is not null)
        {
            return Problem(StatusCodes.Status400BadRequest, FamilyErrors.Fingerprint);
        }

        invitation.VerifiedAt = now;
        await UpsertEndorsementAsync(db, me.Id, invitee, request.Endorsement!, now, cancellationToken);
        await UpsertPinAsync(db, me.Id, invitee.Id, request.Pin!, now, cancellationToken);
        audit.Own(AuditActions.KeyVerified, me.Id, "user", invitee.Id);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<List<PersonDto>>> ListPeopleAsync(ClaimsPrincipal principal, MyLifeDbContext db, CancellationToken cancellationToken)
    {
        var me = Me(principal);
        var people = await db.Users.AsNoTracking().Where(u => u.Id != me && u.Status == UserStatus.Active).OrderBy(u => u.DisplayName)
            .Select(u => new { u.Id, u.DisplayName, u.Email, u.PublicEncryptionKey, u.PublicSigningKey }).ToListAsync(cancellationToken);
        var ids = people.Select(p => p.Id).ToList();
        var endorsements = await db.KeyEndorsements.AsNoTracking().Where(e => ids.Contains(e.SubjectId)).ToListAsync(cancellationToken);
        return TypedResults.Ok(people.Select(p => new PersonDto(
            p.Id, p.DisplayName, p.Email, p.PublicEncryptionKey, p.PublicSigningKey,
            [.. endorsements.Where(e => e.SubjectId == p.Id).Select(e => new EndorsementDto(e.SignerId, e.Fingerprint, e.Signature, e.CreatedAt))])).ToList());
    }

    private static async Task<Ok<List<PinDto>>> ListPinsAsync(ClaimsPrincipal principal, MyLifeDbContext db, CancellationToken cancellationToken)
    {
        var me = Me(principal);
        return TypedResults.Ok(await db.PinnedKeys.AsNoTracking().Where(p => p.OwnerId == me)
            .Select(p => new PinDto(p.SubjectId, p.EncryptedPin)).ToListAsync(cancellationToken));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> PinAsync(
        Guid id, PinRequest request, ClaimsPrincipal principal, MyLifeDbContext db, TimeProvider time, CancellationToken cancellationToken)
    {
        var me = Me(principal);
        if (id == me || !IsBlob(request.EncryptedPin))
        {
            return Problem(StatusCodes.Status400BadRequest, FamilyErrors.Invalid);
        }

        if (!await db.Users.AnyAsync(u => u.Id == id, cancellationToken))
        {
            return Problem(StatusCodes.Status404NotFound, FamilyErrors.NotFound);
        }

        await UpsertPinAsync(db, me, id, request.EncryptedPin, time.GetUtcNow(), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> EndorseAsync(
        Guid id, EndorseRequest request, ClaimsPrincipal principal, MyLifeDbContext db, AuditLog audit, TimeProvider time, CancellationToken cancellationToken)
    {
        var me = await db.Users.SingleAsync(u => u.Id == Me(principal), cancellationToken);
        var subject = id == me.Id ? null : await db.Users.SingleOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (subject is null)
        {
            return Problem(StatusCodes.Status404NotFound, FamilyErrors.NotFound);
        }

        if (await CheckEndorsementAsync(me, subject, request.Signature) is { } problem)
        {
            return problem;
        }

        await UpsertEndorsementAsync(db, me.Id, subject, request.Signature, time.GetUtcNow(), cancellationToken);
        audit.Own(AuditActions.KeyEndorsed, me.Id, "user", subject.Id);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    // ---------- Annehmen (ohne Anmeldung, mit dem Token aus dem Link) ----------

    private static async Task<Results<Ok<InvitationInfoDto>, ProblemHttpResult>> InfoAsync(
        InvitationTokenRequest request, MyLifeDbContext db, TimeProvider time, CancellationToken cancellationToken)
    {
        var found = await FindOpenAsync(db, request.Token, time.GetUtcNow(), cancellationToken);
        if (found is null)
        {
            return Problem(StatusCodes.Status404NotFound, FamilyErrors.TokenInvalid);
        }

        var inviter = await db.Users.AsNoTracking().SingleAsync(u => u.Id == found.InviterId, cancellationToken);
        return TypedResults.Ok(new InvitationInfoDto(found.Id, inviter.Id, inviter.DisplayName, found.Email, found.Language, found.ExpiresAt));
    }

    private static async Task<Results<Ok<Shared.Setup.TotpEnrollmentDto>, ProblemHttpResult>> CreateTotpAsync(
        InvitationTokenRequest request, MyLifeDbContext db, IDataProtectionProvider protection, TimeProvider time, CancellationToken cancellationToken)
    {
        var found = await FindOpenAsync(db, request.Token, time.GetUtcNow(), cancellationToken);
        if (found is null)
        {
            return Problem(StatusCodes.Status404NotFound, FamilyErrors.TokenInvalid);
        }

        var secret = Base32.Encode(Totp.NewSecret());
        var enrollment = Protector(protection, EnrollmentPurpose).Protect($"{found.Id:N}|{secret}", EnrollmentLifetime);
        return TypedResults.Ok(new Shared.Setup.TotpEnrollmentDto(secret, enrollment));
    }

    private static async Task<Results<Ok<Shared.Setup.TotpConfirmationDto>, ProblemHttpResult>> VerifyTotpAsync(
        InviteTotpVerifyRequest request, MyLifeDbContext db, IDataProtectionProvider protection, TimeProvider time, CancellationToken cancellationToken)
    {
        var found = await FindOpenAsync(db, request.Token, time.GetUtcNow(), cancellationToken);
        if (found is null)
        {
            return Problem(StatusCodes.Status404NotFound, FamilyErrors.TokenInvalid);
        }

        if (Unprotect(protection, EnrollmentPurpose, request.Enrollment, found.Id) is not { } secret)
        {
            return Problem(StatusCodes.Status400BadRequest, FamilyErrors.TotpExpired);
        }

        if (Totp.Verify(Base32.Decode(secret), request.Code, time.GetUtcNow()) is null)
        {
            return Problem(StatusCodes.Status400BadRequest, FamilyErrors.TotpInvalid);
        }

        var confirmation = Protector(protection, ConfirmationPurpose).Protect($"{found.Id:N}|{secret}", EnrollmentLifetime);
        return TypedResults.Ok(new Shared.Setup.TotpConfirmationDto(confirmation));
    }

    /// <summary>
    /// Legt das Konto der eingeladenen Person an (kein Admin), speichert die Schlüsselbindung zur Prüfung durch die
    /// einladende Person und den Pin der einladenden Person im neuen Tresor. Der Link gilt danach nicht mehr.
    /// </summary>
    private static async Task<Results<Created, ValidationProblem, ProblemHttpResult>> AcceptAsync(
        AcceptInvitationRequest request, MyLifeDbContext db, IDataProtectionProvider protection, AuditLog audit, MailOutbox outbox,
        TimeProvider time, ILoggerFactory loggers, CancellationToken cancellationToken)
    {
        if (request.Account is null || request.Binding is not { Length: 32 } || (request.InviterPin is not null && !IsBlob(request.InviterPin)))
        {
            return Problem(StatusCodes.Status400BadRequest, FamilyErrors.Invalid);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var now = time.GetUtcNow();
        var invitation = await FindOpenAsync(db, request.Account.Token, now, cancellationToken);
        if (invitation is null)
        {
            return Problem(StatusCodes.Status404NotFound, FamilyErrors.TokenInvalid);
        }

        var errors = SetupEndpoints.Validate(request.Account);
        if (KeyMaterialValidation.NormalizeEmail(request.Account.Email ?? string.Empty) != invitation.Email)
        {
            errors["Email"] = ["invalid-email"];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        if (Unprotect(protection, ConfirmationPurpose, request.Account.TotpConfirmation, invitation.Id) is not { } totpSecret)
        {
            return Problem(StatusCodes.Status400BadRequest, FamilyErrors.TotpExpired);
        }

        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == invitation.Email, cancellationToken))
        {
            return Problem(StatusCodes.Status409Conflict, FamilyErrors.AccountExists);
        }

        var account = request.Account;
        var user = new User
        {
            Id = Guid.CreateVersion7(now),
            Email = invitation.Email,
            DisplayName = account.DisplayName.Trim(),
            IsAdmin = false,
            Status = UserStatus.Active,
            Language = account.Language,
            AuthHash = await AuthHashHasher.HashAsync(account.AuthHash),
            KdfSalt = account.KdfSalt,
            KdfMemoryKiB = account.KdfMemoryKiB,
            KdfIterations = account.KdfIterations,
            KdfParallelism = account.KdfParallelism,
            WrappedVmk = account.WrappedVmk,
            WrappedVmkByRecovery = account.WrappedVmkByRecovery,
            RecoveryAuthHash = await AuthHashHasher.HashAsync(account.RecoveryAuthHash),
            SecurityStamp = SecurityStamps.New(),
            PublicEncryptionKey = account.PublicEncryptionKey,
            WrappedEncryptionPrivateKey = account.WrappedEncryptionPrivateKey,
            PublicSigningKey = account.PublicSigningKey,
            WrappedSigningPrivateKey = account.WrappedSigningPrivateKey,
            TotpSecret = protection.CreateProtector(SetupEndpoints.TotpSecretPurpose).Protect(totpSecret),
            CreatedAt = now,
        };
        db.Users.Add(user);
        invitation.AcceptedAt = now;
        invitation.InviteeId = user.Id;
        invitation.Binding = request.Binding;
        if (request.InviterPin is not null)
        {
            db.PinnedKeys.Add(new PinnedKey { OwnerId = user.Id, SubjectId = invitation.InviterId, EncryptedPin = request.InviterPin, UpdatedAt = now });
        }

        audit.Add(AuditActions.InvitationAccepted, user.Id, invitation.InviterId, "user", user.Id);
        audit.Add(AuditActions.AccountCreated, invitation.InviterId, null, "user", user.Id);
        var inviter = await db.Users.SingleAsync(u => u.Id == invitation.InviterId, cancellationToken);
        outbox.Enqueue(inviter, MailTemplate.InvitationAccepted, new Dictionary<string, string> { ["invitee"] = user.DisplayName });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Problem(StatusCodes.Status409Conflict, FamilyErrors.AccountExists);
        }

        var logger = loggers.CreateLogger(typeof(FamilyEndpoints));
        LogAccepted(logger, user.Id, invitation.InviterId);
        return TypedResults.Created();
    }

    // ---------- Hilfen ----------

    private static IQueryable<InvitationDto> QueryInvitations(
        MyLifeDbContext db, DateTimeOffset now, System.Linq.Expressions.Expression<Func<Invitation, bool>> filter) =>
        from invitation in db.Invitations.AsNoTracking().Where(filter)
        join inviter in db.Users on invitation.InviterId equals inviter.Id
        orderby invitation.CreatedAt descending
        select new InvitationDto(
            invitation.Id,
            invitation.Email,
            invitation.RevokedAt != null ? InvitationStatus.Revoked
                : invitation.MismatchAt != null ? InvitationStatus.Mismatch
                : invitation.VerifiedAt != null ? InvitationStatus.Verified
                : invitation.AcceptedAt != null ? InvitationStatus.Accepted
                : invitation.ExpiresAt <= now ? InvitationStatus.Expired
                : InvitationStatus.Pending,
            invitation.CreatedAt,
            invitation.ExpiresAt,
            invitation.AcceptedAt,
            invitation.InviteeId,
            db.Users.Where(u => u.Id == invitation.InviteeId).Select(u => u.DisplayName).FirstOrDefault(),
            inviter.DisplayName,
            invitation.EncryptedSecret);

    private static async Task<Invitation?> FindOpenAsync(MyLifeDbContext db, string? token, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (OneTimeTokens.HashOf(token) is not { } hash)
        {
            return null;
        }

        return await db.Invitations.SingleOrDefaultAsync(
            i => i.TokenHash == hash && i.AcceptedAt == null && i.RevokedAt == null && i.ExpiresAt > now, cancellationToken);
    }

    /// <summary>Die Gegenzeichnung muss den aktuellen Fingerprint der Person tragen und vom Unterzeichner stammen.</summary>
    private static async Task<ProblemHttpResult?> CheckEndorsementAsync(User signer, User subject, byte[]? signature)
    {
        if (signature is not { Length: CryptoFormat.SignatureLength })
        {
            return Problem(StatusCodes.Status400BadRequest, FamilyErrors.Invalid);
        }

        await using var crypto = new ReferenceMyLifeCrypto();
        var valid = await crypto.VerifyAsync(signer.PublicSigningKey, Fingerprint(subject), signature, KeyContexts.Endorsement(subject.Id));
        return valid ? null : Problem(StatusCodes.Status400BadRequest, FamilyErrors.Fingerprint);
    }

    internal static byte[] Fingerprint(User user) => SHA256.HashData(KeyBinding.FingerprintData(user.PublicEncryptionKey, user.PublicSigningKey));

    private static async Task UpsertEndorsementAsync(MyLifeDbContext db, Guid signerId, User subject, byte[] signature, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var endorsement = await db.KeyEndorsements.SingleOrDefaultAsync(e => e.SignerId == signerId && e.SubjectId == subject.Id, cancellationToken);
        if (endorsement is null)
        {
            db.KeyEndorsements.Add(new KeyEndorsement { SignerId = signerId, SubjectId = subject.Id, Fingerprint = Fingerprint(subject), Signature = signature, CreatedAt = now });
        }
        else
        {
            endorsement.Fingerprint = Fingerprint(subject);
            endorsement.Signature = signature;
            endorsement.CreatedAt = now;
        }
    }

    private static async Task UpsertPinAsync(MyLifeDbContext db, Guid ownerId, Guid subjectId, byte[] encryptedPin, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var pin = await db.PinnedKeys.SingleOrDefaultAsync(p => p.OwnerId == ownerId && p.SubjectId == subjectId, cancellationToken);
        if (pin is null)
        {
            db.PinnedKeys.Add(new PinnedKey { OwnerId = ownerId, SubjectId = subjectId, EncryptedPin = encryptedPin, UpdatedAt = now });
        }
        else
        {
            pin.EncryptedPin = encryptedPin;
            pin.UpdatedAt = now;
        }
    }

    private static void QueueInvitationMail(MailOutbox outbox, User inviter, Invitation invitation, string baseUrl, string token, string fragment) =>
        outbox.Enqueue(invitation.Email, invitation.Language, MailTemplate.Invitation, new Dictionary<string, string>
        {
            ["inviter"] = inviter.DisplayName,
            ["link"] = $"{baseUrl}/invite#t={token}&{fragment}",
            ["days"] = FamilyLimits.InvitationDays.ToString(System.Globalization.CultureInfo.InvariantCulture),
        });

    private static string BaseUrl(string? publicUrl, HttpContext context) => publicUrl ?? $"{context.Request.Scheme}://{context.Request.Host}";

    private static bool IsBlob(byte[]? data) => data is { Length: > 0 and <= FamilyLimits.MaxBlobBytes };

    private static ITimeLimitedDataProtector Protector(IDataProtectionProvider protection, string purpose) =>
        protection.CreateProtector(purpose).ToTimeLimitedDataProtector();

    /// <summary>Liest eine geschützte TOTP-Angabe, die an diese Einladung gebunden ist.</summary>
    private static string? Unprotect(IDataProtectionProvider protection, string purpose, string? value, Guid invitationId)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        try
        {
            var parts = Protector(protection, purpose).Unprotect(value).Split('|', 2);
            return parts.Length == 2 && parts[0] == invitationId.ToString("N") ? parts[1] : null;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private static Guid Me(ClaimsPrincipal principal) => MyLifeClaims.UserId(principal) ?? throw new InvalidOperationException("Nicht angemeldet.");

    private static ProblemHttpResult Problem(int status, string code) => ApiProblems.Create(status, code);

    /// <summary>Geheimnis (32 Byte, Base64url) und Fingerprint der einladenden Person (SHA-256, hex).</summary>
    [GeneratedRegex("^s=[A-Za-z0-9_-]{43}&f=[0-9a-f]{64}$")]
    private static partial Regex MailFragmentPattern();

    [LoggerMessage(Level = LogLevel.Information, Message = "Einladung angenommen: Konto {UserId}, eingeladen von {InviterId}")]
    private static partial void LogAccepted(ILogger logger, Guid userId, Guid inviterId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Schlüsselbindung passt nicht: {InviterId} hat den Schlüssel von {UserId} nicht bestätigt")]
    private static partial void LogMismatch(ILogger logger, Guid inviterId, Guid userId);
}
