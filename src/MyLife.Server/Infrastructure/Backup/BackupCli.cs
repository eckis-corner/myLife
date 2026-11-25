namespace MyLife.Server.Infrastructure.Backup;

/// <summary>
/// Rücksicherung von der Kommandozeile (M3.4, docs/betrieb/installation.md):
/// <c>docker compose run --rm app restore /data/backup/mylife-….mlbak --confirm</c>. Die laufende App vorher stoppen.
/// Die Datenbank wird dabei vollständig ersetzt; ohne <c>--confirm</c> passiert nichts.
/// </summary>
internal static class BackupCli
{
    public static async Task<int> RestoreAsync(string[] args, IConfiguration configuration, MyLifeOptions options)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(options);
        if (args.Length < 2 || !File.Exists(args[1]))
        {
            await Console.Error.WriteLineAsync("Aufruf: restore <Sicherungsdatei> --confirm");
            return 2;
        }

        if (!args.Contains("--confirm", StringComparer.Ordinal))
        {
            await Console.Error.WriteLineAsync(
                "Die Rücksicherung ersetzt die gesamte Datenbank, die Dokumente und die Schlüssel. Zum Ausführen --confirm anhängen.");
            return 2;
        }

        var key = BackupFormat.ParseKey(options.BackupKey);
        var connectionString = configuration.GetConnectionString("MyLife");
        if (key is null || string.IsNullOrWhiteSpace(connectionString))
        {
            await Console.Error.WriteLineAsync("MYLIFE_BACKUP_KEY oder die Datenbankverbindung fehlt.");
            return 2;
        }

        try
        {
            var manifest = await BackupArchive.RestoreAsync(args[1], connectionString, Path.GetFullPath(options.DataDirectory), key, CancellationToken.None);
            await Console.Out.WriteLineAsync(
                $"Zurückgespielt: Sicherung vom {manifest.CreatedAt:u}, {manifest.Tables.Count} Tabellen, {manifest.Documents} Dokumente, {manifest.Keys} Schlüssel.");
            return 0;
        }
        catch (Exception exception) when (exception is InvalidDataException or System.Security.Cryptography.CryptographicException or IOException
                                              or Npgsql.NpgsqlException or EndOfStreamException)
        {
            await Console.Error.WriteLineAsync($"Rücksicherung fehlgeschlagen: {exception.Message}");
            return 1;
        }
    }
}
