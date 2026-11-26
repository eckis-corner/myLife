using System.Security.Cryptography;
using System.Text.Json;
using MyLife.Crypto;
using MyLife.Shared.Vault;

namespace MyLife.Client.Services;

/// <summary>Ein entschlüsseltes Objekt im Arbeitsspeicher des Browsers.</summary>
public sealed class VaultItem
{
    public required Guid Id { get; init; }

    public required int Area { get; init; }

    public required ObjectTypeDefinition Type { get; init; }

    public required VaultPayload Payload { get; set; }

    public ObjectStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public uint Version { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>Eigener Objektschlüssel, verpackt mit dem Bereichsschlüssel (Briefe); sonst null.</summary>
    public byte[]? WrappedKey { get; set; }

    public string Title => Payload.Title;
}

/// <summary>Ein Dokument mit entschlüsselten Metadaten. Der Inhalt wird erst beim Öffnen geladen.</summary>
public sealed class VaultFile
{
    public required Guid Id { get; init; }

    public required Guid ObjectId { get; init; }

    public required DocumentMetadata Metadata { get; init; }

    public required byte[] WrappedFileKey { get; init; }

    public long CipherSize { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}

/// <summary>Stand eines Bereichs für das Dashboard.</summary>
public sealed record AreaState(int Area, bool HasKey, DateTimeOffset? LastReviewedAt);

/// <summary>
/// Der entsperrte Tresor im Browser (Architektur 4.2): Bereichsschlüssel als Handles, Objekte und Dokument-Metadaten
/// entschlüsselt im Arbeitsspeicher. Zum Server gehen nur Chiffrat und verpackte Schlüssel. Beim Abmelden oder
/// Sperren wird alles verworfen.
/// </summary>
public sealed class VaultService : IDisposable
{
    private readonly VaultClient _api;
    private readonly IMyLifeCrypto _crypto;
    private readonly SessionService _session;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Dictionary<int, CryptoKeyHandle> _areaKeys = [];
    private readonly Dictionary<Guid, CryptoKeyHandle> _objectKeys = [];
    private readonly Dictionary<int, AreaDto> _areas = [];
    private List<VaultItem> _items = [];
    private List<VaultFile> _files = [];
    private bool _loaded;

    public VaultService(VaultClient api, IMyLifeCrypto crypto, SessionService session)
    {
        _api = api;
        _crypto = crypto;
        _session = session;
        _session.Changed += OnSessionChanged;
    }

    public event Action? Changed;

    public bool IsLoaded => _loaded;

    /// <summary>Objekte, deren Chiffrat sich nicht entschlüsseln ließ (sollte nie vorkommen; Hinweis im Dashboard).</summary>
    public int UnreadableCount { get; private set; }

    /// <summary>Alle Objekte, auch beendete und gelöschte.</summary>
    public IReadOnlyList<VaultItem> Items => _items;

    public IEnumerable<VaultItem> Current => _items.Where(i => !i.IsDeleted);

    public IReadOnlyList<VaultFile> Files => _files;

    public IEnumerable<AreaState> Areas => ObjectCatalog.Areas.Select(a =>
        _areas.TryGetValue(a.Number, out var dto) ? new AreaState(a.Number, true, dto.LastReviewedAt) : new AreaState(a.Number, false, null));

    public VaultItem? Find(Guid id) => _items.FirstOrDefault(i => i.Id == id);

    /// <summary>Objekttypen mit eigenem Schlüssel: Briefe lassen sich einzeln je Empfänger freigeben (Entscheidung 17).</summary>
    public static bool HasOwnKey(ObjectTypeDefinition type) => type?.Key == "letter";

    /// <summary>Verpackter Bereichsschlüssel, wie ihn der Server hält (für Umschläge).</summary>
    public byte[]? WrappedAreaKey(int area) => _areas.TryGetValue(area, out var dto) ? dto.WrappedKey : null;

    public string? TitleOf(Guid id) => Find(id)?.Title;

    public IEnumerable<VaultFile> FilesOf(Guid objectId) => _files.Where(f => f.ObjectId == objectId);

    /// <summary>Objekte, die auf dieses Objekt verweisen (Rückverweise im Datenblatt, Entscheidung 40).</summary>
    public IEnumerable<VaultItem> ReferencesTo(Guid id) =>
        Current.Where(i => i.Id != id && i.Payload.References(i.Type).Contains(id));

    /// <summary>Lädt und entschlüsselt den Tresor beim ersten Aufruf nach dem Entsperren.</summary>
    public async Task EnsureLoadedAsync()
    {
        if (_loaded)
        {
            return;
        }

        await _lock.WaitAsync();
        try
        {
            if (!_loaded)
            {
                await LoadCoreAsync();
            }
        }
        finally
        {
            _lock.Release();
        }

        Changed?.Invoke();
    }

    public async Task ReloadAsync()
    {
        await _lock.WaitAsync();
        try
        {
            await LoadCoreAsync();
        }
        finally
        {
            _lock.Release();
        }

        Changed?.Invoke();
    }

    public async Task<VaultItem> CreateAsync(string typeKey, VaultPayload payload, ObjectStatus status = ObjectStatus.Active, Guid? id = null)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var type = ObjectCatalog.Find(typeKey) ?? throw new VaultException(VaultErrors.Invalid);
        var objectId = id ?? Guid.NewGuid();
        payload.SchemaVersion = type.SchemaVersion;
        byte[]? wrappedKey = null;
        CryptoKeyHandle key;
        if (HasOwnKey(type))
        {
            var created = await _crypto.CreateKeyAsync([new KeyWrapping(await AreaKeyAsync(type.Area), KeyContexts.ObjectKey(objectId))]);
            wrappedKey = created.WrappedKeys[0];
            key = _objectKeys[objectId] = created.Handle;
        }
        else
        {
            key = await AreaKeyAsync(type.Area);
        }

        var cipher = await EncryptPayloadAsync(key, type, objectId, payload);
        var dto = await _api.CreateObjectAsync(new CreateObjectRequest(objectId, type.Area, type.Key, cipher, type.SchemaVersion, status, wrappedKey));
        var item = ToItem(dto, type, payload.Clone());
        _items.Add(item);
        Changed?.Invoke();
        return item;
    }

    /// <summary>Speichert einen neuen Stand. Hat ihn inzwischen jemand anderes geändert, kommt <see cref="VaultErrors.Conflict"/>.</summary>
    public async Task<VaultItem> UpdateAsync(VaultItem item, VaultPayload payload, ObjectStatus status)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(payload);
        payload.SchemaVersion = item.Type.SchemaVersion;
        var renamedLocation = item.Type.Key == ObjectCatalog.LocationType && item.Payload.Title.Trim() != payload.Title.Trim();
        // Briefe aus der Zeit vor M2.3 bekommen beim nächsten Speichern ihren eigenen Schlüssel.
        byte[]? newKey = null;
        if (HasOwnKey(item.Type) && item.WrappedKey is null)
        {
            var created = await _crypto.CreateKeyAsync([new KeyWrapping(await AreaKeyAsync(item.Area), KeyContexts.ObjectKey(item.Id))]);
            newKey = created.WrappedKeys[0];
            _objectKeys[item.Id] = created.Handle;
        }

        var cipher = await EncryptPayloadAsync(newKey is null ? await ContentKeyAsync(item) : _objectKeys[item.Id], item.Type, item.Id, payload);
        var dto = await _api.UpdateObjectAsync(item.Id, new UpdateObjectRequest(cipher, item.Type.SchemaVersion, status, item.Version, newKey ?? item.WrappedKey));
        item.WrappedKey = dto.WrappedKey;
        item.Payload = payload.Clone();
        item.Status = dto.Status;
        item.UpdatedAt = dto.UpdatedAt;
        item.Version = dto.Version;
        if (renamedLocation)
        {
            await PropagateLocationNameAsync(item);
        }

        Changed?.Invoke();
        return item;
    }

    /// <summary>
    /// Ein Ablageort wurde umbenannt (Entscheidung 143): Alle Einträge, die ihn gewählt haben, bekommen den neuen
    /// Namen, denn dort steht er für Vertrauenspersonen ohne Freigabe des Bereichs „Ablageorte“. Schlägt ein Eintrag
    /// fehl (etwa gleichzeitig geändert), behält er den alten Namen; der Verweis bleibt richtig.
    /// </summary>
    private async Task PropagateLocationNameAsync(VaultItem location)
    {
        foreach (var other in _items.Where(i => !i.IsDeleted && i.Id != location.Id).ToList())
        {
            var payload = other.Payload.Clone();
            if (!payload.RenameLocation(location.Id, location.Title))
            {
                continue;
            }

            try
            {
                await UpdateAsync(other, payload, other.Status);
            }
            catch (VaultException)
            {
                // Bleibt beim alten Namen; beim nächsten Umbenennen oder Speichern wird er nachgezogen.
            }
        }
    }

    public async Task DeleteAsync(Guid id) => Apply(await _api.DeleteObjectAsync(id));

    public async Task RestoreAsync(Guid id) => Apply(await _api.RestoreObjectAsync(id));

    public async Task PurgeAsync(Guid id)
    {
        await _api.PurgeObjectAsync(id);
        _items.RemoveAll(i => i.Id == id);
        _files.RemoveAll(f => f.ObjectId == id);
        Changed?.Invoke();
    }

    public async Task MarkAreaReviewedAsync(int area)
    {
        await AreaKeyAsync(area);
        _areas[area] = await _api.MarkAreaReviewedAsync(area);
        Changed?.Invoke();
    }

    /// <summary>
    /// Verschlüsselt eine Datei in Blöcken von 1 MiB (kryptoformat.md 4) mit einem neuen Dateischlüssel und lädt
    /// sie hoch. Der Server bekommt nur Chiffrat, den verpackten Schlüssel und die verschlüsselten Metadaten.
    /// </summary>
    public async Task<VaultFile> UploadAsync(Guid objectId, Stream content, DocumentMetadata metadata, IProgress<double>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(metadata);
        var item = Find(objectId) ?? throw new VaultException(VaultErrors.NotFound);
        if (metadata.Size > VaultLimits.MaxDocumentBytes)
        {
            throw new VaultException(ClientVaultErrors.FileTooLarge);
        }

        var areaKey = await ContentKeyAsync(item);
        var id = Guid.NewGuid();
        var fileKey = await _crypto.CreateKeyAsync([new KeyWrapping(areaKey, KeyContexts.FileKey(id))]);
        try
        {
            var cipher = await EncryptFileAsync(fileKey.Handle, id, content, metadata.Size, progress);
            var metaCipher = await _crypto.EncryptAsync(areaKey, metadata.ToUtf8(), KeyContexts.DocumentMetadata(id));
            await _api.CreateDocumentAsync(new CreateDocumentRequest(id, objectId, fileKey.WrappedKeys[0], metaCipher, cipher.Length));
            var dto = await _api.UploadContentAsync(id, cipher);
            var file = new VaultFile
            {
                Id = id, ObjectId = objectId, Metadata = metadata, WrappedFileKey = fileKey.WrappedKeys[0], CipherSize = dto.CipherSize, CreatedAt = dto.CreatedAt,
            };
            _files.Add(file);
            Changed?.Invoke();
            return file;
        }
        finally
        {
            await _crypto.ReleaseAsync(fileKey.Handle);
        }
    }

    /// <summary>
    /// Übernimmt ein Dokument aus einem Export: Der Dateischlüssel wird nur auf den eigenen Bereichsschlüssel umgepackt,
    /// das Chiffrat bleibt unverändert (der Kontext hängt an der Dokument-ID, die erhalten bleibt).
    /// </summary>
    internal async Task<VaultFile> ImportFileAsync(VaultItem owner, Guid id, CryptoKeyHandle oldAreaKey, byte[] oldWrappedFileKey, DocumentMetadata metadata, byte[] cipher)
    {
        var areaKey = await ContentKeyAsync(owner);
        var wrapped = await _crypto.RewrapKeyAsync(oldAreaKey, oldWrappedFileKey, KeyContexts.FileKey(id), areaKey, KeyContexts.FileKey(id));
        var metaCipher = await _crypto.EncryptAsync(areaKey, metadata.ToUtf8(), KeyContexts.DocumentMetadata(id));
        await _api.CreateDocumentAsync(new CreateDocumentRequest(id, owner.Id, wrapped, metaCipher, cipher.Length));
        var dto = await _api.UploadContentAsync(id, cipher);
        var file = new VaultFile { Id = id, ObjectId = owner.Id, Metadata = metadata, WrappedFileKey = wrapped, CipherSize = dto.CipherSize, CreatedAt = dto.CreatedAt };
        _files.Add(file);
        Changed?.Invoke();
        return file;
    }

    /// <summary>Lädt ein Dokument und entschlüsselt es Block für Block.</summary>
    public async Task<byte[]> DownloadAsync(VaultFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var item = Find(file.ObjectId) ?? throw new VaultException(VaultErrors.NotFound);
        var cipher = await _api.DownloadContentAsync(file.Id);
        return await DecryptFileAsync(await ContentKeyAsync(item), file.Id, file.WrappedFileKey, cipher);
    }

    public async Task DeleteFileAsync(VaultFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var dto = await _api.DeleteDocumentAsync(file.Id);
        file.IsDeleted = dto.IsDeleted;
        file.DeletedAt = dto.DeletedAt;
        Changed?.Invoke();
    }

    public async Task RestoreFileAsync(VaultFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var dto = await _api.RestoreDocumentAsync(file.Id);
        file.IsDeleted = dto.IsDeleted;
        file.DeletedAt = dto.DeletedAt;
        Changed?.Invoke();
    }

    public async Task PurgeFileAsync(VaultFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        await _api.PurgeDocumentAsync(file.Id);
        _files.Remove(file);
        Changed?.Invoke();
    }

    /// <summary>Bereichsschlüssel als Handle; legt ihn beim ersten Gebrauch an (verpackt mit dem Tresorschlüssel).</summary>
    internal async Task<CryptoKeyHandle> AreaKeyAsync(int area)
    {
        if (_areaKeys.TryGetValue(area, out var handle))
        {
            return handle;
        }

        var vmk = _session.VaultKey ?? throw new VaultException(ClientVaultErrors.Locked);
        if (!_areas.TryGetValue(area, out var dto))
        {
            var created = await _crypto.CreateKeyAsync([new KeyWrapping(vmk, KeyContexts.Area(area))]);
            try
            {
                dto = await _api.CreateAreaKeyAsync(area, created.WrappedKeys[0]);
                _areas[area] = dto;
                return _areaKeys[area] = created.Handle;
            }
            catch (VaultException exception) when (exception.Message == VaultErrors.AreaKeyExists)
            {
                // Ein anderer Tab war schneller; dessen Schlüssel gilt.
                await _crypto.ReleaseAsync(created.Handle);
                dto = (await _api.GetAreasAsync()).Single(a => a.Area == area);
                _areas[area] = dto;
            }
        }

        return _areaKeys[area] = await _crypto.UnwrapKeyAsync(vmk, dto.WrappedKey!, KeyContexts.Area(area));
    }

    /// <summary>
    /// Schlüssel für Inhalt und Dokumente eines Objekts: der eigene Objektschlüssel (Briefe) oder der Bereichsschlüssel.
    /// </summary>
    internal async Task<CryptoKeyHandle> ContentKeyAsync(VaultItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.WrappedKey is null)
        {
            return await AreaKeyAsync(item.Area);
        }

        if (!_objectKeys.TryGetValue(item.Id, out var handle))
        {
            handle = _objectKeys[item.Id] = await _crypto.UnwrapKeyAsync(await AreaKeyAsync(item.Area), item.WrappedKey, KeyContexts.ObjectKey(item.Id));
        }

        return handle;
    }

    /// <summary>
    /// Neuer Bereichsschlüssel (46b, etwa wenn „Sofort teilen“ endet): alle Objekte neu verschlüsseln (Briefe: nur
    /// ihren Schlüssel neu verpacken), Dateischlüssel umpacken, Metadaten neu verschlüsseln; die Umschläge versiegelt
    /// <paramref name="resealGrants"/> mit dem neuen verpackten Schlüssel. Der Server übernimmt alles in einem Schritt.
    /// </summary>
    public async Task RotateAreaAsync(int area, Func<byte[], Task<IReadOnlyList<MyLife.Shared.Trust.RotatedGrant>>> resealGrants)
    {
        ArgumentNullException.ThrowIfNull(resealGrants);
        await EnsureLoadedAsync();
        var vmk = _session.VaultKey ?? throw new VaultException(ClientVaultErrors.Locked);
        if (!_areas.ContainsKey(area))
        {
            return;
        }

        var oldKey = await AreaKeyAsync(area);
        var created = await _crypto.CreateKeyAsync([new KeyWrapping(vmk, KeyContexts.Area(area))]);
        var newKey = created.Handle;
        var committed = false;
        try
        {
            var objects = new List<MyLife.Shared.Trust.RotatedObject>();
            var items = _items.Where(i => i.Area == area).ToList();
            foreach (var item in items)
            {
                if (item.WrappedKey is null)
                {
                    objects.Add(new(item.Id, await EncryptPayloadAsync(newKey, item.Type, item.Id, item.Payload), null, item.Version));
                }
                else
                {
                    var objectKey = await ContentKeyAsync(item);
                    var rewrapped = await _crypto.RewrapKeyAsync(oldKey, item.WrappedKey, KeyContexts.ObjectKey(item.Id), newKey, KeyContexts.ObjectKey(item.Id));
                    objects.Add(new(item.Id, await EncryptPayloadAsync(objectKey, item.Type, item.Id, item.Payload), rewrapped, item.Version));
                }
            }

            var unkeyed = items.Where(i => i.WrappedKey is null).Select(i => i.Id).ToHashSet();
            var documents = new List<MyLife.Shared.Trust.RotatedDocument>();
            foreach (var file in _files.Where(f => unkeyed.Contains(f.ObjectId)))
            {
                var wrapped = await _crypto.RewrapKeyAsync(oldKey, file.WrappedFileKey, KeyContexts.FileKey(file.Id), newKey, KeyContexts.FileKey(file.Id));
                documents.Add(new(file.Id, wrapped, await _crypto.EncryptAsync(newKey, file.Metadata.ToUtf8(), KeyContexts.DocumentMetadata(file.Id))));
            }

            var grants = await resealGrants(created.WrappedKeys[0]);
            await _api.RotateAreaAsync(area, new MyLife.Shared.Trust.RotateAreaRequest(created.WrappedKeys[0], objects, documents, grants));

            committed = true;
        }
        finally
        {
            await _crypto.ReleaseAsync(newKey);
        }

        // Übernommen: Der Tresor wird mit dem neuen Bereichsschlüssel neu geladen.
        if (committed)
        {
            await ReloadAsync();
        }
    }

    /// <summary>
    /// Neuer eigener Schlüssel für ein Objekt (Brief, M2.6): Inhalt neu verschlüsseln, Dateischlüssel und Metadaten
    /// seiner Dokumente umpacken; die Umschläge versiegelt <paramref name="resealGrants"/> mit dem neuen verpackten
    /// Schlüssel. Der Server übernimmt alles in einem Schritt.
    /// </summary>
    public async Task RekeyObjectAsync(
        Guid objectId, Func<CryptoKeyHandle, byte[], Task<IReadOnlyList<MyLife.Shared.Trust.RotatedGrant>>> resealGrants)
    {
        ArgumentNullException.ThrowIfNull(resealGrants);
        await EnsureLoadedAsync();
        var item = Find(objectId);
        if (item?.WrappedKey is null)
        {
            return;
        }

        var areaKey = await AreaKeyAsync(item.Area);
        var oldKey = await ContentKeyAsync(item);
        var created = await _crypto.CreateKeyAsync([new KeyWrapping(areaKey, KeyContexts.ObjectKey(item.Id))]);
        var committed = false;
        try
        {
            var payload = await EncryptPayloadAsync(created.Handle, item.Type, item.Id, item.Payload);
            var documents = new List<MyLife.Shared.Trust.RotatedDocument>();
            foreach (var file in FilesOf(item.Id))
            {
                var wrapped = await _crypto.RewrapKeyAsync(oldKey, file.WrappedFileKey, KeyContexts.FileKey(file.Id), created.Handle, KeyContexts.FileKey(file.Id));
                documents.Add(new(file.Id, wrapped, await _crypto.EncryptAsync(created.Handle, file.Metadata.ToUtf8(), KeyContexts.DocumentMetadata(file.Id))));
            }

            var grants = await resealGrants(areaKey, created.WrappedKeys[0]);
            await _api.RekeyLetterAsync(item.Id, new MyLife.Shared.Trust.RekeyLetterRequest(created.WrappedKeys[0], payload, item.Version, documents, grants));
            committed = true;
        }
        finally
        {
            await _crypto.ReleaseAsync(created.Handle);
        }

        if (committed)
        {
            await ReloadAsync();
        }
    }

    internal async Task<byte[]> EncryptFileAsync(CryptoKeyHandle fileKey, Guid documentId, Stream content, long size, IProgress<double>? progress)
    {
        var chunks = VaultLimits.ChunkCount(size);
        using var output = new MemoryStream((int)VaultLimits.CipherSize(size));
        var buffer = new byte[VaultLimits.ChunkSize];
        long done = 0;
        for (var index = 0; index < chunks; index++)
        {
            var length = (int)Math.Min(VaultLimits.ChunkSize, size - done);
            await content.ReadExactlyAsync(buffer.AsMemory(0, length));
            var block = await _crypto.EncryptChunkAsync(fileKey, index, index == chunks - 1, buffer[..length], KeyContexts.Document(documentId));
            await output.WriteAsync(block);
            done += length;
            progress?.Report(size == 0 ? 1 : (double)done / size);
        }

        return output.ToArray();
    }

    internal async Task<byte[]> DecryptFileAsync(CryptoKeyHandle areaKey, Guid documentId, byte[] wrappedFileKey, byte[] cipher)
    {
        var fileKey = await _crypto.UnwrapKeyAsync(areaKey, wrappedFileKey, KeyContexts.FileKey(documentId));
        try
        {
            using var output = new MemoryStream(Math.Max(cipher.Length - VaultLimits.ChunkOverhead, 0));
            const int fullBlock = VaultLimits.ChunkSize + VaultLimits.ChunkOverhead;
            var offset = 0;
            var index = 0;
            do
            {
                var length = Math.Min(fullBlock, cipher.Length - offset);
                var isLast = offset + length == cipher.Length;
                var plain = await _crypto.DecryptChunkAsync(fileKey, index, isLast, cipher[offset..(offset + length)], KeyContexts.Document(documentId));
                await output.WriteAsync(plain);
                offset += length;
                index++;
            }
            while (offset < cipher.Length);

            return output.ToArray();
        }
        catch (CryptographicException exception)
        {
            throw new VaultException(ClientVaultErrors.Decrypt, exception);
        }
        finally
        {
            await _crypto.ReleaseAsync(fileKey);
        }
    }

    public void Dispose()
    {
        _session.Changed -= OnSessionChanged;
        _lock.Dispose();
    }

    private async Task LoadCoreAsync()
    {
        if (_session.VaultKey is null)
        {
            throw new VaultException(ClientVaultErrors.Locked);
        }

        await ReleaseAreaKeysAsync();
        _areas.Clear();
        foreach (var area in await _api.GetAreasAsync())
        {
            _areas[area.Area] = area;
        }

        var items = new List<VaultItem>();
        var unreadable = 0;
        foreach (var dto in await _api.GetObjectsAsync())
        {
            var type = ObjectCatalog.Find(dto.TypeKey);
            if (type is null || !_areas.ContainsKey(dto.Area))
            {
                unreadable++;
                continue;
            }

            try
            {
                var key = dto.WrappedKey is null
                    ? await AreaKeyAsync(dto.Area)
                    : _objectKeys[dto.Id] = await _crypto.UnwrapKeyAsync(await AreaKeyAsync(dto.Area), dto.WrappedKey, KeyContexts.ObjectKey(dto.Id));
                var plain = await _crypto.DecryptAsync(key, dto.Payload, KeyContexts.VaultObject(type.Key, dto.Id));
                items.Add(ToItem(dto, type, VaultPayload.FromUtf8(plain)));
            }
            catch (Exception exception) when (exception is CryptographicException or JsonException)
            {
                unreadable++;
            }
        }

        var files = new List<VaultFile>();
        var byId = items.ToDictionary(i => i.Id);
        foreach (var dto in await _api.GetDocumentsAsync())
        {
            if (!byId.TryGetValue(dto.ObjectId, out var owner))
            {
                continue;
            }

            try
            {
                var plain = await _crypto.DecryptAsync(await ContentKeyAsync(owner), dto.Metadata, KeyContexts.DocumentMetadata(dto.Id));
                files.Add(new VaultFile
                {
                    Id = dto.Id,
                    ObjectId = dto.ObjectId,
                    Metadata = DocumentMetadata.FromUtf8(plain),
                    WrappedFileKey = dto.WrappedFileKey,
                    CipherSize = dto.CipherSize,
                    CreatedAt = dto.CreatedAt,
                    IsDeleted = dto.IsDeleted,
                    DeletedAt = dto.DeletedAt,
                });
            }
            catch (Exception exception) when (exception is CryptographicException or JsonException)
            {
                unreadable++;
            }
        }

        _items = items;
        _files = files;
        UnreadableCount = unreadable;
        _loaded = true;
    }

    private async Task<byte[]> EncryptPayloadAsync(CryptoKeyHandle key, ObjectTypeDefinition type, Guid id, VaultPayload payload)
    {
        var bytes = payload.ToUtf8();
        if (bytes.Length > VaultLimits.MaxPayloadBytes)
        {
            throw new VaultException(VaultErrors.TooLarge);
        }

        return await _crypto.EncryptAsync(key, bytes, KeyContexts.VaultObject(type.Key, id));
    }

    private void Apply(VaultObjectDto dto)
    {
        if (Find(dto.Id) is { } item)
        {
            item.IsDeleted = dto.IsDeleted;
            item.DeletedAt = dto.DeletedAt;
            item.Status = dto.Status;
            item.Version = dto.Version;
            item.UpdatedAt = dto.UpdatedAt;
        }

        Changed?.Invoke();
    }

    private static VaultItem ToItem(VaultObjectDto dto, ObjectTypeDefinition type, VaultPayload payload) => new()
    {
        Id = dto.Id,
        Area = dto.Area,
        Type = type,
        Payload = payload,
        Status = dto.Status,
        CreatedAt = dto.CreatedAt,
        UpdatedAt = dto.UpdatedAt,
        Version = dto.Version,
        IsDeleted = dto.IsDeleted,
        DeletedAt = dto.DeletedAt,
        WrappedKey = dto.WrappedKey,
    };

    private async Task ReleaseAreaKeysAsync()
    {
        foreach (var handle in _areaKeys.Values.Concat(_objectKeys.Values))
        {
            await _crypto.ReleaseAsync(handle);
        }

        _areaKeys.Clear();
        _objectKeys.Clear();
    }

    private void OnSessionChanged()
    {
        if (_session.State == SessionState.Unlocked)
        {
            return;
        }

        // Abgemeldet oder gesperrt: Die Handles hat SessionService bereits verworfen; hier nur die Klartexte.
        _areaKeys.Clear();
        _objectKeys.Clear();
        _areas.Clear();
        _items = [];
        _files = [];
        UnreadableCount = 0;
        _loaded = false;
        Changed?.Invoke();
    }
}
