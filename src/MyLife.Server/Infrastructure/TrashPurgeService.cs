using Microsoft.EntityFrameworkCore;
using MyLife.Server.Api;
using MyLife.Server.Data;
using MyLife.Shared.Vault;

namespace MyLife.Server.Infrastructure;

/// <summary>
/// Leert den Papierkorb: Objekte und Dokumente, die seit 30 Tagen gelöscht sind, werden endgültig entfernt
/// (Architektur 6.3). Abgebrochene Uploads verschwinden nach einem Tag. Läuft stündlich und nach jedem Start.
/// </summary>
internal sealed partial class TrashPurgeService(IServiceScopeFactory scopes, TimeProvider time, ILogger<TrashPurgeService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PurgeOnceAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is DbUpdateException or InvalidOperationException or IOException or Npgsql.NpgsqlException)
            {
                LogFailed(logger, exception);
            }

            try
            {
                await Task.Delay(Interval, time, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    public async Task<int> PurgeOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MyLifeDbContext>();
        var files = scope.ServiceProvider.GetRequiredService<FileStore>();
        var now = time.GetUtcNow();
        var trashLimit = now.AddDays(-VaultLimits.TrashDays);
        var uploadLimit = now.AddDays(-1);

        var objects = await db.Objects.Include(o => o.Documents)
            .Where(o => o.IsDeleted && o.DeletedAt < trashLimit).Take(500).ToListAsync(cancellationToken);
        foreach (var entity in objects)
        {
            VaultEndpoints.Purge(db, files, entity);
            db.AuditLog.Add(new AuditEntry { At = now, OwnerId = entity.OwnerId, Action = AuditActions.ObjectPurged, TargetType = entity.TypeKey, TargetId = entity.Id, Details = "auto" });
        }

        var documents = await db.Documents
            .Where(d => (d.IsDeleted && d.DeletedAt < trashLimit) || (!d.IsComplete && d.CreatedAt < uploadLimit))
            .Take(500).ToListAsync(cancellationToken);
        foreach (var document in documents.Where(d => objects.All(o => o.Id != d.ObjectId)))
        {
            files.Delete(document.StorageKey);
            db.Documents.Remove(document);
            if (document.IsComplete)
            {
                db.AuditLog.Add(new AuditEntry { At = now, OwnerId = document.OwnerId, Action = AuditActions.DocumentPurged, TargetType = "document", TargetId = document.Id, Details = "auto" });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return objects.Count + documents.Count;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Papierkorb konnte nicht geleert werden")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
