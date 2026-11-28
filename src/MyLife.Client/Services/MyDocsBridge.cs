using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MyLife.Crypto;
using MyLife.Shared.Vault;

namespace MyLife.Client.Services;

// Brückenformat Version 1 (myDocs, docs/brueckenformat.md): eigene Klassen für dasselbe JSON, damit myLife nicht von
// myDocs abhängt. Unbekannte Felder werden übergangen; das Format wächst nur durch Ergänzen.

public sealed record MyDocsExport(string Format, int Version, DateTimeOffset GeneratedAt, string Fingerprint, MyDocsInstance Instance, MyDocsPerson Person,
    IReadOnlyList<MyDocsObject> Objects)
{
    public const string FormatName = "mydocs-bridge";

    public const int SupportedVersion = 1;
}

public sealed record MyDocsInstance(string Url);

public sealed record MyDocsPerson(Guid Id, string Name);

public sealed record MyDocsObject(Guid Id, string Type, int SchemaVersion, string Title, Dictionary<string, string> Fields, Dictionary<string, Guid> Locations,
    DateOnly? ExpiresOn, string? LifecycleState, DateOnly? LifecycleDate, bool Ended, DateOnly? LastReviewedOn, DateOnly? OverdueSince, DateTimeOffset UpdatedAt,
    IReadOnlyList<MyDocsDocument> Documents);

public sealed record MyDocsDocument(Guid Id, string Hash, string Title, string FileName, string ContentType, long Size, DateOnly? Date);

/// <summary>Verbindung zu myDocs, verschlüsselt mit dem Tresorschlüssel auf dem Server abgelegt (167).</summary>
public sealed class MyDocsSettings
{
    public string ServerUrl { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;

    public DateTimeOffset? LastSyncedAt { get; set; }
}

public enum MyDocsChangeKind
{
    New,
    Changed,
    Removed,
}

/// <summary>Was mit einem Eintrag geschieht, der nicht mehr aus myDocs kommt (170).</summary>
public enum MyDocsRemoval
{
    /// <summary>Bleibt als eigener Eintrag, wieder bearbeitbar.</summary>
    Unlink,

    /// <summary>In den Papierkorb.</summary>
    Trash,
}

/// <summary>Eine Änderung aus myDocs für einen Eintrag, mit allem, was die Übernahme braucht.</summary>
public sealed class MyDocsChange
{
    public required MyDocsChangeKind Kind { get; init; }

    public required ObjectTypeDefinition Type { get; init; }

    public required string Title { get; init; }

    /// <summary>Quelle in myDocs; fehlt bei <see cref="MyDocsChangeKind.Removed"/>.</summary>
    public MyDocsObject? Source { get; init; }

    /// <summary>Eintrag in myLife; fehlt bei <see cref="MyDocsChangeKind.New"/>.</summary>
    public VaultItem? Item { get; init; }

    /// <summary>ID in myLife: vorhanden oder für einen neuen Eintrag schon vergeben (für Verweise).</summary>
    public Guid TargetId { get; init; }

    /// <summary>Geänderte Angaben: <c>title</c>, Feldschlüssel, <c>expiresOn</c>, <c>status</c>, <c>reviewedOn</c>, <c>overdueSince</c>, <c>lifecycle</c>.</summary>
    public IReadOnlyList<string> Fields { get; init; } = [];

    public IReadOnlyList<MyDocsDocument> NewDocuments { get; init; } = [];

    public IReadOnlyList<(MyDocsDocument Source, VaultFile Copy)> ChangedDocuments { get; init; } = [];

    public IReadOnlyList<VaultFile> RemovedDocuments { get; init; } = [];

    /// <summary>Größer als 25 MB: kommt nicht mit (58).</summary>
    public IReadOnlyList<MyDocsDocument> SkippedDocuments { get; init; } = [];

    public MyDocsRemoval Removal { get; set; } = MyDocsRemoval.Unlink;
}

/// <summary>Stand aus myDocs mit den Änderungen gegenüber dem Tresor.</summary>
public sealed record MyDocsPlan(MyDocsExport Export, IReadOnlyList<MyDocsChange> Changes, IReadOnlyDictionary<Guid, Guid> Ids)
{
    public bool IsEmpty => Changes.Count == 0;
}

/// <summary>Fehlercodes; Texte unter <c>MyDocsError_{Code}</c>.</summary>
public static class MyDocsErrors
{
    public const string Unreachable = "unreachable";
    public const string Token = "token";
    public const string Format = "format";
    public const string Url = "url";
    public const string Failed = "failed";
}

public sealed class MyDocsException(string code, Exception? inner = null) : Exception(code, inner);

/// <summary>
/// Brücke zu myDocs (56–61, 166–171): myLife zieht. Der Browser holt mit dem lesenden Token den Stand im Klartext über
/// TLS, vergleicht ihn mit dem entschlüsselten Tresor und übernimmt nach Bestätigung: verschlüsselt wie jede andere
/// Änderung. Der myLife-Server sieht davon nur Chiffrat. Einbahnstraße: Nichts geht von hier nach myDocs außer der
/// Quittung mit dem Fingerabdruck des übernommenen Stands.
/// </summary>
public sealed class MyDocsBridge : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly VaultClient _api;
    private readonly VaultService _vault;
    private readonly IMyLifeCrypto _crypto;
    private readonly SessionService _session;
    private readonly Func<HttpMessageHandler> _handlers;
    private bool _loaded;

    public MyDocsBridge(VaultClient api, VaultService vault, IMyLifeCrypto crypto, SessionService session, Func<HttpMessageHandler>? handlers = null)
    {
        _api = api;
        _vault = vault;
        _crypto = crypto;
        _session = session;
        _handlers = handlers ?? (() => new HttpClientHandler());
        _session.Changed += OnSessionChanged;
    }

    public event Action? Changed;

    public MyDocsSettings? Settings { get; private set; }

    /// <summary>Letzte Prüfung: Änderungen aus myDocs, die auf Bestätigung warten.</summary>
    public MyDocsPlan? Pending { get; private set; }

    /// <summary>Fehler der letzten Prüfung (<see cref="MyDocsErrors"/>), sonst null.</summary>
    public string? Error { get; private set; }

    public bool IsConnected => Settings is not null;

    /// <summary>Lädt und entschlüsselt die Verbindung beim ersten Aufruf nach dem Entsperren.</summary>
    public async Task<MyDocsSettings?> LoadAsync()
    {
        if (_loaded)
        {
            return Settings;
        }

        var vmk = _session.VaultKey ?? throw new VaultException(ClientVaultErrors.Locked);
        var cipher = await _api.GetMyDocsAsync();
        Settings = cipher is null ? null : JsonSerializer.Deserialize<MyDocsSettings>(await _crypto.DecryptAsync(vmk, cipher, KeyContexts.MyDocsBridge), Json);
        _loaded = true;
        return Settings;
    }

    /// <summary>Speichert Adresse und Token (167); geprüft wird beim nächsten Abgleich.</summary>
    public async Task ConnectAsync(string serverUrl, string token)
    {
        if (NormalizeUrl(serverUrl) is not { } url || string.IsNullOrWhiteSpace(token) || token.Trim().Length > 100)
        {
            throw new MyDocsException(MyDocsErrors.Url);
        }

        await SaveAsync(new MyDocsSettings { ServerUrl = url, Token = token.Trim() });
        Pending = null;
        Error = null;
        Changed?.Invoke();
    }

    public async Task DisconnectAsync()
    {
        await _api.ClearMyDocsAsync();
        Settings = null;
        Pending = null;
        Error = null;
        Changed?.Invoke();
    }

    /// <summary>Adresse mit Schema und abschließendem Schrägstrich; null, wenn es keine http(s)-Adresse ist.</summary>
    public static string? NormalizeUrl(string? value) =>
        Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? uri.GetLeftPart(UriPartial.Path).TrimEnd('/') + "/"
            : null;

    /// <summary>Holt den Stand aus myDocs und vergleicht ihn mit dem Tresor. Ändert nichts.</summary>
    public async Task<MyDocsPlan?> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (await LoadAsync() is not { } settings)
        {
            return null;
        }

        try
        {
            await _vault.EnsureLoadedAsync();
            var export = await FetchAsync(settings, cancellationToken);
            Pending = Plan(export, _vault.Items, _vault.Files, DateTimeOffset.UtcNow);
            Error = null;
            return Pending;
        }
        catch (MyDocsException exception)
        {
            Pending = null;
            Error = exception.Message;
            return null;
        }
        finally
        {
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Übernimmt den Stand: Einträge anlegen und ändern, Kopien der Dokumente ersetzen, nicht mehr gelieferte Einträge
    /// lösen oder in den Papierkorb legen; danach die Quittung an myDocs. Bricht etwas ab, gibt es keine Quittung, und
    /// der nächste Abgleich zeigt, was noch fehlt.
    /// </summary>
    public async Task ApplyAsync(MyDocsPlan plan, IProgress<double>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var settings = Settings ?? throw new MyDocsException(MyDocsErrors.Token);
        var now = DateTimeOffset.UtcNow;
        var done = 0;
        foreach (var change in plan.Changes)
        {
            switch (change.Kind)
            {
                case MyDocsChangeKind.New:
                {
                    var (payload, status) = Merge(change.Source!, null, plan.Ids, change.Type, now);
                    var created = await _vault.CreateAsync(change.Type.Key, payload, status, change.TargetId);
                    await CopyDocumentsAsync(settings, created.Id, change);
                    break;
                }

                case MyDocsChangeKind.Changed:
                {
                    var item = _vault.Find(change.Item!.Id) ?? throw new VaultException(VaultErrors.NotFound);
                    var (payload, status) = Merge(change.Source!, item.Payload, plan.Ids, change.Type, now);
                    if (change.Fields.Count > 0)
                    {
                        await _vault.UpdateAsync(item, payload, status);
                    }

                    await CopyDocumentsAsync(settings, item.Id, change);
                    break;
                }

                case MyDocsChangeKind.Removed:
                {
                    var item = _vault.Find(change.Item!.Id) ?? throw new VaultException(VaultErrors.NotFound);
                    if (change.Removal == MyDocsRemoval.Trash)
                    {
                        await _vault.DeleteAsync(item.Id);
                    }
                    else
                    {
                        var payload = item.Payload.Clone();
                        payload.MyDocs = null;
                        await _vault.UpdateAsync(item, payload, item.Status);
                    }

                    break;
                }
            }

            progress?.Report((double)++done / plan.Changes.Count);
        }

        await ConfirmAsync(settings, plan.Export.Fingerprint);
        await _api.ReportAsync(AuditActions.MyDocsSynced, plan.Changes.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        settings.LastSyncedAt = now;
        await SaveAsync(settings);
        Pending = plan with { Changes = [] };
        Changed?.Invoke();
    }

    /// <summary>
    /// Vergleich (pure Funktion, für Tests): Verknüpft wird über <see cref="VaultPayload.MyDocs"/>. Einträge im Papierkorb
    /// bleiben dort; Typen, die myLife nicht kennt, werden übergangen.
    /// </summary>
    public static MyDocsPlan Plan(MyDocsExport export, IReadOnlyList<VaultItem> items, IReadOnlyList<VaultFile> files, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(export);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(files);
        if (export.Format != MyDocsExport.FormatName || export.Version > MyDocsExport.SupportedVersion)
        {
            throw new MyDocsException(MyDocsErrors.Format);
        }

        var linked = items.Where(i => i.Payload.MyDocs is not null)
            .GroupBy(i => i.Payload.MyDocs!.Id)
            .ToDictionary(g => g.Key, g => g.OrderBy(i => i.IsDeleted).First());
        var known = export.Objects.Where(o => ObjectCatalog.Find(o.Type) is not null).ToList();
        var ids = known.ToDictionary(o => o.Id, o => linked.TryGetValue(o.Id, out var item) ? item.Id : Guid.NewGuid());
        var changes = new List<MyDocsChange>();
        foreach (var source in known)
        {
            var type = ObjectCatalog.Find(source.Type)!;
            if (!linked.TryGetValue(source.Id, out var item))
            {
                changes.Add(new MyDocsChange
                {
                    Kind = MyDocsChangeKind.New, Type = type, Title = source.Title, Source = source, TargetId = ids[source.Id],
                    NewDocuments = [.. source.Documents.Where(d => d.Size <= VaultLimits.MaxDocumentBytes)],
                    SkippedDocuments = [.. source.Documents.Where(d => d.Size > VaultLimits.MaxDocumentBytes)],
                });
                continue;
            }

            if (item.IsDeleted || item.Type.Key != type.Key)
            {
                continue;
            }

            var (desired, status) = Merge(source, item.Payload, ids, type, now);
            var fields = Differences(item.Payload, item.Status, desired, status);
            var copies = files.Where(f => f.ObjectId == item.Id && !f.IsDeleted && f.Metadata.MyDocsId is not null).ToList();
            var fresh = source.Documents.Where(d => d.Size <= VaultLimits.MaxDocumentBytes).ToList();
            var added = fresh.Where(d => copies.All(c => c.Metadata.MyDocsId != d.Id)).ToList();
            var replaced = fresh.Select(d => (Source: d, Copy: copies.FirstOrDefault(c => c.Metadata.MyDocsId == d.Id)))
                .Where(p => p.Copy is not null && p.Copy.Metadata.MyDocsHash != p.Source.Hash).Select(p => (p.Source, p.Copy!)).ToList();
            var removed = copies.Where(c => fresh.All(d => d.Id != c.Metadata.MyDocsId)).ToList();
            if (fields.Count + added.Count + replaced.Count + removed.Count > 0)
            {
                changes.Add(new MyDocsChange
                {
                    Kind = MyDocsChangeKind.Changed, Type = type, Title = source.Title, Source = source, Item = item, TargetId = item.Id, Fields = fields,
                    NewDocuments = added, ChangedDocuments = replaced, RemovedDocuments = removed,
                    SkippedDocuments = [.. source.Documents.Where(d => d.Size > VaultLimits.MaxDocumentBytes)],
                });
            }
        }

        var delivered = known.Select(o => o.Id).ToHashSet();
        foreach (var (myDocsId, item) in linked.Where(l => !l.Value.IsDeleted && !delivered.Contains(l.Key)).OrderBy(l => l.Value.Title, StringComparer.CurrentCultureIgnoreCase))
        {
            changes.Add(new MyDocsChange { Kind = MyDocsChangeKind.Removed, Type = item.Type, Title = item.Title, Item = item, TargetId = item.Id });
        }

        return new MyDocsPlan(export, changes, ids);
    }

    /// <summary>
    /// Stand eines Eintrags nach dem Abgleich: Titel, Katalogfelder, Ablageorte, Ablaufdatum, „zuletzt geprüft“ und
    /// Status aus myDocs; Wunsch, Ansprechpartner, Verknüpfungen, Ablageort des Originals, Notizen, Zusatzfelder und
    /// Wiedervorlage bleiben aus myLife. Verweise auf myDocs-IDs werden auf die Einträge hier umgeschrieben.
    /// </summary>
    public static (VaultPayload Payload, ObjectStatus Status) Merge(MyDocsObject source, VaultPayload? existing, IReadOnlyDictionary<Guid, Guid> ids,
        ObjectTypeDefinition type, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(type);
        var payload = existing?.Clone() ?? new VaultPayload();
        payload.Title = source.Title.Trim();
        payload.Fields = [];
        foreach (var (key, value) in source.Fields.OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            if (type.Field(key) is { Type: FieldType.Reference })
            {
                if (Guid.TryParse(value, out var target) && ids.TryGetValue(target, out var mine))
                {
                    payload.Set(key, mine.ToString());
                }

                continue;
            }

            payload.Set(key, value);
        }

        var locations = new Dictionary<string, Guid>();
        if (payload.Locations.TryGetValue(VaultPayload.OriginalLocationKey, out var original))
        {
            locations[VaultPayload.OriginalLocationKey] = original;
        }

        foreach (var (key, target) in source.Locations)
        {
            if (key != VaultPayload.OriginalLocationKey && ids.TryGetValue(target, out var mine))
            {
                locations[key] = mine;
            }
        }

        payload.Locations = locations;
        payload.ExpiresOn = source.ExpiresOn;
        payload.ReviewedOn = source.LastReviewedOn;
        payload.MyDocs = new MyDocsLink
        {
            Id = source.Id, SyncedAt = now, LifecycleState = source.LifecycleState, LifecycleDate = source.LifecycleDate, OverdueSince = source.OverdueSince,
        };
        return (payload, source.Ended ? ObjectStatus.Ended : ObjectStatus.Active);
    }

    /// <summary>
    /// Schreibschutz (59, 168): Beim Speichern eines verknüpften Eintrags gelten die Angaben aus myDocs, egal was das
    /// Formular geschickt hat.
    /// </summary>
    public static void KeepManaged(VaultPayload original, VaultPayload draft)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(draft);
        if (original.MyDocs is null)
        {
            return;
        }

        draft.Title = original.Title;
        draft.Fields = new(original.Fields);
        var originalLocation = draft.Locations.TryGetValue(VaultPayload.OriginalLocationKey, out var keep) ? keep : (Guid?)null;
        draft.Locations = original.Locations.Where(l => l.Key != VaultPayload.OriginalLocationKey).ToDictionary(l => l.Key, l => l.Value);
        if (originalLocation is { } id)
        {
            draft.Locations[VaultPayload.OriginalLocationKey] = id;
        }

        draft.ExpiresOn = original.ExpiresOn;
        draft.ReviewedOn = original.ReviewedOn;
        draft.MyDocs = original.MyDocs;
    }

    public void Dispose() => _session.Changed -= OnSessionChanged;

    private static List<string> Differences(VaultPayload current, ObjectStatus currentStatus, VaultPayload desired, ObjectStatus desiredStatus)
    {
        var result = new List<string>();
        if (current.Title != desired.Title)
        {
            result.Add("title");
        }

        foreach (var key in current.Fields.Keys.Union(desired.Fields.Keys).Order(StringComparer.Ordinal))
        {
            if (current.Get(key) != desired.Get(key))
            {
                result.Add(key);
            }
        }

        if (!SameLocations(current.Locations, desired.Locations))
        {
            result.Add("locations");
        }

        if (current.ExpiresOn != desired.ExpiresOn)
        {
            result.Add("expiresOn");
        }

        if (current.ReviewedOn != desired.ReviewedOn)
        {
            result.Add("reviewedOn");
        }

        if (currentStatus != desiredStatus)
        {
            result.Add("status");
        }

        if (current.MyDocs?.LifecycleState != desired.MyDocs?.LifecycleState || current.MyDocs?.LifecycleDate != desired.MyDocs?.LifecycleDate)
        {
            result.Add("lifecycle");
        }

        if (current.MyDocs?.OverdueSince != desired.MyDocs?.OverdueSince)
        {
            result.Add("overdueSince");
        }

        return result;
    }

    private static bool SameLocations(Dictionary<string, Guid> a, Dictionary<string, Guid> b)
    {
        var left = a.Where(l => l.Key != VaultPayload.OriginalLocationKey).ToList();
        var right = b.Where(l => l.Key != VaultPayload.OriginalLocationKey).ToDictionary(l => l.Key, l => l.Value);
        return left.Count == right.Count && left.All(l => right.TryGetValue(l.Key, out var id) && id == l.Value);
    }

    private async Task CopyDocumentsAsync(MyDocsSettings settings, Guid objectId, MyDocsChange change)
    {
        foreach (var document in change.NewDocuments)
        {
            await CopyAsync(settings, objectId, document);
        }

        foreach (var (source, copy) in change.ChangedDocuments)
        {
            await CopyAsync(settings, objectId, source);
            await _vault.DeleteFileAsync(copy);
        }

        foreach (var copy in change.RemovedDocuments)
        {
            await _vault.DeleteFileAsync(copy);
        }
    }

    private async Task CopyAsync(MyDocsSettings settings, Guid objectId, MyDocsDocument document)
    {
        using var client = Client(settings);
        using var response = await SendAsync(() => client.GetAsync(new Uri($"api/mylife/documents/{document.Id}/content", UriKind.Relative)));
        var bytes = await response.Content.ReadAsByteArrayAsync();
        if (bytes.LongLength != document.Size)
        {
            throw new MyDocsException(MyDocsErrors.Format);
        }

        using var stream = new MemoryStream(bytes, writable: false);
        await _vault.UploadAsync(objectId, stream, new DocumentMetadata
        {
            Label = document.Title, FileName = document.FileName, ContentType = document.ContentType, Size = bytes.LongLength, Date = document.Date,
            MyDocsId = document.Id, MyDocsHash = document.Hash,
        });
    }

    private async Task<MyDocsExport> FetchAsync(MyDocsSettings settings, CancellationToken cancellationToken)
    {
        using var client = Client(settings);
        using var response = await SendAsync(() => client.GetAsync(new Uri("api/mylife/export", UriKind.Relative), cancellationToken));
        try
        {
            return await response.Content.ReadFromJsonAsync<MyDocsExport>(Json, cancellationToken) ?? throw new MyDocsException(MyDocsErrors.Format);
        }
        catch (JsonException exception)
        {
            throw new MyDocsException(MyDocsErrors.Format, exception);
        }
    }

    private async Task ConfirmAsync(MyDocsSettings settings, string fingerprint)
    {
        using var client = Client(settings);
        using var _ = await SendAsync(() => client.PostAsJsonAsync(new Uri("api/mylife/synced", UriKind.Relative), new { fingerprint }, Json));
    }

    private static async Task<HttpResponseMessage> SendAsync(Func<Task<HttpResponseMessage>> send)
    {
        HttpResponseMessage response;
        try
        {
            response = await send();
        }
        catch (HttpRequestException exception)
        {
            // Ohne Netz, falsche Adresse oder von der CSP bzw. CORS blockiert: Der Browser sagt nicht, was davon.
            throw new MyDocsException(MyDocsErrors.Unreachable, exception);
        }

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        response.Dispose();
        throw new MyDocsException(response.StatusCode == HttpStatusCode.Unauthorized ? MyDocsErrors.Token : MyDocsErrors.Failed);
    }

    /// <summary>Ohne Cookies (fremder Origin), mit dem lesenden Token und dem CSRF-Header von myDocs für die Quittung.</summary>
    private HttpClient Client(MyDocsSettings settings)
    {
        var client = new HttpClient(_handlers(), disposeHandler: true) { BaseAddress = new Uri(settings.ServerUrl), Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.Token);
        client.DefaultRequestHeaders.Add("X-MyDocs-Request", "1");
        return client;
    }

    private async Task SaveAsync(MyDocsSettings settings)
    {
        var vmk = _session.VaultKey ?? throw new VaultException(ClientVaultErrors.Locked);
        await _api.SetMyDocsAsync(await _crypto.EncryptAsync(vmk, JsonSerializer.SerializeToUtf8Bytes(settings, Json), KeyContexts.MyDocsBridge));
        Settings = settings;
        _loaded = true;
    }

    private void OnSessionChanged()
    {
        if (_session.State == SessionState.Unlocked)
        {
            return;
        }

        Settings = null;
        Pending = null;
        Error = null;
        _loaded = false;
    }
}
