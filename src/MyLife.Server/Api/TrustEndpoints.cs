using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using MyLife.Crypto;
using MyLife.Server.Data;
using MyLife.Server.Infrastructure;
using MyLife.Server.Infrastructure.Auth;
using MyLife.Server.Infrastructure.Mail;
using MyLife.Shared.Trust;
using MyLife.Shared.Vault;

namespace MyLife.Server.Api;

/// <summary>
/// Vertrauenspersonen und Freigaben (M2.3, Architektur 4.2): Der Browser des Eigentümers versiegelt und signiert
/// die Umschläge; der Server speichert sie und liefert sie erst aus, wenn der Modus gilt. „Sofort teilen“ (Live)
/// gilt sofort. Der Server prüft nur Form, Zugehörigkeit und dass jeder Umschlag zum aktuellen Schlüssel der Person passt.
/// </summary>
internal static class TrustEndpoints
{
    public static IEndpointRouteBuilder MapTrustEndpoints(this IEndpointRouteBuilder app)
    {
        var trust = app.MapGroup("/api/trust").RequireAuthorization().RequireRateLimiting(VaultEndpoints.RateLimitPolicy);
        trust.MapGet("/trustees", ListTrusteesAsync);
        trust.MapPut("/trustees/{trusteeId:guid}", SaveTrusteeAsync);
        trust.MapDelete("/trustees/{trusteeId:guid}", RemoveTrusteeAsync);
        trust.MapGet("/areas/{area:int}/grants", ListAreaGrantsAsync);
        trust.MapPost("/areas/{area:int}/rotate", RotateAreaAsync);

        var shared = app.MapGroup("/api/shared").RequireAuthorization().RequireRateLimiting(VaultEndpoints.RateLimitPolicy);
        shared.MapGet("", ListSharedAsync);
        shared.MapGet("/{ownerId:guid}/areas/{area:int}", AreaContentAsync);
        shared.MapGet("/{ownerId:guid}/letters/{objectId:guid}", LetterContentAsync);
        shared.MapGet("/{ownerId:guid}/documents/{documentId:guid}/content", DocumentContentAsync);
        return app;
    }

    // ---------- Eigentümer ----------

    private static async Task<Ok<List<TrusteeDto>>> ListTrusteesAsync(ClaimsPrincipal principal, MyLifeDbContext db, CancellationToken cancellationToken)
    {
        var me = Me(principal);
        var relations = await db.TrustRelations.AsNoTracking().Where(r => r.OwnerId == me).OrderBy(r => r.CreatedAt).ToListAsync(cancellationToken);
        var grants = await db.Grants.AsNoTracking().Where(g => g.OwnerId == me).ToListAsync(cancellationToken);
        return TypedResults.Ok(relations.Select(r => new TrusteeDto(
            r.TrusteeId, r.EncryptedProfile, r.CanRequestAccess, r.CreatedAt,
            [.. grants.Where(g => g.TrusteeId == r.TrusteeId).Select(ToDto)])).ToList());
    }

    /// <summary>
    /// Legt eine Vertrauensperson an oder ändert sie. Die Umschläge ersetzen die bisherigen dieser Person; wer
    /// „Sofort teilen“ beendet, wechselt vorher den Bereichsschlüssel (<see cref="RotateAreaAsync"/>).
    /// </summary>
    private static async Task<Results<Ok<TrusteeDto>, ProblemHttpResult>> SaveTrusteeAsync(
        Guid trusteeId, SaveTrusteeRequest request, ClaimsPrincipal principal, MyLifeDbContext db, AuditLog audit, MailOutbox outbox,
        TimeProvider time, CancellationToken cancellationToken)
    {
        var me = await db.Users.SingleAsync(u => u.Id == Me(principal), cancellationToken);
        var trustee = trusteeId == me.Id ? null : await db.Users.SingleOrDefaultAsync(u => u.Id == trusteeId && u.Status == UserStatus.Active, cancellationToken);
        if (trustee is null)
        {
            return Problem(StatusCodes.Status404NotFound, TrustErrors.NotFound);
        }

        if (request.EncryptedProfile is not { Length: > 0 and <= TrustLimits.MaxProfileBytes } || request.Grants is null || request.Grants.Count > TrustLimits.MaxGrants)
        {
            return Problem(StatusCodes.Status400BadRequest, TrustErrors.Invalid);
        }

        var fingerprint = FamilyEndpoints.Fingerprint(trustee);
        var letters = await db.Objects.Where(o => o.OwnerId == me.Id && o.TypeKey == "letter" && o.WrappedKey != null)
            .Select(o => new { o.Id, o.AreaKey }).ToDictionaryAsync(o => o.Id, o => o.AreaKey, cancellationToken);
        var areas = await db.Areas.Where(a => a.OwnerId == me.Id).Select(a => a.AreaKey).ToListAsync(cancellationToken);
        var seen = new HashSet<(GrantKind, int?, Guid?, GrantMode)>();
        foreach (var grant in request.Grants)
        {
            var valid = Enum.IsDefined(grant.Kind) && Enum.IsDefined(grant.Mode)
                && grant.Envelope is { Length: > 0 and <= TrustLimits.MaxEnvelopeBytes } && grant.Signature is { Length: CryptoFormat.SignatureLength }
                && seen.Add((grant.Kind, grant.Area, grant.ObjectId, grant.Mode))
                && (grant.Kind == GrantKind.Area
                    ? grant.Area is { } area && ObjectCatalog.IsUsableArea(area) && areas.Contains(area) && grant.ObjectId is null
                    : grant.ObjectId is { } letter && letters.ContainsKey(letter) && grant.Mode != GrantMode.Live);
            if (!valid)
            {
                return Problem(StatusCodes.Status400BadRequest, TrustErrors.Invalid);
            }

            // Umschläge nur für den aktuellen Schlüssel der Person; sonst hat der Browser einen veralteten gesehen.
            if (!grant.Fingerprint.AsSpan().SequenceEqual(fingerprint))
            {
                return Problem(StatusCodes.Status409Conflict, TrustErrors.KeyChanged);
            }
        }

        var now = time.GetUtcNow();
        var relation = await db.TrustRelations.SingleOrDefaultAsync(r => r.OwnerId == me.Id && r.TrusteeId == trustee.Id, cancellationToken);
        var isNew = relation is null;
        if (relation is null)
        {
            relation = new TrustRelation { OwnerId = me.Id, TrusteeId = trustee.Id, EncryptedProfile = request.EncryptedProfile, CreatedAt = now };
            db.TrustRelations.Add(relation);
        }

        relation.EncryptedProfile = request.EncryptedProfile;
        relation.CanRequestAccess = request.CanRequestAccess;
        relation.UpdatedAt = now;

        await db.Grants.Where(g => g.OwnerId == me.Id && g.TrusteeId == trustee.Id).ExecuteDeleteAsync(cancellationToken);
        foreach (var grant in request.Grants)
        {
            db.Grants.Add(new Grant
            {
                Id = Guid.CreateVersion7(now),
                OwnerId = me.Id,
                TrusteeId = trustee.Id,
                Kind = grant.Kind,
                AreaKey = grant.Kind == GrantKind.Area ? grant.Area!.Value : letters[grant.ObjectId!.Value],
                ObjectId = grant.ObjectId,
                Mode = grant.Mode,
                Envelope = grant.Envelope,
                Signature = grant.Signature,
                Fingerprint = fingerprint,
                CreatedAt = now,
            });
        }

        audit.Own(isNew ? AuditActions.TrusteeAdded : AuditActions.TrusteeChanged, me.Id, "user", trustee.Id,
            $"{request.Grants.Count(g => g.Mode == GrantMode.Live)}/{request.Grants.Count}");
        // Die Person erfährt, dass sie Vertrauensperson ist, aber nicht wofür.
        if (isNew)
        {
            outbox.Enqueue(trustee, MailTemplate.TrusteeAdded, new Dictionary<string, string> { ["owner"] = me.DisplayName });
        }

        await db.SaveChangesAsync(cancellationToken);
        var saved = await db.Grants.AsNoTracking().Where(g => g.OwnerId == me.Id && g.TrusteeId == trustee.Id).ToListAsync(cancellationToken);
        return TypedResults.Ok(new TrusteeDto(trustee.Id, relation.EncryptedProfile, relation.CanRequestAccess, relation.CreatedAt, [.. saved.Select(ToDto)]));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> RemoveTrusteeAsync(
        Guid trusteeId, ClaimsPrincipal principal, MyLifeDbContext db, AuditLog audit, CancellationToken cancellationToken)
    {
        var me = Me(principal);
        var relation = await db.TrustRelations.SingleOrDefaultAsync(r => r.OwnerId == me && r.TrusteeId == trusteeId, cancellationToken);
        if (relation is null)
        {
            return Problem(StatusCodes.Status404NotFound, TrustErrors.NotFound);
        }

        await db.Grants.Where(g => g.OwnerId == me && g.TrusteeId == trusteeId).ExecuteDeleteAsync(cancellationToken);
        db.TrustRelations.Remove(relation);
        audit.Own(AuditActions.TrusteeRemoved, me, "user", trusteeId);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>Alle Umschläge eines Bereichs (für den Schlüsselwechsel), mit Empfänger.</summary>
    private static async Task<Ok<List<AreaGrantDto>>> ListAreaGrantsAsync(int area, ClaimsPrincipal principal, MyLifeDbContext db, CancellationToken cancellationToken)
    {
        var me = Me(principal);
        return TypedResults.Ok(await db.Grants.AsNoTracking().Where(g => g.OwnerId == me && g.Kind == GrantKind.Area && g.AreaKey == area)
            .Select(g => new AreaGrantDto(g.Id, g.TrusteeId, g.Mode)).ToListAsync(cancellationToken));
    }

    /// <summary>
    /// Neuer Bereichsschlüssel (46b): Der Browser hat alle Objekte des Bereichs neu verschlüsselt (Briefe: nur ihren
    /// Schlüssel neu verpackt), alle Dateischlüssel umgepackt und alle Umschläge des Bereichs neu versiegelt. Fehlt
    /// etwas oder hat sich inzwischen etwas geändert, wird nichts übernommen (409).
    /// </summary>
    private static async Task<Results<NoContent, ProblemHttpResult>> RotateAreaAsync(
        int area, RotateAreaRequest request, ClaimsPrincipal principal, MyLifeDbContext db, FileStore files, AuditLog audit,
        CancellationToken cancellationToken)
    {
        var me = Me(principal);
        if (!ObjectCatalog.IsUsableArea(area) || !KeyMaterialValidation.IsWrappedSecretKey(request.WrappedAreaKey)
            || request.Objects is null || request.Documents is null || request.Grants is null)
        {
            return Problem(StatusCodes.Status400BadRequest, TrustErrors.Invalid);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var areaEntity = await db.Areas.SingleOrDefaultAsync(a => a.OwnerId == me && a.AreaKey == area, cancellationToken);
        if (areaEntity is null)
        {
            return Problem(StatusCodes.Status404NotFound, TrustErrors.NotFound);
        }

        var objects = await db.Objects.Where(o => o.OwnerId == me && o.AreaKey == area).ToListAsync(cancellationToken);
        var objectIds = objects.Select(o => o.Id).ToList();
        var documents = await db.Documents.Where(d => objectIds.Contains(d.ObjectId)).ToListAsync(cancellationToken);
        var grants = await db.Grants.Where(g => g.OwnerId == me && g.Kind == GrantKind.Area && g.AreaKey == area).ToListAsync(cancellationToken);

        var objectUpdates = request.Objects.ToDictionary(o => o.Id);
        var documentUpdates = request.Documents.ToDictionary(d => d.Id);
        var grantUpdates = request.Grants.ToDictionary(g => g.Id);
        // Dokumente mit eigenem Objektschlüssel (Briefe) behalten ihren verpackten Dateischlüssel.
        var keyedObjects = objects.Where(o => o.WrappedKey is not null).Select(o => o.Id).ToHashSet();
        var documentsToRewrap = documents.Where(d => d.IsComplete && !keyedObjects.Contains(d.ObjectId)).ToList();
        if (objectUpdates.Count != objects.Count || objects.Any(o => !objectUpdates.ContainsKey(o.Id))
            || documentUpdates.Count != documentsToRewrap.Count || documentsToRewrap.Any(d => !documentUpdates.ContainsKey(d.Id))
            || grantUpdates.Count != grants.Count || grants.Any(g => !grantUpdates.ContainsKey(g.Id)))
        {
            return Problem(StatusCodes.Status409Conflict, TrustErrors.Stale);
        }

        foreach (var entity in objects)
        {
            var update = objectUpdates[entity.Id];
            var keyed = entity.WrappedKey is not null;
            if (update.Version != entity.Version || !VaultEndpoints.IsWrappedObjectKey(update.WrappedKey) || keyed != update.WrappedKey is not null
                || update.Payload is not { Length: > 0 and <= VaultLimits.MaxPayloadBytes + 64 })
            {
                return Problem(StatusCodes.Status409Conflict, TrustErrors.Stale);
            }

            entity.Payload = update.Payload;
            entity.WrappedKey = update.WrappedKey;
        }

        foreach (var document in documentsToRewrap)
        {
            var update = documentUpdates[document.Id];
            if (!KeyMaterialValidation.IsWrappedSecretKey(update.WrappedFileKey) || update.Metadata is not { Length: > 0 and <= 8192 })
            {
                return Problem(StatusCodes.Status400BadRequest, TrustErrors.Invalid);
            }

            document.WrappedFileKey = update.WrappedFileKey;
            document.Metadata = update.Metadata;
        }

        foreach (var grant in grants)
        {
            var update = grantUpdates[grant.Id];
            if (update.Envelope is not { Length: > 0 and <= TrustLimits.MaxEnvelopeBytes } || update.Signature is not { Length: CryptoFormat.SignatureLength })
            {
                return Problem(StatusCodes.Status400BadRequest, TrustErrors.Invalid);
            }

            grant.Envelope = update.Envelope;
            grant.Signature = update.Signature;
        }

        // Abgebrochene Uploads kennt der Browser nicht; mit dem alten Schlüssel wären sie ohnehin unlesbar.
        foreach (var incomplete in documents.Where(d => !d.IsComplete && !keyedObjects.Contains(d.ObjectId)))
        {
            files.Delete(incomplete.StorageKey);
            db.Documents.Remove(incomplete);
        }

        areaEntity.WrappedAreaKey = request.WrappedAreaKey;
        audit.Own(AuditActions.AreaKeyRotated, me, "area", details: area.ToString(System.Globalization.CultureInfo.InvariantCulture));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Problem(StatusCodes.Status409Conflict, TrustErrors.Stale);
        }

        return TypedResults.NoContent();
    }

    // ---------- Vertrauensperson: was mit mir geteilt ist ----------

    /// <summary>Umschläge, die der Server schon ausliefert: in M2.3 nur „Sofort teilen“, ab M2.4 auch freigegebene Modi.</summary>
    private static async Task<Ok<List<SharedOwnerDto>>> ListSharedAsync(ClaimsPrincipal principal, MyLifeDbContext db, CancellationToken cancellationToken)
    {
        var me = Me(principal);
        var grants = await Deliverable(db, me).ToListAsync(cancellationToken);
        var owners = await db.Users.AsNoTracking().Where(u => grants.Select(g => g.OwnerId).Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName }).ToListAsync(cancellationToken);
        return TypedResults.Ok(owners.OrderBy(o => o.DisplayName).Select(o => new SharedOwnerDto(o.Id, o.DisplayName,
            [.. grants.Where(g => g.OwnerId == o.Id).Select(g => new SharedGrantDto(g.Id, g.Kind, g.Kind == GrantKind.Area ? g.AreaKey : null, g.ObjectId, g.Mode, g.Envelope, g.Signature))]))
            .ToList());
    }

    private static async Task<Results<Ok<SharedContentDto>, ProblemHttpResult>> AreaContentAsync(
        Guid ownerId, int area, ClaimsPrincipal principal, MyLifeDbContext db, AuditLog audit, CancellationToken cancellationToken)
    {
        var me = Me(principal);
        if (!await Deliverable(db, me).AnyAsync(g => g.OwnerId == ownerId && g.Kind == GrantKind.Area && g.AreaKey == area, cancellationToken))
        {
            return Problem(StatusCodes.Status404NotFound, TrustErrors.NotFound);
        }

        // Nur aktive Objekte ohne eigenen Schlüssel: Briefe gibt es nur einzeln, gelöschte gar nicht.
        var objects = await db.Objects.AsNoTracking()
            .Where(o => o.OwnerId == ownerId && o.AreaKey == area && !o.IsDeleted && o.WrappedKey == null).ToListAsync(cancellationToken);
        var ids = objects.Select(o => o.Id).ToList();
        var documents = await db.Documents.AsNoTracking().Where(d => ids.Contains(d.ObjectId) && !d.IsDeleted && d.IsComplete).ToListAsync(cancellationToken);
        audit.Add(AuditActions.SharedRead, me, ownerId, "area", details: area.ToString(System.Globalization.CultureInfo.InvariantCulture));
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(new SharedContentDto([.. objects.Select(VaultEndpoints.ObjectDto)], [.. documents.Select(VaultEndpoints.DocumentDtoOf)]));
    }

    private static async Task<Results<Ok<SharedContentDto>, ProblemHttpResult>> LetterContentAsync(
        Guid ownerId, Guid objectId, ClaimsPrincipal principal, MyLifeDbContext db, AuditLog audit, CancellationToken cancellationToken)
    {
        var me = Me(principal);
        if (!await Deliverable(db, me).AnyAsync(g => g.OwnerId == ownerId && g.Kind == GrantKind.Letter && g.ObjectId == objectId, cancellationToken))
        {
            return Problem(StatusCodes.Status404NotFound, TrustErrors.NotFound);
        }

        var letter = await db.Objects.AsNoTracking().SingleOrDefaultAsync(o => o.Id == objectId && o.OwnerId == ownerId && !o.IsDeleted, cancellationToken);
        if (letter is null)
        {
            return Problem(StatusCodes.Status404NotFound, TrustErrors.NotFound);
        }

        var documents = await db.Documents.AsNoTracking().Where(d => d.ObjectId == objectId && !d.IsDeleted && d.IsComplete).ToListAsync(cancellationToken);
        audit.Add(AuditActions.SharedRead, me, ownerId, letter.TypeKey, letter.Id);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(new SharedContentDto([VaultEndpoints.ObjectDto(letter)], [.. documents.Select(VaultEndpoints.DocumentDtoOf)]));
    }

    private static async Task<Results<FileStreamHttpResult, ProblemHttpResult>> DocumentContentAsync(
        Guid ownerId, Guid documentId, ClaimsPrincipal principal, MyLifeDbContext db, FileStore files, AuditLog audit, CancellationToken cancellationToken)
    {
        var me = Me(principal);
        var document = await db.Documents.AsNoTracking().Include(d => d.Parent)
            .SingleOrDefaultAsync(d => d.Id == documentId && d.OwnerId == ownerId && !d.IsDeleted && d.IsComplete, cancellationToken);
        var parent = document?.Parent;
        var allowed = parent is not null && !parent.IsDeleted && (parent.WrappedKey is null
            ? await Deliverable(db, me).AnyAsync(g => g.OwnerId == ownerId && g.Kind == GrantKind.Area && g.AreaKey == parent.AreaKey, cancellationToken)
            : await Deliverable(db, me).AnyAsync(g => g.OwnerId == ownerId && g.Kind == GrantKind.Letter && g.ObjectId == parent.Id, cancellationToken));
        if (!allowed || files.OpenRead(document!.StorageKey) is not { } stream)
        {
            return Problem(StatusCodes.Status404NotFound, TrustErrors.NotFound);
        }

        audit.Add(AuditActions.SharedDocumentRead, me, ownerId, "document", document.Id);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.File(stream, "application/octet-stream");
    }

    /// <summary>Was der Server an mich ausliefern darf. M2.4 ergänzt hier die freigegebenen Modi.</summary>
    internal static IQueryable<Grant> Deliverable(MyLifeDbContext db, Guid trusteeId) =>
        db.Grants.AsNoTracking().Where(g => g.TrusteeId == trusteeId && g.Mode == GrantMode.Live
            && db.TrustRelations.Any(r => r.OwnerId == g.OwnerId && r.TrusteeId == trusteeId)
            && db.Users.Any(u => u.Id == g.OwnerId && u.Status == UserStatus.Active));

    private static GrantDto ToDto(Grant g) => new(g.Id, g.Kind, g.Kind == GrantKind.Area ? g.AreaKey : null, g.ObjectId, g.Mode, g.Fingerprint);

    private static Guid Me(ClaimsPrincipal principal) => MyLifeClaims.UserId(principal) ?? throw new InvalidOperationException("Nicht angemeldet.");

    private static ProblemHttpResult Problem(int status, string code) => ApiProblems.Create(status, code);
}
