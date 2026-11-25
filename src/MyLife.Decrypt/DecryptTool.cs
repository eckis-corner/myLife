using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using MyLife.Crypto;
using MyLife.Shared.Vault;

namespace MyLife.Decrypt;

/// <summary>
/// mylife-decrypt (M3.5): öffnet einen JSON-Export (docs/format.md) mit dem Master-Passwort oder den 24 Wörtern des
/// Recovery-Kits und ein Notfallpaket mit den 24 Wörtern vom Schlüsselblatt. Heraus kommt ein Ordner mit einer
/// Übersicht, je Eintrag einem lesbaren Datenblatt (.txt) und den Originaldokumenten. Alles bleibt auf dem Rechner.
/// </summary>
public static partial class DecryptTool
{
    private const string Usage = """
        mylife-decrypt – öffnet myLife-Daten ohne Server

          mylife-decrypt export  <Datei.mylife.json> <Zielordner> [--en]
              JSON-Export (Export & Druck → Datensicherung). Abgefragt wird das Master-Passwort zum Zeitpunkt
              des Exports oder die 24 Wörter des Recovery-Kits.

          mylife-decrypt package <myLife-Notfallpaket.html> <Zielordner> [--en]
              Notfallpaket. Abgefragt werden die 24 Wörter vom Schlüsselblatt.

        Statt der Abfrage kann das Geheimnis in der Umgebungsvariablen MYLIFE_SECRET stehen.
        Der Zielordner muss leer sein oder darf noch nicht existieren. --en: englische Beschriftungen.
        """;

    private static readonly JsonSerializerOptions ExportJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private static readonly JsonSerializerOptions IndentedPayload = new(VaultPayload.JsonOptions) { WriteIndented = true };

    public static async Task<int> RunAsync(string[] args, TextReader input, TextWriter output, TextWriter error, string? secret = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        var positional = args.Where(a => !a.StartsWith("--", StringComparison.Ordinal)).ToList();
        if (positional.Count != 3 || positional[0] is not ("export" or "package"))
        {
            await output.WriteLineAsync(Usage);
            return 2;
        }

        var (command, source, target) = (positional[0], positional[1], positional[2]);
        if (!File.Exists(source))
        {
            await error.WriteLineAsync($"Datei nicht gefunden: {source}");
            return 2;
        }

        if (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any())
        {
            await error.WriteLineAsync($"Der Zielordner ist nicht leer: {target}");
            return 2;
        }

        var texts = Texts.Load(args.Contains("--en", StringComparer.Ordinal) ? "en" : "de");
        secret ??= await PromptAsync(command == "export" ? "Master-Passwort oder die 24 Wörter des Recovery-Kits: " : "Die 24 Wörter vom Schlüsselblatt: ", input, output);
        try
        {
            var count = command == "export"
                ? await ExportAsync(source, target, secret, texts)
                : await PackageAsync(source, target, secret, texts);
            await output.WriteLineAsync($"Fertig: {count} Einträge in {Path.GetFullPath(target)}. Die Übersicht steht in index.txt.");
            return 0;
        }
        catch (Exception exception) when (exception is System.Security.Cryptography.CryptographicException or JsonException or InvalidDataException
                                              or FormatException or IOException or KeyNotFoundException)
        {
            await error.WriteLineAsync($"Das hat nicht geklappt: {exception.Message}");
            return 1;
        }
    }

    // ---------- JSON-Export (docs/format.md) ----------

    private static async Task<int> ExportAsync(string source, string target, string secret, Texts texts)
    {
        var export = JsonSerializer.Deserialize<ExportModel>(await File.ReadAllBytesAsync(source), ExportJson)
            ?? throw new InvalidDataException("Leere Datei.");
        if (export.Format != "mylife-export" || export.Version != 1)
        {
            throw new InvalidDataException("Das ist kein myLife-Export (Version 1).");
        }

        await using var crypto = new ReferenceMyLifeCrypto();

        // 1. Tresorschlüssel: mit den 24 Wörtern des Recovery-Kits oder mit dem Master-Passwort.
        CryptoKeyHandle vmk;
        if (Bip39.TryDecode(secret, out var recoveryKey) && export.WrappedVmkByRecovery is { } byRecovery)
        {
            vmk = await crypto.UnwrapKeyAsync(await crypto.ImportRecoveryKeyAsync(recoveryKey), byRecovery, KeyContexts.Vmk);
        }
        else
        {
            var master = await crypto.DeriveMasterKeyAsync(secret, export.Kdf.Salt, new Argon2Parameters(export.Kdf.MemoryKiB, export.Kdf.Iterations, export.Kdf.Parallelism));
            vmk = await crypto.UnwrapKeyAsync(await crypto.DeriveKeyEncryptionKeyAsync(master), export.WrappedVmk, KeyContexts.Vmk);
        }

        // 2. Bereichsschlüssel, 3. Einträge (Briefe mit eigenem Schlüssel).
        var areaKeys = new Dictionary<int, CryptoKeyHandle>();
        foreach (var area in export.Areas)
        {
            areaKeys[area.Area] = await crypto.UnwrapKeyAsync(vmk, area.WrappedKey, KeyContexts.Area(area.Area));
        }

        var contentKeys = new Dictionary<Guid, CryptoKeyHandle>();
        var items = new List<(ExportObjectModel Object, ObjectTypeDefinition Type, VaultPayload Payload)>();
        foreach (var item in export.Objects)
        {
            var type = ObjectCatalog.Find(item.Type) ?? throw new InvalidDataException($"Unbekannter Eintragstyp {item.Type}.");
            var key = areaKeys[item.Area];
            if (item.WrappedKey is { } wrapped)
            {
                key = await crypto.UnwrapKeyAsync(key, wrapped, KeyContexts.ObjectKey(item.Id));
            }

            contentKeys[item.Id] = key;
            items.Add((item, type, VaultPayload.FromUtf8(await crypto.DecryptAsync(key, item.Payload, KeyContexts.VaultObject(item.Type, item.Id)))));
        }

        var titles = items.ToDictionary(i => i.Object.Id, i => i.Payload.Title);
        Directory.CreateDirectory(target);
        var index = new StringBuilder();
        index.AppendLine(CultureInfo.InvariantCulture, $"myLife – {export.DisplayName} ({export.Email})");
        index.AppendLine(CultureInfo.InvariantCulture, $"Export vom {export.CreatedAt.ToLocalTime().ToString("g", texts.Culture)}");
        foreach (var area in items.GroupBy(i => i.Object.Area).OrderBy(g => g.Key))
        {
            var folder = Path.Combine(target, Folder(area.Key, texts.Area(area.Key)));
            Directory.CreateDirectory(folder);
            index.AppendLine().AppendLine(texts.Area(area.Key));
            foreach (var (item, type, payload) in area.OrderBy(i => texts.Type(i.Type.Key)).ThenBy(i => i.Payload.Title, StringComparer.CurrentCultureIgnoreCase))
            {
                var name = Unique(folder, SafeName($"{payload.Title} ({texts.Type(type.Key)})"));
                var sheet = new StringBuilder();
                sheet.AppendLine(payload.Title).AppendLine(item.Status == ObjectStatus.Ended ? $"{texts.Type(type.Key)} · {texts["ObjectStatus_Ended"]}" : texts.Type(type.Key));
                sheet.AppendLine(new string('-', 40));
                foreach (var (label, value) in texts.Rows(type, payload, id => titles.GetValueOrDefault(id)))
                {
                    sheet.AppendLine(CultureInfo.InvariantCulture, $"{label}: {value}");
                }

                // 4. Dokumente: Dateischlüssel entpacken, Metadaten und Blöcke entschlüsseln.
                var files = new List<string>();
                foreach (var document in export.Documents.Where(d => d.ObjectId == item.Id))
                {
                    var fileKey = await crypto.UnwrapKeyAsync(contentKeys[item.Id], document.WrappedFileKey, KeyContexts.FileKey(document.Id));
                    var metadata = DocumentMetadata.FromUtf8(await crypto.DecryptAsync(contentKeys[item.Id], document.Metadata, KeyContexts.DocumentMetadata(document.Id)));
                    if (document.Content is not { } cipher)
                    {
                        files.Add($"{metadata.FileName} ({texts["DecryptNotInExport"]})");
                        continue;
                    }

                    var documentFolder = Path.Combine(folder, name);
                    Directory.CreateDirectory(documentFolder);
                    var fileName = Unique(documentFolder, SafeName(metadata.FileName));
                    await File.WriteAllBytesAsync(Path.Combine(documentFolder, fileName), await DecryptDocumentAsync(crypto, fileKey, document.Id, cipher));
                    files.Add(string.IsNullOrWhiteSpace(metadata.Label) ? fileName : $"{metadata.Label}: {fileName}");
                }

                if (files.Count > 0)
                {
                    sheet.AppendLine(CultureInfo.InvariantCulture, $"{texts["DocumentsTitle"]}: {string.Join(", ", files)}");
                }

                await File.WriteAllTextAsync(Path.Combine(folder, $"{name}.txt"), sheet.ToString());
                await File.WriteAllBytesAsync(Path.Combine(folder, $"{name}.json"), JsonSerializer.SerializeToUtf8Bytes(payload, IndentedPayload));
                index.AppendLine(CultureInfo.InvariantCulture, $"  - {payload.Title} ({texts.Type(type.Key)}) → {Path.GetFileName(folder)}/{name}.txt");
            }
        }

        await File.WriteAllTextAsync(Path.Combine(target, "index.txt"), index.ToString());
        return items.Count;
    }

    private static async Task<byte[]> DecryptDocumentAsync(ReferenceMyLifeCrypto crypto, CryptoKeyHandle fileKey, Guid documentId, byte[] cipher)
    {
        using var plain = new MemoryStream();
        var block = VaultLimits.ChunkSize + VaultLimits.ChunkOverhead;
        var count = Math.Max(1, (cipher.Length + block - 1) / block);
        for (var index = 0; index < count; index++)
        {
            var start = index * block;
            var chunk = cipher.AsSpan(start, Math.Min(block, cipher.Length - start)).ToArray();
            await plain.WriteAsync(await crypto.DecryptChunkAsync(fileKey, index, index == count - 1, chunk, KeyContexts.Document(documentId)));
        }

        return plain.ToArray();
    }

    // ---------- Notfallpaket (docs/format.md, Abschnitt „Notfallpaket“) ----------

    private static async Task<int> PackageAsync(string source, string target, string secret, Texts texts)
    {
        var html = await File.ReadAllTextAsync(source);
        var match = PackageBlock().Match(html);
        if (!match.Success)
        {
            throw new InvalidDataException("Das ist kein myLife-Notfallpaket.");
        }

        using var header = JsonDocument.Parse(match.Groups[1].Value);
        if (header.RootElement.GetProperty("format").GetString() != "mylife-package")
        {
            throw new InvalidDataException("Das ist kein myLife-Notfallpaket.");
        }

        if (!Bip39.TryDecode(secret, out var packageKey))
        {
            throw new InvalidDataException("Die 24 Wörter passen nicht zusammen. Bitte prüfe sie noch einmal, besonders die Reihenfolge.");
        }

        await using var crypto = new ReferenceMyLifeCrypto();
        var key = await crypto.ImportPackageKeyAsync(packageKey);
        using var content = JsonDocument.Parse(await crypto.DecryptAsync(key, Convert.FromBase64String(header.RootElement.GetProperty("data").GetString()!), "package"));
        var root = content.RootElement;
        Directory.CreateDirectory(target);
        var index = new StringBuilder();
        index.AppendLine(CultureInfo.InvariantCulture, $"myLife – {root.GetProperty("owner").GetString()}");
        index.AppendLine(CultureInfo.InvariantCulture, $"Notfallpaket vom {root.GetProperty("createdAt").GetDateTimeOffset().ToLocalTime().ToString("g", texts.Culture)}");
        var count = 0;
        foreach (var area in root.GetProperty("areas").EnumerateArray())
        {
            var title = area.GetProperty("title").GetString()!;
            var folder = Path.Combine(target, Folder(area.GetProperty("area").GetInt32(), title));
            Directory.CreateDirectory(folder);
            index.AppendLine().AppendLine(title);
            foreach (var item in area.GetProperty("items").EnumerateArray())
            {
                var itemTitle = item.GetProperty("title").GetString()!;
                var kind = item.GetProperty("kind").GetString()!;
                var name = Unique(folder, SafeName($"{itemTitle} ({kind})"));
                var sheet = new StringBuilder().AppendLine(itemTitle).AppendLine(kind).AppendLine(new string('-', 40));
                foreach (var row in item.GetProperty("rows").EnumerateArray())
                {
                    sheet.AppendLine(CultureInfo.InvariantCulture, $"{row.GetProperty("label").GetString()}: {row.GetProperty("value").GetString()}");
                }

                if (item.TryGetProperty("links", out var links) && links.GetArrayLength() > 0)
                {
                    sheet.AppendLine(CultureInfo.InvariantCulture, $"{texts["CommonLinks"]}: {string.Join(", ", links.EnumerateArray().Select(l => l.GetString()))}");
                }

                var files = new List<string>();
                foreach (var document in item.GetProperty("documents").EnumerateArray())
                {
                    var documentFolder = Path.Combine(folder, name);
                    Directory.CreateDirectory(documentFolder);
                    var fileName = Unique(documentFolder, SafeName(document.GetProperty("fileName").GetString()!));
                    await File.WriteAllBytesAsync(Path.Combine(documentFolder, fileName), Convert.FromBase64String(document.GetProperty("data").GetString()!));
                    files.Add(fileName);
                }

                if (files.Count > 0)
                {
                    sheet.AppendLine(CultureInfo.InvariantCulture, $"{texts["DocumentsTitle"]}: {string.Join(", ", files)}");
                }

                await File.WriteAllTextAsync(Path.Combine(folder, $"{name}.txt"), sheet.ToString());
                index.AppendLine(CultureInfo.InvariantCulture, $"  - {itemTitle} ({kind}) → {Path.GetFileName(folder)}/{name}.txt");
                count++;
            }
        }

        await File.WriteAllTextAsync(Path.Combine(target, "index.txt"), index.ToString());
        return count;
    }

    // ---------- Hilfen ----------

    private static async Task<string> PromptAsync(string prompt, TextReader input, TextWriter output)
    {
        await output.WriteAsync(prompt);
        if (ReferenceEquals(input, Console.In) && !Console.IsInputRedirected)
        {
            // Verdeckte Eingabe: Das Passwort erscheint nicht auf dem Bildschirm.
            var secret = new StringBuilder();
            while (Console.ReadKey(intercept: true) is var key && key.Key != ConsoleKey.Enter)
            {
                if (key.Key == ConsoleKey.Backspace)
                {
                    secret.Length = Math.Max(0, secret.Length - 1);
                }
                else if (!char.IsControl(key.KeyChar))
                {
                    secret.Append(key.KeyChar);
                }
            }

            await output.WriteLineAsync();
            return secret.ToString();
        }

        return await input.ReadLineAsync() ?? string.Empty;
    }

    private static string Folder(int area, string title) => $"{area.ToString("00", CultureInfo.InvariantCulture)} {SafeName(title)}";

    /// <summary>Dateiname ohne Pfadtrenner und unzulässige Zeichen, höchstens 100 Zeichen.</summary>
    internal static string SafeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars().Concat(['/', '\\', ':', '*', '?', '"', '<', '>', '|']).ToHashSet();
        var safe = new string([.. name.Select(c => invalid.Contains(c) || char.IsControl(c) ? '_' : c)]).Trim().Trim('.');
        return safe.Length == 0 ? "_" : safe.Length > 100 ? safe[..100] : safe;
    }

    private static string Unique(string folder, string name)
    {
        var extension = Path.GetExtension(name);
        var stem = extension.Length is > 0 and <= 6 ? name[..^extension.Length] : name;
        extension = extension.Length is > 0 and <= 6 ? extension : string.Empty;
        var candidate = name;
        for (var n = 2; File.Exists(Path.Combine(folder, candidate)) || File.Exists(Path.Combine(folder, $"{candidate}.txt")) || Directory.Exists(Path.Combine(folder, candidate)); n++)
        {
            candidate = $"{stem} ({n.ToString(CultureInfo.InvariantCulture)}){extension}";
        }

        return candidate;
    }

    [GeneratedRegex("<script type=\"application/json\" id=\"mylife-package\">([\\s\\S]*?)</script>")]
    private static partial Regex PackageBlock();

    private sealed record ExportModel(
        string Format, int Version, DateTimeOffset CreatedAt, string Email, string DisplayName, ExportKdfModel Kdf, byte[] WrappedVmk, byte[]? WrappedVmkByRecovery,
        List<ExportAreaModel> Areas, List<ExportObjectModel> Objects, List<ExportDocumentModel> Documents);

    private sealed record ExportKdfModel(string Algorithm, byte[] Salt, int MemoryKiB, int Iterations, int Parallelism);

    private sealed record ExportAreaModel(int Area, byte[] WrappedKey);

    private sealed record ExportObjectModel(Guid Id, int Area, string Type, ObjectStatus Status, byte[] Payload, byte[]? WrappedKey);

    private sealed record ExportDocumentModel(Guid Id, Guid ObjectId, byte[] WrappedFileKey, byte[] Metadata, byte[]? Content);
}
