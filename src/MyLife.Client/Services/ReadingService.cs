using MyLife.Shared.Trust;

namespace MyLife.Client.Services;

/// <summary>Ein Schritt im Lesemodus: die Briefe an mich oder ein Bereich.</summary>
public sealed class ReadingStep
{
    public required string Key { get; init; }

    /// <summary>Bereich oder null für die persönlichen Briefe.</summary>
    public int? Area { get; init; }

    /// <summary>Umschläge dieses Schritts (ein Bereich oder mehrere Briefe).</summary>
    public required IReadOnlyList<SharedGrant> Grants { get; init; }

    /// <summary>Geöffnete Inhalte; leer, bis der Schritt das erste Mal aufgeschlagen wird.</summary>
    public List<SharedContent> Contents { get; } = [];

    public bool Loaded { get; set; }

    public IEnumerable<VaultItem> Items => Contents.SelectMany(c => c.Items);

    public IEnumerable<(SharedContent Content, VaultFile File)> FilesOf(Guid objectId) =>
        Contents.SelectMany(c => c.Files.Where(f => f.ObjectId == objectId).Select(f => (c, f)));
}

/// <summary>Alles, was eine Person für mich freigegeben hat, als geführter Weg.</summary>
public sealed record ReadingBook(SharedOwner Owner, bool Death, bool Incapacity, IReadOnlyList<ReadingStep> Steps);

/// <summary>
/// Lesemodus für Hinterbliebene (M2.5): Personen, deren Notfallzugang für mich freigegeben ist, und ihre Inhalte
/// als ruhiger, linearer Weg. Bis zur Checkliste (M3) führt der Weg entlang der Bereiche, in einer Reihenfolge,
/// die im Ernstfall hilft: zuerst Briefe, Wünsche und Kontakte, dann Dokumente, Versicherungen und Konten.
/// </summary>
public sealed class ReadingService(SharedService shared)
{
    /// <summary>Reihenfolge der Bereiche im Ernstfall (ohne Checkliste 12, die folgt mit M3).</summary>
    public static IReadOnlyList<int> Order { get; } = [11, 10, 0, 6, 4, 1, 2, 3, 13, 5, 7, 8, 9];

    /// <summary>Personen, für die mindestens ein Modus (verstorben oder handlungsunfähig) freigegeben ist.</summary>
    public async Task<IReadOnlyList<SharedOwner>> GetOwnersAsync() =>
        [.. (await shared.GetOwnersAsync()).Where(o => o.Grants.Any(g => g.Dto.Mode != GrantMode.Live))];

    public async Task<ReadingBook?> OpenAsync(Guid ownerId)
    {
        var owner = (await GetOwnersAsync()).FirstOrDefault(o => o.OwnerId == ownerId);
        if (owner is null)
        {
            return null;
        }

        var steps = new List<ReadingStep>();
        var letters = owner.Grants.Where(g => g.Dto.Kind == GrantKind.Letter).GroupBy(g => g.Dto.ObjectId).Select(Preferred).ToList();
        if (letters.Count > 0)
        {
            steps.Add(new ReadingStep { Key = "letters", Area = null, Grants = letters });
        }

        var areas = owner.Grants.Where(g => g.Dto.Kind == GrantKind.Area).GroupBy(g => g.Dto.Area!.Value).ToDictionary(g => g.Key, Preferred);
        foreach (var area in Order.Where(areas.ContainsKey))
        {
            steps.Add(new ReadingStep { Key = $"area-{area}", Area = area, Grants = [areas[area]] });
        }

        return new ReadingBook(
            owner,
            owner.Grants.Any(g => g.Dto.Mode == GrantMode.Death),
            owner.Grants.Any(g => g.Dto.Mode == GrantMode.Incapacity),
            steps);
    }

    /// <summary>Öffnet die Umschläge eines Schritts beim ersten Aufschlagen.</summary>
    public async Task LoadAsync(ReadingBook book, ReadingStep step)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(step);
        if (step.Loaded)
        {
            return;
        }

        foreach (var grant in step.Grants)
        {
            step.Contents.Add(await shared.OpenAsync(book.Owner.OwnerId, grant.Dto));
        }

        step.Loaded = true;
    }

    public Task<byte[]> DownloadAsync(SharedContent content, VaultFile file) => shared.DownloadAsync(content, file);

    /// <summary>Schlüssel wieder aus dem Krypto-Modul entfernen, wenn der Lesemodus endet.</summary>
    public async Task CloseAsync(ReadingBook? book)
    {
        foreach (var content in book?.Steps.SelectMany(s => s.Contents) ?? [])
        {
            await shared.CloseAsync(content);
        }
    }

    /// <summary>Gleicher Inhalt in mehreren Modi: einer genügt, bevorzugt ein geprüfter.</summary>
    private static SharedGrant Preferred(IEnumerable<SharedGrant> grants) =>
        grants.OrderByDescending(g => g.Verified).ThenBy(g => g.Dto.Mode == GrantMode.Live).First();
}
