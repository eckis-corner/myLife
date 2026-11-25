using System.Data;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using MyLife.Server.Data;
using Npgsql;

namespace MyLife.Server.Infrastructure.Backup;

/// <summary>Ergebnis einer Sicherung.</summary>
internal sealed record BackupResult(string Path, long Size, int Tables, int Documents);

/// <summary>
/// Erstellt und spielt Sicherungen zurück (M3.4). Statt <c>pg_dump</c> (das im App-Image fehlt) liest myLife jede
/// Tabelle mit <c>COPY … (FORMAT binary)</c> aus einem gemeinsamen Schnappschuss (REPEATABLE READ): konsistent,
/// ohne zusätzliches Programm und in den Tests vollständig prüfbar. Dazu die Dokumente, auf die dieser Schnappschuss
/// verweist, und der Schlüsselring von Data Protection.
/// </summary>
internal static class BackupArchive
{
    public const string ContentFormat = "mylife-backup-content";

    /// <summary>Tabellen des Modells, jede nach den Tabellen, auf die sie verweist.</summary>
    public static IReadOnlyList<string> OrderedTables(IModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var tables = model.GetEntityTypes().Where(t => t.GetTableName() is not null && t.GetViewName() is null)
            .GroupBy(t => t.GetTableName()!)
            .ToDictionary(g => g.Key, g => g.SelectMany(t => t.GetForeignKeys()).Select(f => f.PrincipalEntityType.GetTableName()!)
                .Where(p => p != g.Key).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);
        var ordered = new List<string>();
        var done = new HashSet<string>(StringComparer.Ordinal);
        while (ordered.Count < tables.Count)
        {
            var next = tables.Keys.Where(t => !done.Contains(t) && tables[t].All(done.Contains)).Order(StringComparer.Ordinal).FirstOrDefault()
                ?? throw new InvalidOperationException("Zyklische Fremdschlüssel im Modell.");
            ordered.Add(next);
            done.Add(next);
        }

        return ordered;
    }

    public static async Task<BackupResult> CreateAsync(
        MyLifeDbContext db, string dataDirectory, byte[] backupKey, string targetPath, bool includeDocuments, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(backupKey);
        var salt = RandomNumberGenerator.GetBytes(32);
        var header = new BackupHeader("mylife-backup", 1, now, typeof(BackupArchive).Assembly.GetName().Version?.ToString() ?? "0", Convert.ToBase64String(salt));
        var partial = targetPath + ".partial";
        var tables = OrderedTables(db.Model);
        var documents = 0;
        var keys = 0;
        try
        {
            await using (var file = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 16, useAsync: true))
            {
                var rawHeader = await BackupFormat.WriteHeaderAsync(file, header, cancellationToken);
                await using var output = new BackupEncryptingStream(file, BackupFormat.DeriveKey(backupKey, salt), SHA256.HashData(rawHeader));

                await db.Database.OpenConnectionAsync(cancellationToken);
                try
                {
                    var connection = (NpgsqlConnection)db.Database.GetDbConnection();
                    await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
                    await db.Database.UseTransactionAsync(transaction, cancellationToken);
                    var migration = (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).Last();
                    var storageKeys = includeDocuments
                        ? await db.Documents.AsNoTracking().Select(d => d.StorageKey).ToListAsync(cancellationToken)
                        : [];
                    var keyFiles = Directory.Exists(Path.Combine(dataDirectory, "keys"))
                        ? Directory.GetFiles(Path.Combine(dataDirectory, "keys")).Order(StringComparer.Ordinal).ToList()
                        : [];
                    var documentFiles = storageKeys.Select(k => (Key: k, Path: Path.Combine(dataDirectory, "files", k))).Where(f => File.Exists(f.Path)).ToList();
                    documents = documentFiles.Count;
                    keys = keyFiles.Count;

                    var manifest = BackupFormat.Serialize(new BackupManifest(ContentFormat, 1, now, migration, tables, documents, keys));
                    await BackupFormat.WriteEntryAsync(output, BackupEntryKind.Manifest, "manifest.json", manifest.Length, s => s.WriteAsync(manifest, cancellationToken).AsTask(), cancellationToken);

                    foreach (var table in tables)
                    {
                        using var buffer = new MemoryStream();
                        await using (var copy = await connection.BeginRawBinaryCopyAsync($"COPY \"{table}\" TO STDOUT (FORMAT binary)", cancellationToken))
                        {
                            await copy.CopyToAsync(buffer, cancellationToken);
                        }

                        buffer.Position = 0;
                        await BackupFormat.WriteEntryAsync(output, BackupEntryKind.Table, table, buffer.Length, s => buffer.CopyToAsync(s, cancellationToken), cancellationToken);
                    }

                    await transaction.CommitAsync(cancellationToken);
                    await db.Database.UseTransactionAsync(null, cancellationToken);

                    foreach (var (key, path) in documentFiles)
                    {
                        await CopyFileAsync(output, BackupEntryKind.Document, key, path, cancellationToken);
                    }

                    foreach (var path in keyFiles)
                    {
                        await CopyFileAsync(output, BackupEntryKind.Key, Path.GetFileName(path), path, cancellationToken);
                    }
                }
                finally
                {
                    await db.Database.CloseConnectionAsync();
                }

                await output.WriteAsync(new[] { (byte)BackupEntryKind.End }, cancellationToken);
                await output.CompleteAsync(cancellationToken);
            }

            File.Move(partial, targetPath);
            return new BackupResult(targetPath, new FileInfo(targetPath).Length, tables.Count, documents);
        }
        catch
        {
            File.Delete(partial);
            throw;
        }
    }

    /// <summary>
    /// Spielt eine Sicherung in die Datenbank und das Datenverzeichnis zurück. Die Datenbank wird dabei <b>geleert</b>:
    /// Schema neu, Migrationen bis zum Stand der Sicherung, Daten per COPY, danach die übrigen Migrationen.
    /// </summary>
    public static async Task<BackupManifest> RestoreAsync(
        string path, string connectionString, string dataDirectory, byte[] backupKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(backupKey);
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, useAsync: true);
        var (header, rawHeader) = await BackupFormat.ReadHeaderAsync(file, cancellationToken);
        if (header is not { Format: "mylife-backup", Version: 1 })
        {
            throw new InvalidDataException("Unbekanntes Sicherungsformat.");
        }

        await using var input = new BackupDecryptingStream(file, file.Length - file.Position,
            BackupFormat.DeriveKey(backupKey, Convert.FromBase64String(header.Salt)), SHA256.HashData(rawHeader));

        var (kind, _, length) = await BackupFormat.ReadEntryHeadAsync(input, cancellationToken);
        if (kind != BackupEntryKind.Manifest || length > 1024 * 1024)
        {
            throw new InvalidDataException("Inhaltsverzeichnis fehlt.");
        }

        var manifestBytes = new byte[length];
        await input.ReadExactlyAsync(manifestBytes, cancellationToken);
        var manifest = BackupFormat.Deserialize<BackupManifest>(manifestBytes);
        if (manifest.Format != ContentFormat || manifest.Version != 1)
        {
            throw new InvalidDataException("Unbekanntes Sicherungsformat.");
        }

        var options = new DbContextOptionsBuilder<MyLifeDbContext>().UseNpgsql(connectionString).Options;
        await using var db = new MyLifeDbContext(options);
        var migrator = db.GetService<IMigrator>();
        if (!db.Database.GetMigrations().Contains(manifest.Migration))
        {
            throw new InvalidDataException($"Die Sicherung stammt von einer neueren Version (Migration {manifest.Migration}).");
        }

        // Leeren und auf den Stand der Sicherung bringen.
        await db.Database.ExecuteSqlRawAsync("DROP SCHEMA IF EXISTS public CASCADE; CREATE SCHEMA public;", cancellationToken);
        await migrator.MigrateAsync(manifest.Migration, cancellationToken);

        await db.Database.OpenConnectionAsync(cancellationToken);
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        var documentsDirectory = Path.Combine(dataDirectory, "files");
        var keysDirectory = Path.Combine(dataDirectory, "keys");
        var documentsSeen = false;
        var keysSeen = false;
        while (true)
        {
            (kind, var name, length) = await BackupFormat.ReadEntryHeadAsync(input, cancellationToken);
            if (kind == BackupEntryKind.End)
            {
                break;
            }

            switch (kind)
            {
                case BackupEntryKind.Table when manifest.Tables.Contains(name):
                    await using (var copy = await connection.BeginRawBinaryCopyAsync($"COPY \"{name}\" FROM STDIN (FORMAT binary)", cancellationToken))
                    {
                        await CopyExactlyAsync(input, copy, length, cancellationToken);
                    }

                    break;

                case BackupEntryKind.Document:
                    if (!documentsSeen)
                    {
                        ClearDirectory(documentsDirectory);
                        documentsSeen = true;
                    }

                    await WriteFileAsync(input, Path.Combine(documentsDirectory, SafeName(name)), length, cancellationToken);
                    break;

                case BackupEntryKind.Key:
                    if (!keysSeen)
                    {
                        ClearDirectory(keysDirectory);
                        keysSeen = true;
                    }

                    await WriteFileAsync(input, Path.Combine(keysDirectory, SafeName(name)), length, cancellationToken);
                    break;

                default:
                    throw new InvalidDataException($"Unerwarteter Eintrag {kind} {name} in der Sicherung.");
            }
        }

        // Fortlaufende Nummern (etwa im Protokoll) nach dem Zurückspielen auf den höchsten Wert setzen.
        var identities = new List<(string Table, string Column)>();
        await using (var command = new NpgsqlCommand(
            "SELECT table_name, column_name FROM information_schema.columns WHERE table_schema = 'public' AND is_identity = 'YES'", connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                identities.Add((reader.GetString(0), reader.GetString(1)));
            }
        }

        foreach (var (table, column) in identities)
        {
            await using var reset = new NpgsqlCommand(
                $"SELECT setval(pg_get_serial_sequence('\"{table}\"', '{column}'), COALESCE((SELECT MAX(\"{column}\") FROM \"{table}\"), 0) + 1, false)", connection);
            await reset.ExecuteScalarAsync(cancellationToken);
        }

        await db.Database.CloseConnectionAsync();
        await migrator.MigrateAsync(cancellationToken: cancellationToken);
        return manifest;
    }

    private static async Task CopyFileAsync(Stream output, BackupEntryKind kind, string name, string path, CancellationToken cancellationToken)
    {
        await using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, useAsync: true);
        await BackupFormat.WriteEntryAsync(output, kind, name, source.Length, s => source.CopyToAsync(s, cancellationToken), cancellationToken);
    }

    private static async Task WriteFileAsync(Stream input, string path, long length, CancellationToken cancellationToken)
    {
        await using var target = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16, useAsync: true);
        await CopyExactlyAsync(input, target, length, cancellationToken);
    }

    private static async Task CopyExactlyAsync(Stream input, Stream output, long length, CancellationToken cancellationToken)
    {
        var buffer = new byte[1 << 16];
        while (length > 0)
        {
            var read = await input.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, length)), cancellationToken);
            if (read == 0)
            {
                throw new InvalidDataException("Sicherung ist unvollständig.");
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            length -= read;
        }
    }

    private static void ClearDirectory(string directory)
    {
        Directory.CreateDirectory(directory);
        foreach (var file in Directory.GetFiles(directory))
        {
            File.Delete(file);
        }
    }

    /// <summary>Namen aus der Sicherung landen nur als Dateiname im Zielordner, nie woanders.</summary>
    private static string SafeName(string name)
    {
        var file = Path.GetFileName(name);
        return file.Length == 0 || file != name || file is "." or ".." ? throw new InvalidDataException("Ungültiger Dateiname in der Sicherung.") : file;
    }
}
