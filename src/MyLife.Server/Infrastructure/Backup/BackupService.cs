using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyLife.Server.Data;
using MyLife.Server.Infrastructure.Admin;
using MyLife.Server.Infrastructure.Mail;
using MyLife.Shared.Admin;
using MyLife.Shared.Vault;

namespace MyLife.Server.Infrastructure.Backup;

/// <summary>
/// Tägliche Server-Sicherung (M3.4, Entscheidung 5): zur eingestellten Uhrzeit eine verschlüsselte Sicherung in
/// <c>{DataDirectory}/backup</c>, danach Aufräumen nach Generationen. Das Synology-Backup übernimmt den Ordner.
/// Schlägt eine Sicherung fehl, bekommen die Admins höchstens einmal am Tag eine Mail.
/// </summary>
internal sealed partial class BackupService(
    IServiceScopeFactory scopes, IOptions<MyLifeOptions> options, SettingsStore settings, TimeProvider time, ILogger<BackupService> logger) : BackgroundService
{
    private const string Prefix = "mylife-";
    private const string NameFormat = "yyyyMMdd-HHmmss";
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly byte[]? _key = BackupFormat.ParseKey(options.Value.BackupKey);
    private DateOnly? _failureMailOn;

    public string Directory { get; } = Path.Combine(Path.GetFullPath(options.Value.DataDirectory), "backup");

    public bool Configured => _key is not null;

    public string? LastError { get; private set; }

    public DateTimeOffset? LastErrorAt { get; private set; }

    public override void Dispose()
    {
        _gate.Dispose();
        base.Dispose();
    }

    public IReadOnlyList<BackupItemDto> List()
    {
        if (!System.IO.Directory.Exists(Directory))
        {
            return [];
        }

        return
        [
            .. new DirectoryInfo(Directory).GetFiles($"{Prefix}*{BackupFormat.FileExtension}")
                .Select(f => new BackupItemDto(f.Name, CreatedAt(f), f.Length))
                .OrderByDescending(i => i.CreatedAt),
        ];
    }

    public async Task<BackupStatusDto> StatusAsync(CancellationToken cancellationToken)
    {
        var items = List();
        var backup = (await settings.GetAsync(cancellationToken)).Backup;
        return new BackupStatusDto(Configured, items.Count > 0 ? items[0].CreatedAt : null, LastError, LastErrorAt, Configured ? NextRun(items, backup) : null, items);
    }

    /// <summary>Jetzt sichern (Knopf in der Verwaltung oder Zeitplan); danach alte Generationen aufräumen.</summary>
    public async Task<BackupItemDto> RunAsync(Guid? actorId, CancellationToken cancellationToken)
    {
        if (_key is null)
        {
            throw new InvalidOperationException("Kein Sicherungsschlüssel eingerichtet (MYLIFE_BACKUP_KEY).");
        }

        await _gate.WaitAsync(cancellationToken);
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MyLifeDbContext>();
        try
        {
            var backup = (await settings.GetAsync(cancellationToken)).Backup;
            System.IO.Directory.CreateDirectory(Directory);
            var now = time.GetUtcNow();
            var stamp = now.UtcDateTime.ToString(NameFormat, CultureInfo.InvariantCulture);
            var path = Path.Combine(Directory, $"{Prefix}{stamp}{BackupFormat.FileExtension}");
            for (var n = 2; File.Exists(path); n++)
            {
                path = Path.Combine(Directory, $"{Prefix}{stamp}-{n.ToString(CultureInfo.InvariantCulture)}{BackupFormat.FileExtension}");
            }
            var result = await BackupArchive.CreateAsync(db, Path.GetFullPath(options.Value.DataDirectory), _key, path, backup.IncludeDocuments, now, cancellationToken);
            var created = Path.GetFileName(result.Path);
            // Die gerade erstellte Sicherung bleibt immer, auch wenn es heute schon eine gab.
            foreach (var old in ToDelete([.. List().Select(i => (i.Name, i.CreatedAt))], backup, TimeZoneInfo.Local).Where(n => n != created))
            {
                File.Delete(Path.Combine(Directory, old));
            }

            LastError = null;
            LastErrorAt = null;
            db.AuditLog.Add(AuditLog.Entry(AuditActions.AdminBackupCreated, actorId, null, now, null, "backup", null,
                string.Create(CultureInfo.InvariantCulture, $"{Path.GetFileName(result.Path)} {result.Size} {result.Documents}")));
            await db.SaveChangesAsync(cancellationToken);
            LogCreated(logger, created, result.Size, result.Documents);
            return new BackupItemDto(created, now, result.Size);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Npgsql.NpgsqlException or DbUpdateException or InvalidOperationException)
        {
            await FailedAsync(db, actorId, exception, cancellationToken);
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Ein Durchlauf des Zeitplans: sichern, wenn heute (Ortszeit) noch keine Sicherung da ist und die Uhrzeit erreicht ist.</summary>
    public async Task<bool> ProcessOnceAsync(CancellationToken cancellationToken)
    {
        if (!Configured)
        {
            return false;
        }

        var backup = (await settings.GetAsync(cancellationToken)).Backup;
        var next = NextRun(List(), backup);
        if (next is not { } due || time.GetUtcNow() < due)
        {
            return false;
        }

        try
        {
            await RunAsync(null, cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Npgsql.NpgsqlException or DbUpdateException or InvalidOperationException)
        {
            // Protokolliert und gemeldet; der nächste Durchlauf versucht es erneut.
            return false;
        }
    }

    /// <summary>Nächster Lauf: heute zur Uhrzeit, wenn heute noch keine Sicherung da ist, sonst morgen.</summary>
    public DateTimeOffset? NextRun(IReadOnlyList<BackupItemDto> items, BackupSettingsDto backup)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(backup);
        var zone = TimeZoneInfo.Local;
        var today = TimeZoneInfo.ConvertTime(time.GetUtcNow(), zone).Date;
        var doneToday = items.Any(i => TimeZoneInfo.ConvertTime(i.CreatedAt, zone).Date == today);
        var day = doneToday ? today.AddDays(1) : today;
        var local = day.AddHours(backup.Hour);
        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }

    /// <summary>
    /// Generationen: die neueste Sicherung je Tag für die letzten <c>KeepDaily</c> Tage, je Woche (ISO) für
    /// <c>KeepWeekly</c> Wochen, je Monat für <c>KeepMonthly</c> Monate; die neueste bleibt immer.
    /// </summary>
    public static IReadOnlyList<string> ToDelete(IReadOnlyList<(string Name, DateTimeOffset CreatedAt)> items, BackupSettingsDto keep, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(keep);
        var ordered = items.OrderByDescending(i => i.CreatedAt).ToList();
        var kept = new HashSet<string>(StringComparer.Ordinal);
        if (ordered.Count > 0)
        {
            kept.Add(ordered[0].Name);
        }

        void KeepPer(Func<DateTime, int> bucket, int count)
        {
            var buckets = new HashSet<int>();
            foreach (var (name, createdAt) in ordered)
            {
                if (buckets.Add(bucket(TimeZoneInfo.ConvertTime(createdAt, zone).DateTime)))
                {
                    if (buckets.Count > count)
                    {
                        return;
                    }

                    kept.Add(name);
                }
            }
        }

        KeepPer(d => (d.Year * 1000) + d.DayOfYear, keep.KeepDaily);
        KeepPer(d => (ISOWeek.GetYear(d) * 100) + ISOWeek.GetWeekOfYear(d), keep.KeepWeekly);
        KeepPer(d => (d.Year * 100) + d.Month, keep.KeepMonthly);
        return [.. ordered.Where(i => !kept.Contains(i.Name)).Select(i => i.Name)];
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!Configured)
        {
            LogNotConfigured(logger);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOnceAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is DbUpdateException or InvalidOperationException or Npgsql.NpgsqlException)
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

    private async Task FailedAsync(MyLifeDbContext db, Guid? actorId, Exception exception, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        LastError = exception.Message;
        LastErrorAt = now;
        LogFailed(logger, exception);
        try
        {
            db.ChangeTracker.Clear();
            db.AuditLog.Add(AuditLog.Entry(AuditActions.AdminBackupFailed, actorId, null, now, null, "backup", null, exception.GetType().Name));
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, TimeZoneInfo.Local).DateTime);
            if (_failureMailOn != today)
            {
                _failureMailOn = today;
                await using var scope = scopes.CreateAsyncScope();
                var mails = scope.ServiceProvider.GetRequiredService<MailOutbox>();
                var mailDb = scope.ServiceProvider.GetRequiredService<MyLifeDbContext>();
                foreach (var admin in await mailDb.Users.Where(u => u.IsAdmin && u.Status == UserStatus.Active).ToListAsync(cancellationToken))
                {
                    mails.Enqueue(admin, MailTemplate.BackupFailed, new Dictionary<string, string> { ["error"] = exception.Message });
                }

                await mailDb.SaveChangesAsync(cancellationToken);
            }

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception inner) when (inner is DbUpdateException or Npgsql.NpgsqlException or InvalidOperationException)
        {
            LogFailed(logger, inner);
        }
    }

    private static DateTimeOffset CreatedAt(FileInfo file)
    {
        var name = Path.GetFileNameWithoutExtension(file.Name)[Prefix.Length..];
        var stamp = name.Length >= NameFormat.Length ? name[..NameFormat.Length] : name;
        return DateTime.TryParseExact(stamp, NameFormat, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed)
            ? new DateTimeOffset(parsed, TimeSpan.Zero)
            : new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Sicherung {Name} erstellt ({Size} Byte, {Documents} Dokumente)")]
    private static partial void LogCreated(ILogger logger, string name, long size, int documents);

    [LoggerMessage(Level = LogLevel.Error, Message = "Sicherung fehlgeschlagen")]
    private static partial void LogFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Kein Sicherungsschlüssel (MYLIFE_BACKUP_KEY) eingerichtet: keine automatische Sicherung.")]
    private static partial void LogNotConfigured(ILogger logger);
}
