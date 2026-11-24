using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using MyLife.Crypto;
using MyLife.Server.Data;
using MyLife.Server.Infrastructure;
using MyLife.Server.Infrastructure.Auth;
using MyLife.Shared.Vault;

namespace MyLife.Server.Api;

/// <summary>
/// Der eigene Tresor: Bereiche, Objekte, Dokumente und das Audit-Log. Der Server speichert nur Chiffrat und die
/// Metadaten aus Architektur 6.4 (Bereich, Objekttyp, Status, Zeiten, Größen). Jeder Endpunkt verlangt die Anmeldung
/// und arbeitet nur auf Datensätzen der angemeldeten Person.
/// </summary>
internal static class VaultEndpoints
{
    public const string RateLimitPolicy = "vault";

    public static IEndpointRouteBuilder MapVaultEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/vault").RequireAuthorization().RequireRateLimiting(RateLimitPolicy);
        group.MapGet("/areas", ListAreasAsync);
        group.MapPut("/areas/{area:int}/key", CreateAreaKeyAsync);
        group.MapPost("/areas/{area:int}/reviewed", MarkReviewedAsync);

        group.MapGet("/objects", ListObjectsAsync);
        group.MapPost("/objects", CreateObjectAsync);
        group.MapPut("/objects/{id:guid}", UpdateObjectAsync);
        group.MapDelete("/objects/{id:guid}", DeleteObjectAsync);
        group.MapPost("/objects/{id:guid}/restore", RestoreObjectAsync);
        group.MapDelete("/objects/{id:guid}/purge", PurgeObjectAsync);

        group.MapGet("/documents", ListDocumentsAsync);
        group.MapPost("/documents", CreateDocumentAsync);
        group.MapPut("/documents/{id:guid}/content", UploadContentAsync);
        group.MapGet("/documents/{id:guid}/content", DownloadContentAsync);
        group.MapDelete("/documents/{id:guid}", DeleteDocumentAsync);
        group.MapPost("/documents/{id:guid}/restore", RestoreDocumentAsync);
        group.MapDelete("/documents/{id:guid}/purge", PurgeDocumentAsync);

        group.MapGet("/audit", ListAuditAsync);
        group.MapPost("/audit", ReportAuditAsync);
        return app;
    }

    private static Guid Me(ClaimsPrincipal principal) =>
        MyLifeClaims.UserId(principal) ?? throw new InvalidOperationException("Endpunkt ohne Anmeldung aufgerufen.");

    // ---------- Bereiche ----------

    private static async Task<Ok<List<AreaDto>>> ListAreasAsync(ClaimsPrincipal principal, MyLifeDbContext db, CancellationToken cancellationToken)
    {
        var me = Me(principal);
        var areas = await db.Areas.AsNoTracking().Where(a => a.OwnerId == me).OrderBy(a => a.AreaKey)
            .Select(a => new AreaDto(a.AreaKey, a.WrappedAreaKey, a.LastReviewedAt))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(areas);
    }

    private static async Task<Results<Ok<AreaDto>, ProblemHttpResult>> CreateAreaKeyAsync(
        int area, AreaKeyRequest request, ClaimsPrincipal principal, MyLifeDbContext db, AuditLog audit, TimeProvider time,
        CancellationToken cancellationToken)
    {
        var me = Me(principal);
        if (!ObjectCatalog.IsUsableArea(area) || !KeyMaterialValidation.IsWrappedSecretKey(request.WrappedKey))
        {
            return ApiProblems.Create(StatusCodes.Status400BadRequest, VaultErrors.Invalid);
        }

        if (await db.Areas.AnyAsync(a => a.OwnerId == me && a.AreaKey == area, cancellationToken))
        {
            return ApiProblems.Create(StatusCodes.Status409Conflict, VaultErrors.AreaKeyExists);
        }

        var entity = new VaultArea { Id = Guid.NewGuid(), OwnerId = me, AreaKey = area, WrappedAreaKey = request.WrappedKey, CreatedAt = time.GetUtcNow() };
        db.Areas.Add(entity);
        audit.Own(AuditActions.AreaCreated, me, "area", details: area.ToString(System.Globalization.CultureInfo.InvariantCulture));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Zwei Tabs gleichzeitig: Der erste Schlüssel gilt, der Browser lädt ihn neu.
            return ApiProblems.Create(StatusCodes.Status409Conflict, VaultErrors.AreaKeyExists);
        }

        return TypedResults.Ok(new AreaDto(area, entity.WrappedAreaKey, null));
    }

    private static async Task<Results<Ok<AreaDto>, ProblemHttpResult>> MarkReviewedAsync(
        int area, ClaimsPrincipal principal, MyLifeDbContext db, AuditLog audit, TimeProvider time, CancellationToken cancellationToken)
    {
        var me = Me(principal);
        var entity = await db.Areas.SingleOrDefaultAsync(a => a.OwnerId == me && a.AreaKey == area, cancellationToken);
        if (entity is null)
        {
            return ApiProblems.Create(StatusCodes.Status404NotFound, VaultErrors.AreaKeyMissing);
        }

        entity.LastReviewedAt = time.GetUtcNow();
        audit.Own(AuditActions.AreaReviewed, me, "area", details: area.ToString(System.Globalization.CultureInfo.InvariantCulture));
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(new AreaDto(area, entity.WrappedAreaKey, entity.LastReviewedAt));
    }

    // ---------- Objekte ----------

    private static async Task<Ok<List<VaultObjectDto>>> ListObjectsAsync(ClaimsPrincipal principal, MyLifeDbContext db, CancellationToken cancellationToken)
    {
        var me = Me(principal);
        var objects = await db.Objects.AsNoTracking().Where(o => o.OwnerId == me).OrderBy(o => o.CreatedAt)
            .Select(o => new VaultObjectDto(o.Id, o.AreaKey, o.TypeKey, o.Payload, o.SchemaVersion, o.Status, o.CreatedAt, o.UpdatedAt, o.Version, o.IsDeleted, o.DeletedAt))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(objects);
    }

    private static async Task<Results<Created<VaultObjectDto>, ProblemHttpResult>> CreateObjectAsync(
        CreateObjectRequest request, ClaimsPrincipal principal, MyLifeDbContext db, AuditLog audit, TimeProvider time,
        CancellationToken cancellationToken)
    {
        var me = Me(principal);
        var type = ObjectCatalog.Find(request.TypeKey);
        if (request.Id == Guid.Empty || type is null || type.Area != request.Area || !IsPayload(request.Payload)
            || !Enum.IsDefined(request.Status) || request.SchemaVersion is < 1 or > 1000)
        {
            return ApiProblems.Create(StatusCodes.Status400BadRequest, VaultErrors.Invalid);
        }

        if (!await db.Areas.AnyAsync(a => a.OwnerId == me && a.AreaKey == request.Area, cancellationToken))
        {
            return ApiProblems.Create(StatusCodes.Status400BadRequest, VaultErrors.AreaKeyMissing);
        }

        if (await db.Objects.AnyAsync(o => o.Id == request.Id, cancellationToken))
        {
            return ApiProblems.Create(StatusCodes.Status409Conflict, VaultErrors.Conflict);
        }

        var now = time.GetUtcNow();
        var entity = new VaultObject
        {
            Id = request.Id,
            OwnerId = me,
            AreaKey = request.Area,
            TypeKey = type.Key,
            Payload = request.Payload,
            SchemaVersion = request.SchemaVersion,
            Status = request.Status,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Objects.Add(entity);
        audit.Own(AuditActions.ObjectCreated, me, type.Key, entity.Id);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/vault/objects/{entity.Id}", ToDto(entity));
    }

    private static async Task<Results<Ok<VaultObjectDto>, ProblemHttpResult>> UpdateObjectAsync(
        Guid id, UpdateObjectRequest request, ClaimsPrincipal principal, MyLifeDbContext db, AuditLog audit, TimeProvider time,
        CancellationToken cancellationToken)
    {
        var me = Me(principal);
        if (!IsPayload(request.Payload) || !Enum.IsDefined(request.Status) || request.SchemaVersion is < 1 or > 1000)
        {
            return ApiProblems.Create(StatusCodes.Status400BadRequest, VaultErrors.Invalid);
        }

        var entity = await db.Objects.SingleOrDefaultAsync(o => o.Id == id && o.OwnerId == me && !o.IsDeleted, cancellationToken);
        if (entity is null)
        {
            return ApiProblems.Create(StatusCodes.Status404NotFound, VaultErrors.NotFound);
        }

        db.Entry(entity).Property(o => o.Version).OriginalValue = request.Version;
        entity.Payload = request.Payload;
        entity.SchemaVersion = request.SchemaVersion;
        entity.Status = request.Status;
        entity.UpdatedAt = time.GetUtcNow();
        audit.Own(AuditActions.ObjectUpdated, me, entity.TypeKey, entity.Id);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApiProblems.Create(StatusCodes.Status409Conflict, VaultErrors.Conflict);
        }

        return TypedResults.Ok(ToDto(entity));
    }

    private static async Task<Results<Ok<VaultObjectDto>, ProblemHttpResult>> DeleteObjectAsync(
        Guid id, ClaimsPrincipal principal, MyLifeDbContext db, AuditLog audit, TimeProvider time, CancellationToken cancellationToken)
    {
        var me = Me(principal);
        var entity = await db.Objects.SingleOrDefaultAsync(o => o.Id == id && o.OwnerId == me, cancellationToken);
        if (entity is null)
        {
            return ApiProblems.Create(StatusCodes.Status404NotFound, VaultErrors.NotFound);
        }

        if (!entity.IsDeleted)
        {
            entity.IsDeleted = true;
            entity.DeletedAt = time.GetUtcNow();
            audit.Own(AuditActions.ObjectDeleted, me, entity.TypeKey, entity.Id);
            await db.SaveChangesAsync(cancellationToken);
        }

        return TypedResults.Ok(ToDto(entity));
    }

    private static async Task<Results<Ok<VaultObjectDto>, ProblemHttpResult>> RestoreObjectAsync(
        Guid id, ClaimsPrincipal principal, MyLifeDbContext db, AuditLog audit, CancellationToken cancellationToken)
    {
        var me = Me(principal);
        var entity = await db.Objects.SingleOrDefaultAsync(o => o.Id == id && o.OwnerId == me, cancellationToken);
        if (entity is null)
        {
            return ApiProblems.Create(StatusCodes.Status404NotFound, VaultErrors.NotFound);
        }

        if (entity.IsDeleted)
        {
            entity.IsDeleted = false;
            entity.DeletedAt = null;
            audit.Own(AuditActions.ObjectRestored, me, entity.TypeKey, entity.Id);
            await db.SaveChangesAsync(cancellationToken);
        }

        return TypedResults.Ok(ToDto(entity));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> PurgeObjectAsync(
        Guid id, ClaimsPrincipal principal, MyLifeDbContext db, AuditLog audit, FileStore files, CancellationToken cancellationToken)
    {
        var me = Me(principal);
        var entity = await db.Objects.Include(o => o.Documents).SingleOrDefaultAsync(o => o.Id == id && o.OwnerId == me, cancellationToken);
        if (entity is null)
        {
            return ApiProblems.Create(StatusCodes.Status404NotFound, VaultErrors.NotFound);
        }

        // Endgültig löschen geht nur aus dem Papierkorb.
        if (!entity.IsDeleted)
        {
            return ApiProblems.Create(StatusCodes.Status409Conflict, VaultErrors.Conflict);
        }

        Purge(db, files, entity);
        audit.Own(AuditActions.ObjectPurged, me, entity.TypeKey, entity.Id);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>Entfernt ein Objekt mit allen Dokumenten aus Datenbank und Dateiablage.</summary>
    internal static void Purge(MyLifeDbContext db, FileStore files, VaultObject entity)
    {
        foreach (var document in entity.Documents)
        {
            files.Delete(document.StorageKey);
        }

        db.Objects.Remove(entity);
    }

    // ---------- Dokumente ----------

    private static async Task<Ok<List<DocumentDto>>> ListDocumentsAsync(ClaimsPrincipal principal, MyLifeDbContext db, CancellationToken cancellationToken)
    {
        var me = Me(principal);
        var documents = await db.Documents.AsNoTracking().Where(d => d.OwnerId == me && d.IsComplete).OrderBy(d => d.CreatedAt)
            .Select(d => new DocumentDto(d.Id, d.ObjectId, d.WrappedFileKey, d.Metadata, d.CipherSize, d.IsComplete, d.CreatedAt, d.IsDeleted, d.DeletedAt))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(documents);
    }

    private static async Task<Results<Created<DocumentDto>, ProblemHttpResult>> CreateDocumentAsync(
        CreateDocumentRequest request, ClaimsPrincipal principal, MyLifeDbContext db, TimeProvider time, CancellationToken cancellationToken)
    {
        var me = Me(principal);
        if (request.Id == Guid.Empty || !KeyMaterialValidation.IsWrappedSecretKey(request.WrappedFileKey)
            || !IsData(request.Metadata, VaultLimits.MaxMetadataBytes) || request.CipherSize < VaultLimits.ChunkOverhead)
        {
            return ApiProblems.Create(StatusCodes.Status400BadRequest, VaultErrors.Invalid);
        }

        if (request.CipherSize > VaultLimits.MaxCipherSize)
        {
            return ApiProblems.Create(StatusCodes.Status413PayloadTooLarge, VaultErrors.TooLarge);
        }

        if (!await db.Objects.AnyAsync(o => o.Id == request.ObjectId && o.OwnerId == me && !o.IsDeleted, cancellationToken))
        {
            return ApiProblems.Create(StatusCodes.Status404NotFound, VaultErrors.NotFound);
        }

        if (await db.Documents.AnyAsync(d => d.Id == request.Id, cancellationToken))
        {
            return ApiProblems.Create(StatusCodes.Status409Conflict, VaultErrors.Conflict);
        }

        var entity = new VaultDocument
        {
            Id = request.Id,
            ObjectId = request.ObjectId,
            OwnerId = me,
            WrappedFileKey = request.WrappedFileKey,
            Metadata = request.Metadata,
            CipherSize = request.CipherSize,
            StorageKey = FileStore.NewStorageKey(),
            CreatedAt = time.GetUtcNow(),
        };
        db.Documents.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/vault/documents/{entity.Id}", ToDto(entity));
    }

    private static async Task<Results<Ok<DocumentDto>, ProblemHttpResult>> UploadContentAsync(
        Guid id, HttpContext context, ClaimsPrincipal principal, MyLifeDbContext db, FileStore files, AuditLog audit,
        CancellationToken cancellationToken)
    {
        var me = Me(principal);
        var entity = await db.Documents.SingleOrDefaultAsync(d => d.Id == id && d.OwnerId == me, cancellationToken);
        if (entity is null)
        {
            return ApiProblems.Create(StatusCodes.Status404NotFound, VaultErrors.NotFound);
        }

        // Ein Dateischlüssel gehört zu genau einer Datei (kryptoformat.md 4): Inhalt nur einmal.
        if (entity.IsComplete)
        {
            return ApiProblems.Create(StatusCodes.Status409Conflict, VaultErrors.Conflict);
        }

        if (context.Request.ContentLength is { } length && length != entity.CipherSize)
        {
            return ApiProblems.Create(StatusCodes.Status400BadRequest, VaultErrors.Invalid);
        }

        if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
        {
            limit.MaxRequestBodySize = VaultLimits.MaxCipherSize;
        }

        var hash = await files.WriteAsync(entity.StorageKey, context.Request.Body, entity.CipherSize, cancellationToken);
        if (hash is null)
        {
            return ApiProblems.Create(StatusCodes.Status400BadRequest, VaultErrors.Invalid);
        }

        entity.CipherSha256 = hash;
        entity.IsComplete = true;
        audit.Own(AuditActions.DocumentUploaded, me, "document", entity.Id);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToDto(entity));
    }

    private static async Task<Results<FileStreamHttpResult, ProblemHttpResult>> DownloadContentAsync(
        Guid id, ClaimsPrincipal principal, MyLifeDbContext db, FileStore files, CancellationToken cancellationToken)
    {
        var me = Me(principal);
        var entity = await db.Documents.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id && d.OwnerId == me && d.IsComplete, cancellationToken);
        var stream = entity is null ? null : files.OpenRead(entity.StorageKey);
        return stream is null
            ? ApiProblems.Create(StatusCodes.Status404NotFound, VaultErrors.NotFound)
            : TypedResults.Stream(stream, "application/octet-stream");
    }

    private static async Task<Results<Ok<DocumentDto>, ProblemHttpResult>> DeleteDocumentAsync(
        Guid id, ClaimsPrincipal principal, MyLifeDbContext db, AuditLog audit, TimeProvider time, CancellationToken cancellationToken)
    {
        var me = Me(principal);
        var entity = await db.Documents.SingleOrDefaultAsync(d => d.Id == id && d.OwnerId == me, cancellationToken);
        if (entity is null)
        {
            return ApiProblems.Create(StatusCodes.Status404NotFound, VaultErrors.NotFound);
        }

        if (!entity.IsDeleted)
        {
            entity.IsDeleted = true;
            entity.DeletedAt = time.GetUtcNow();
            audit.Own(AuditActions.DocumentDeleted, me, "document", entity.Id);
            await db.SaveChangesAsync(cancellationToken);
        }

        return TypedResults.Ok(ToDto(entity));
    }

    private static async Task<Results<Ok<DocumentDto>, ProblemHttpResult>> RestoreDocumentAsync(
        Guid id, ClaimsPrincipal principal, MyLifeDbContext db, AuditLog audit, CancellationToken cancellationToken)
    {
        var me = Me(principal);
        var entity = await db.Documents.SingleOrDefaultAsync(d => d.Id == id && d.OwnerId == me, cancellationToken);
        if (entity is null)
        {
            return ApiProblems.Create(StatusCodes.Status404NotFound, VaultErrors.NotFound);
        }

        if (entity.IsDeleted)
        {
            entity.IsDeleted = false;
            entity.DeletedAt = null;
            audit.Own(AuditActions.DocumentRestored, me, "document", entity.Id);
            await db.SaveChangesAsync(cancellationToken);
        }

        return TypedResults.Ok(ToDto(entity));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> PurgeDocumentAsync(
        Guid id, ClaimsPrincipal principal, MyLifeDbContext db, AuditLog audit, FileStore files, CancellationToken cancellationToken)
    {
        var me = Me(principal);
        var entity = await db.Documents.SingleOrDefaultAsync(d => d.Id == id && d.OwnerId == me, cancellationToken);
        if (entity is null)
        {
            return ApiProblems.Create(StatusCodes.Status404NotFound, VaultErrors.NotFound);
        }

        if (!entity.IsDeleted)
        {
            return ApiProblems.Create(StatusCodes.Status409Conflict, VaultErrors.Conflict);
        }

        files.Delete(entity.StorageKey);
        db.Documents.Remove(entity);
        audit.Own(AuditActions.DocumentPurged, me, "document", entity.Id);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    // ---------- Audit-Log ----------

    private static async Task<Ok<AuditPageDto>> ListAuditAsync(
        ClaimsPrincipal principal, MyLifeDbContext db, int? page, int? size, CancellationToken cancellationToken)
    {
        var me = Me(principal);
        var take = Math.Clamp(size ?? 50, 1, 100);
        var skip = Math.Max(page ?? 0, 0) * take;
        var query = db.AuditLog.AsNoTracking().Where(a => a.OwnerId == me);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(a => a.At).ThenByDescending(a => a.Id).Skip(skip).Take(take)
            .Select(a => new AuditEntryDto(
                a.Id, a.At, a.Action, a.TargetType, a.TargetId, a.Ip,
                db.Users.IgnoreQueryFilters().Where(u => u.Id == a.ActorId).Select(u => u.DisplayName).FirstOrDefault(),
                a.Details))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(new AuditPageDto(items, total));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> ReportAuditAsync(
        AuditEventRequest request, ClaimsPrincipal principal, MyLifeDbContext db, AuditLog audit, CancellationToken cancellationToken)
    {
        var me = Me(principal);
        if (!AuditActions.ClientReported.Contains(request.Action) || request.Details is { Length: > 200 })
        {
            return ApiProblems.Create(StatusCodes.Status400BadRequest, VaultErrors.Invalid);
        }

        audit.Own(request.Action, me, details: request.Details);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    // ---------- Hilfen ----------

    private static bool IsPayload(byte[]? payload) => IsData(payload, VaultLimits.MaxPayloadBytes);

    /// <summary>AES-GCM-Datensatz der Art „Data“ (kryptoformat.md 3) mit Obergrenze für den Klartext.</summary>
    private static bool IsData(byte[]? data, int maxPlainBytes) =>
        data is { Length: >= 2 + CryptoFormat.NonceLength + CryptoFormat.TagLength } && data.Length <= maxPlainBytes + 2 + CryptoFormat.NonceLength + CryptoFormat.TagLength
        && data[0] == CryptoFormat.CurrentVersion && data[1] == (byte)CryptoKind.Data;

    private static VaultObjectDto ToDto(VaultObject o) =>
        new(o.Id, o.AreaKey, o.TypeKey, o.Payload, o.SchemaVersion, o.Status, o.CreatedAt, o.UpdatedAt, o.Version, o.IsDeleted, o.DeletedAt);

    private static DocumentDto ToDto(VaultDocument d) =>
        new(d.Id, d.ObjectId, d.WrappedFileKey, d.Metadata, d.CipherSize, d.IsComplete, d.CreatedAt, d.IsDeleted, d.DeletedAt);
}
