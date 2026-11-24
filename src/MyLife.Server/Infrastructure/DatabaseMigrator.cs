using Microsoft.EntityFrameworkCore;
using MyLife.Server.Data;

namespace MyLife.Server.Infrastructure;

/// <summary>Spielt beim Start ausstehende Migrationen ein. Wartet, bis PostgreSQL erreichbar ist.</summary>
internal static partial class DatabaseMigrator
{
    public static async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MyLifeDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseMigrator));

        const int maxAttempts = 30;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync(cancellationToken);
                LogMigrated(logger);
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts && ex is Npgsql.NpgsqlException or TimeoutException)
            {
                LogWaiting(logger, attempt, maxAttempts);
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Datenbank ist auf dem aktuellen Stand.")]
    private static partial void LogMigrated(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Datenbank noch nicht erreichbar (Versuch {Attempt}/{MaxAttempts}), neuer Versuch in 2 s.")]
    private static partial void LogWaiting(ILogger logger, int attempt, int maxAttempts);
}
