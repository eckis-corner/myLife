using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using MyLife.Client.Services;
using MyLife.Crypto;
using MyLife.Shared.Vault;

namespace MyLife.Client.Export;

/// <summary>
/// Offenes, verschlüsseltes Exportformat (Architektur 7, docs/format.md). Enthält das Chiffrat genau so, wie es auf
/// dem Server liegt, dazu die verpackten Schlüssel. Öffnen lässt es sich mit dem Master-Passwort zum Zeitpunkt
/// des Exports oder mit dem Recovery-Key, auch ohne myLife-Server.
/// </summary>
public sealed class ExportFile
{
    public const string FormatName = "mylife-export";
    public const int CurrentVersion = 1;

    public string Format { get; set; } = FormatName;

    public int Version { get; set; } = CurrentVersion;

    /// <summary>Version des Verschlüsselungsformats (docs/kryptoformat.md).</summary>
    public int CryptoFormat { get; set; } = MyLife.Crypto.CryptoFormat.CurrentVersion;

    public DateTimeOffset CreatedAt { get; set; }

    public string Email { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public ExportKdf Kdf { get; set; } = new();

    /// <summary>Tresorschlüssel, verpackt mit dem KEK aus dem Passwort (Kontext <c>vmk</c>).</summary>
    public byte[] WrappedVmk { get; set; } = [];

    /// <summary>Tresorschlüssel, verpackt mit dem Recovery-Key (Kontext <c>vmk</c>).</summary>
    public byte[]? WrappedVmkByRecovery { get; set; }

    public List<ExportArea> Areas { get; set; } = [];

    public List<ExportObject> Objects { get; set; } = [];

    public List<ExportDocument> Documents { get; set; } = [];

    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}

public sealed class ExportKdf
{
    public string Algorithm { get; set; } = "argon2id";

    public byte[] Salt { get; set; } = [];

    public int MemoryKiB { get; set; }

    public int Iterations { get; set; }

    public int Parallelism { get; set; }
}

/// <summary>Bereichsschlüssel, verpackt mit dem Tresorschlüssel (Kontext <c>area:{n}</c>).</summary>
public sealed record ExportArea(int Area, byte[] WrappedKey);

/// <summary>Objekt; <see cref="Payload"/> ist JSON, verschlüsselt mit dem Bereichsschlüssel (Kontext <c>object:{Typ}:{Id}</c>).</summary>
public sealed record ExportObject(
    Guid Id, int Area, string Type, int SchemaVersion, ObjectStatus Status, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, byte[] Payload);

/// <summary>Dokument; Inhalt in Blöcken mit dem Dateischlüssel verschlüsselt, fehlt bei einem Export ohne Dokumente.</summary>
public sealed record ExportDocument(Guid Id, Guid ObjectId, byte[] WrappedFileKey, byte[] Metadata, byte[]? Content);

public sealed record ImportResult(int Objects, int Skipped, int Documents, int Unreadable);

public sealed class VaultExport(VaultClient api, VaultService vault, SessionService session, IMyLifeCrypto crypto)
{
    /// <summary>Exportiert den eigenen Tresor ohne Papierkorb. Entschlüsselt dafür nichts.</summary>
    public async Task<byte[]> ExportAsync(bool includeDocuments, DateTimeOffset now, IProgress<double>? progress = null)
    {
        var account = session.Session ?? throw new VaultException(ClientVaultErrors.Locked);
        var file = new ExportFile
        {
            CreatedAt = now,
            Email = account.Email,
            DisplayName = account.DisplayName,
            Kdf = new ExportKdf
            {
                Salt = account.KdfSalt, MemoryKiB = account.KdfMemoryKiB, Iterations = account.KdfIterations, Parallelism = account.KdfParallelism,
            },
            WrappedVmk = account.WrappedVmk,
            WrappedVmkByRecovery = account.WrappedVmkByRecovery,
            Areas = [.. (await api.GetAreasAsync()).Where(a => a.WrappedKey is not null).Select(a => new ExportArea(a.Area, a.WrappedKey!))],
        };

        var objects = (await api.GetObjectsAsync()).Where(o => !o.IsDeleted).ToList();
        file.Objects = [.. objects.Select(o => new ExportObject(o.Id, o.Area, o.TypeKey, o.SchemaVersion, o.Status, o.CreatedAt, o.UpdatedAt, o.Payload))];
        var live = objects.Select(o => o.Id).ToHashSet();
        var documents = (await api.GetDocumentsAsync()).Where(d => !d.IsDeleted && live.Contains(d.ObjectId)).ToList();
        for (var i = 0; i < documents.Count; i++)
        {
            var d = documents[i];
            var content = includeDocuments ? await api.DownloadContentAsync(d.Id) : null;
            file.Documents.Add(new ExportDocument(d.Id, d.ObjectId, d.WrappedFileKey, d.Metadata, content));
            progress?.Report((i + 1d) / documents.Count);
        }

        return JsonSerializer.SerializeToUtf8Bytes(file, ExportFile.JsonOptions);
    }

    public static ExportFile Parse(byte[] json)
    {
        try
        {
            var file = JsonSerializer.Deserialize<ExportFile>(json, ExportFile.JsonOptions);
            return file is { Format: ExportFile.FormatName, Version: ExportFile.CurrentVersion } && file.WrappedVmk.Length > 0
                ? file
                : throw new VaultException(ClientVaultErrors.ImportInvalid);
        }
        catch (JsonException exception)
        {
            throw new VaultException(ClientVaultErrors.ImportInvalid, exception);
        }
    }

    /// <summary>
    /// Übernimmt einen Export in den eigenen Tresor. <paramref name="secret"/> ist das Passwort zum Zeitpunkt des Exports
    /// oder die 24 Wörter. Objekte behalten ihre ID; was schon im Tresor ist, wird übersprungen. Objekte werden mit den
    /// eigenen Bereichsschlüsseln neu verschlüsselt, Dateischlüssel nur umgepackt.
    /// </summary>
    public async Task<ImportResult> ImportAsync(ExportFile file, string secret, IProgress<double>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(file);
        await vault.EnsureLoadedAsync();
        var handles = new List<CryptoKeyHandle>();
        try
        {
            var vmk = await OpenVaultKeyAsync(file, secret, handles);
            var areaKeys = new Dictionary<int, CryptoKeyHandle>();
            foreach (var area in file.Areas.Where(a => ObjectCatalog.IsUsableArea(a.Area)))
            {
                try
                {
                    var key = await crypto.UnwrapKeyAsync(vmk, area.WrappedKey, KeyContexts.Area(area.Area));
                    handles.Add(key);
                    areaKeys[area.Area] = key;
                }
                catch (CryptographicException)
                {
                    // Bereich unlesbar: seine Objekte zählen unten als unlesbar.
                }
            }

            int imported = 0, skipped = 0, unreadable = 0, documents = 0, done = 0;
            var total = Math.Max(file.Objects.Count + file.Documents.Count, 1);
            foreach (var entry in file.Objects)
            {
                progress?.Report(++done / (double)total);
                if (vault.Find(entry.Id) is not null)
                {
                    skipped++;
                    continue;
                }

                var type = ObjectCatalog.Find(entry.Type);
                if (type is null || type.Area != entry.Area || !areaKeys.TryGetValue(entry.Area, out var areaKey))
                {
                    unreadable++;
                    continue;
                }

                try
                {
                    var payload = VaultPayload.FromUtf8(await crypto.DecryptAsync(areaKey, entry.Payload, KeyContexts.VaultObject(type.Key, entry.Id)));
                    await vault.CreateAsync(type.Key, payload, entry.Status, entry.Id);
                    imported++;
                }
                catch (Exception exception) when (exception is CryptographicException or JsonException)
                {
                    unreadable++;
                }
                catch (VaultException exception) when (exception.Message == VaultErrors.Conflict)
                {
                    skipped++;
                }
            }

            foreach (var entry in file.Documents)
            {
                progress?.Report(++done / (double)total);
                var owner = vault.Find(entry.ObjectId);
                if (entry.Content is null || owner is null || vault.Files.Any(f => f.Id == entry.Id))
                {
                    continue;
                }

                var source = file.Objects.FirstOrDefault(o => o.Id == entry.ObjectId);
                if (source is null || !areaKeys.TryGetValue(source.Area, out var oldAreaKey))
                {
                    unreadable++;
                    continue;
                }

                try
                {
                    var metadata = DocumentMetadata.FromUtf8(await crypto.DecryptAsync(oldAreaKey, entry.Metadata, KeyContexts.DocumentMetadata(entry.Id)));
                    await vault.ImportFileAsync(owner, entry.Id, oldAreaKey, entry.WrappedFileKey, metadata, entry.Content);
                    documents++;
                }
                catch (Exception exception) when (exception is CryptographicException or JsonException)
                {
                    unreadable++;
                }
                catch (VaultException exception) when (exception.Message == VaultErrors.Conflict)
                {
                    // Dokument-ID schon vergeben.
                }
            }

            return new ImportResult(imported, skipped, documents, unreadable);
        }
        finally
        {
            foreach (var handle in handles)
            {
                await crypto.ReleaseAsync(handle);
            }
        }
    }

    private async Task<CryptoKeyHandle> OpenVaultKeyAsync(ExportFile file, string secret, List<CryptoKeyHandle> handles)
    {
        try
        {
            if (Bip39.TryDecode(secret, out var recoveryKey))
            {
                try
                {
                    var recovery = await crypto.ImportRecoveryKeyAsync(recoveryKey);
                    handles.Add(recovery);
                    var vmk = await crypto.UnwrapKeyAsync(recovery, file.WrappedVmkByRecovery ?? [], KeyContexts.Vmk);
                    handles.Add(vmk);
                    return vmk;
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(recoveryKey);
                }
            }

            var parameters = new Argon2Parameters(file.Kdf.MemoryKiB, file.Kdf.Iterations, file.Kdf.Parallelism);
            var master = await crypto.DeriveMasterKeyAsync(secret, file.Kdf.Salt, parameters);
            handles.Add(master);
            var kek = await crypto.DeriveKeyEncryptionKeyAsync(master);
            handles.Add(kek);
            var key = await crypto.UnwrapKeyAsync(kek, file.WrappedVmk, KeyContexts.Vmk);
            handles.Add(key);
            return key;
        }
        catch (Exception exception) when (exception is CryptographicException or ArgumentException)
        {
            throw new VaultException(ClientVaultErrors.ImportWrongSecret, exception);
        }
    }
}
