using System.Globalization;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyLife.Client.Services;
using MyLife.Server.Data;
using MyLife.Server.Infrastructure.Backup;
using MyLife.Shared.Admin;
using MyLife.Shared.Vault;
using Npgsql;
using static MyLife.Tests.Server.TestToken;

namespace MyLife.Tests.Server;

/// <summary>Server-Sicherung (M3.4): erstellen, in eine frische Datenbank zurückspielen und vergleichen; Schutz und Generationen.</summary>
public sealed class BackupTests(ServerFixture server) : IClassFixture<ServerFixture>, IAsyncLifetime
{
    private AuthTestSupport _account = null!;

    public async ValueTask InitializeAsync() => _account = await AuthTestSupport.ForAsync(server);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task ABackupRestoresIntoAnEmptyDatabaseExactly()
    {
        var browser = await VaultBrowser.OpenAsync(_account);
        var item = await browser.Vault.CreateAsync("bankAccount", VaultSamples.BankAccount("Sicherungskonto", "Backup"));
        var content = RandomNumberGenerator.GetBytes((2 * VaultLimits.ChunkSize) + 777);
        await browser.Vault.UploadAsync(item.Id, new MemoryStream(content), new DocumentMetadata { Label = "Auszug", FileName = "a.pdf", ContentType = "application/pdf", Size = content.Length });

        // Über die Verwaltung sichern.
        var (_, _, http) = await _account.LoginAsync();
        var admin = new AdminClient(http);
        var backup = await admin.BackupNowAsync(Ct);
        var status = await admin.GetBackupsAsync(Ct);
        Assert.True(status.Configured);
        Assert.Contains(status.Items, i => i.Name == backup.Name);
        var path = Path.Combine(server.DataDirectory, "backup", backup.Name);
        Assert.True(File.Exists(path));
        // Auch was in der Datenbank im Klartext steht (etwa die Mail-Adressen), ist in der Sicherung verschlüsselt.
        Assert.Equal(-1, (await File.ReadAllBytesAsync(path, Ct)).AsSpan().IndexOf(System.Text.Encoding.UTF8.GetBytes(AuthTestSupport.Email)));

        // In eine leere Datenbank und ein leeres Datenverzeichnis zurückspielen.
        var (restoredConnection, dropAsync) = await NewDatabaseAsync();
        var restoredData = Path.Combine(Path.GetTempPath(), "mylife-restore", Guid.NewGuid().ToString("N"));
        try
        {
            var manifest = await BackupArchive.RestoreAsync(path, restoredConnection, restoredData, BackupFormat.ParseKey(ServerFixture.BackupKey)!, Ct);
            Assert.True(manifest.Documents >= 1);

            // Alle Tabellen sind zurück; die, die sich seit der Sicherung nicht ändern, Zeile für Zeile gleich.
            // (Protokoll, Erinnerungen und Notfallzugang schreiben Hintergrunddienste weiter.)
            await using var original = new NpgsqlConnection(Configuration.GetConnectionString("MyLife"));
            await using var restored = new NpgsqlConnection(restoredConnection);
            await original.OpenAsync(Ct);
            await restored.OpenAsync(Ct);
            Assert.Equal(BackupArchive.OrderedTables(new MyLifeDbContext(new DbContextOptionsBuilder<MyLifeDbContext>().UseNpgsql(restoredConnection).Options).Model), manifest.Tables);
            foreach (var table in new[] { "users", "areas", "objects", "documents", "system_settings", "passkeys" })
            {
                Assert.Equal(await FingerprintAsync(original, table), await FingerprintAsync(restored, table));
            }

            Assert.NotEqual("0", await QueryAsync(restored, "SELECT count(*) FROM audit_log"));

            // Dokumente und Schlüsselring sind byte-genau zurück.
            var storageKey = (await QueryAsync(restored, $"SELECT storage_key FROM documents WHERE object_id = '{item.Id}'"))!;
            Assert.Equal(await File.ReadAllBytesAsync(Path.Combine(server.DataDirectory, "files", storageKey), Ct),
                await File.ReadAllBytesAsync(Path.Combine(restoredData, "files", storageKey), Ct));
            foreach (var key in Directory.GetFiles(Path.Combine(server.DataDirectory, "keys")))
            {
                Assert.Equal(await File.ReadAllBytesAsync(key, Ct), await File.ReadAllBytesAsync(Path.Combine(restoredData, "keys", Path.GetFileName(key)), Ct));
            }

            // Fortlaufende Nummern laufen nach dem Zurückspielen weiter.
            await using var db = new MyLifeDbContext(new DbContextOptionsBuilder<MyLifeDbContext>().UseNpgsql(restoredConnection).Options);
            db.AuditLog.Add(new AuditEntry { At = DateTimeOffset.UtcNow, Action = "restore-test" });
            await db.SaveChangesAsync(Ct);
            Assert.Empty(await db.Database.GetPendingMigrationsAsync(Ct));
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await dropAsync();
            if (Directory.Exists(restoredData))
            {
                Directory.Delete(restoredData, recursive: true);
            }
        }
    }

    [Fact]
    public async Task AWrongKeyATamperedOrATruncatedBackupIsRejected()
    {
        var backups = server.Services.GetRequiredService<BackupService>();
        var item = await backups.RunAsync(null, Ct);
        var path = Path.Combine(server.DataDirectory, "backup", item.Name);
        var bytes = await File.ReadAllBytesAsync(path, Ct);
        var key = BackupFormat.ParseKey(ServerFixture.BackupKey)!;
        var (connection, dropAsync) = await NewDatabaseAsync();
        var target = Path.Combine(Path.GetTempPath(), "mylife-restore", Guid.NewGuid().ToString("N"));
        try
        {
            await Assert.ThrowsAsync<CryptographicException>(() => BackupArchive.RestoreAsync(path, connection, target, RandomNumberGenerator.GetBytes(32), Ct));

            var tampered = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.mlbak");
            var copy = bytes.ToArray();
            copy[^20] ^= 1;
            await File.WriteAllBytesAsync(tampered, copy, Ct);
            await Assert.ThrowsAsync<CryptographicException>(() => BackupArchive.RestoreAsync(tampered, connection, target, key, Ct));

            // Ein abgeschnittener Rest fällt an der Markierung des letzten Blocks auf.
            await File.WriteAllBytesAsync(tampered, bytes[..(bytes.Length - 1000)], Ct);
            await Assert.ThrowsAnyAsync<Exception>(() => BackupArchive.RestoreAsync(tampered, connection, target, key, Ct));

            await File.WriteAllBytesAsync(tampered, "kein Backup"u8.ToArray(), Ct);
            await Assert.ThrowsAnyAsync<Exception>(() => BackupArchive.RestoreAsync(tampered, connection, target, key, Ct));
            File.Delete(tampered);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await dropAsync();
        }
    }

    [Fact]
    public async Task TheScheduleRunsOncePerDayAfterTheConfiguredHour()
    {
        var backups = server.Services.GetRequiredService<BackupService>();
        server.Clock.Advance(TimeSpan.FromDays(400));
        var (_, _, http) = await _account.LoginAsync();
        var settings = await new AdminClient(http).GetSettingsAsync(Ct);
        var next = backups.NextRun(backups.List(), settings.Backup)!.Value;
        if (server.Clock.GetUtcNow() < next)
        {
            server.Clock.Advance(next - server.Clock.GetUtcNow() + TimeSpan.FromMinutes(1));
        }

        Assert.True(await backups.ProcessOnceAsync(Ct));
        Assert.False(await backups.ProcessOnceAsync(Ct));
        Assert.True(backups.NextRun(backups.List(), settings.Backup) > server.Clock.GetUtcNow());
    }

    [Fact]
    public async Task TheCommandLineRestoreNeedsConfirmationAndTheSettingsAreChecked()
    {
        var item = await server.Services.GetRequiredService<BackupService>().RunAsync(null, Ct);
        var options = new MyLife.Server.Infrastructure.MyLifeOptions { BackupKey = ServerFixture.BackupKey, DataDirectory = server.DataDirectory };
        var path = Path.Combine(server.DataDirectory, "backup", item.Name);
        Assert.Equal(2, await BackupCli.RestoreAsync(["restore", path], Configuration, options));
        Assert.Equal(2, await BackupCli.RestoreAsync(["restore", "/gibt/es/nicht.mlbak", "--confirm"], Configuration, options));

        Assert.Contains("backup.hour", AdminSettingsValidation.Validate(new AdminSettingsDto { Backup = new BackupSettingsDto { Hour = 24 } }).Keys);
        Assert.Contains("backup.keep", AdminSettingsValidation.Validate(new AdminSettingsDto { Backup = new BackupSettingsDto { KeepDaily = 0 } }).Keys);
        Assert.DoesNotContain(AdminSettingsValidation.Validate(new AdminSettingsDto()).Keys, k => k.StartsWith("backup", StringComparison.Ordinal));
    }

    [Fact]
    public void GenerationsKeepDailyWeeklyAndMonthlyBackups()
    {
        var zone = TimeZoneInfo.Utc;
        var start = new DateTimeOffset(2026, 1, 1, 3, 0, 0, TimeSpan.Zero);
        var items = Enumerable.Range(0, 120).Select(d => ($"b{d:000}", start.AddDays(d))).ToList();
        items.Add(("b119-second", start.AddDays(119).AddHours(2)));

        var deleted = BackupService.ToDelete(items, new BackupSettingsDto { KeepDaily = 7, KeepWeekly = 4, KeepMonthly = 3 }, zone).ToHashSet();
        var kept = items.Select(i => i.Item1).Where(n => !deleted.Contains(n)).ToList();

        // Neueste Sicherung je Tag der letzten 7 Tage; am letzten Tag nur die spätere.
        Assert.Contains("b119-second", kept);
        Assert.DoesNotContain("b119", kept);
        Assert.All(Enumerable.Range(113, 6), d => Assert.Contains($"b{d:000}", kept));
        // Dazu je eine je Woche und Monat; insgesamt deutlich weniger als 121.
        Assert.InRange(kept.Count, 8, 7 + 4 + 3);
        Assert.Empty(BackupService.ToDelete([], new BackupSettingsDto(), zone));
    }

    private IConfiguration Configuration => server.Services.GetRequiredService<IConfiguration>();

    private async Task<(string ConnectionString, Func<Task> DropAsync)> NewDatabaseAsync()
    {
        var builder = new NpgsqlConnectionStringBuilder(Configuration.GetConnectionString("MyLife"));
        var name = $"restore_{Guid.NewGuid():N}";
        await using (var admin = new NpgsqlConnection(builder.ConnectionString))
        {
            await admin.OpenAsync(Ct);
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin);
            await create.ExecuteNonQueryAsync(Ct);
        }

        var original = builder.ConnectionString;
        builder.Database = name;
        return (builder.ConnectionString, async () =>
        {
            await using var admin = new NpgsqlConnection(original);
            await admin.OpenAsync();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{name}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        });
    }

    private static async Task<string> FingerprintAsync(NpgsqlConnection connection, string table) =>
        (await QueryAsync(connection, $"SELECT count(*)::text || ':' || coalesce(md5(string_agg(t::text, '|' ORDER BY t::text)), '') FROM \"{table}\" t"))!;

    private static async Task<string?> QueryAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return Convert.ToString(await command.ExecuteScalarAsync(Ct), CultureInfo.InvariantCulture);
    }
}
