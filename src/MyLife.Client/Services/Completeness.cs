using MyLife.Shared.Vault;

namespace MyLife.Client.Services;

/// <summary>Was an einem Eintrag fehlt: Schlüssel und Beschriftung (Feld, Wunsch, Ablaufdatum, Ablageort).</summary>
public sealed record MissingField(string Key, string Label);

/// <summary>Vollständigkeit eines Bereichs: Anteil in Prozent (null = noch leer), Einträge mit Lücken, fehlende typische Einträge.</summary>
public sealed record AreaCompleteness(int Area, int? Percent, int Incomplete, IReadOnlyList<TypicalEntry> MissingTypical)
{
    public bool IsEmpty => Percent is null;
}

/// <summary>
/// Vollständigkeitsanzeige (M3.3), ganz im Browser: Ein Eintrag ist vollständig, wenn seine sichtbaren Pflicht- und
/// empfohlenen Felder ausgefüllt sind, dazu der Wunsch (wo es Wünsche gibt), der Ablageort des Originals (Urkunden)
/// und das Ablaufdatum (Ausweise, Karten). Gezählt werden nur aktive Einträge; beendete sind erledigt.
/// </summary>
public sealed class Completeness(SchemaText text)
{
    /// <summary>Was an einem Eintrag fehlt, in der Reihenfolge des Formulars.</summary>
    public IReadOnlyList<MissingField> Missing(VaultItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var payload = item.Payload;
        var missing = new List<MissingField>();
        foreach (var field in Expected(item.Type).Where(f => FieldValues.IsVisible(f, payload)))
        {
            if (payload.Get(field.Key).Length == 0)
            {
                missing.Add(new MissingField(field.Key, text.Field(item.Type, field)));
            }
        }

        if (item.Type.OriginalLocationRequired && string.IsNullOrWhiteSpace(payload.OriginalLocation))
        {
            missing.Add(new MissingField("originalLocation", text.OriginalLocation));
        }

        if (item.Type.ExpiryRecommended && payload.ExpiresOn is null)
        {
            missing.Add(new MissingField("expiresOn", text.Expiry(item.Type)));
        }

        if (item.Type.Wishes.Count > 0 && string.IsNullOrWhiteSpace(payload.Wish))
        {
            missing.Add(new MissingField("wish", text.WishLabel));
        }

        return missing;
    }

    /// <summary>Anteil des Ausgefüllten an allem Erwarteten (0–1).</summary>
    public double Score(VaultItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var expected = Expected(item.Type).Count(f => FieldValues.IsVisible(f, item.Payload))
            + (item.Type.OriginalLocationRequired ? 1 : 0) + (item.Type.ExpiryRecommended ? 1 : 0) + (item.Type.Wishes.Count > 0 ? 1 : 0);
        return expected == 0 ? 1 : 1 - ((double)Missing(item).Count / expected);
    }

    public AreaCompleteness ForArea(int area, IEnumerable<VaultItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var active = Active(items).Where(i => i.Area == area).ToList();
        var missingTypical = (ObjectCatalog.FindArea(area)?.Typical ?? []).Where(t => !active.Any(i => Matches(t, i))).ToList();
        return new AreaCompleteness(
            area,
            active.Count == 0 ? null : Percent(active),
            active.Count(i => Missing(i).Count > 0),
            missingTypical);
    }

    /// <summary>Gesamtanteil über alle aktiven Einträge; null, solange der Tresor leer ist.</summary>
    public int? Overall(IEnumerable<VaultItem> items)
    {
        var active = Active(items).ToList();
        return active.Count == 0 ? null : Percent(active);
    }

    /// <summary>Beschriftung eines typischen Eintrags, etwa „Urkunde / Dokument: Patientenverfügung“ oder „Bankkonto“.</summary>
    public string Label(TypicalEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var type = ObjectCatalog.Find(entry.TypeKey)!;
        return entry is { FieldKey: { } key, Value: { } value } && type.Field(key) is { OptionSet: { } set }
            ? $"{text.Type(type)}: {text.Option(set, value)}"
            : text.Type(type);
    }

    /// <summary>Adresse zum Anlegen, mit vorbelegtem Wert.</summary>
    public static string NewHref(TypicalEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return entry is { FieldKey: { } key, Value: { } value }
            ? $"object/new/{entry.TypeKey}?field={Uri.EscapeDataString(key)}&value={Uri.EscapeDataString(value)}"
            : $"object/new/{entry.TypeKey}";
    }

    public static bool Matches(TypicalEntry entry, VaultItem item)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(item);
        return item.Type.Key == entry.TypeKey && (entry.FieldKey is null || item.Payload.Get(entry.FieldKey) == entry.Value);
    }

    private static IEnumerable<FieldDefinition> Expected(ObjectTypeDefinition type) => type.Fields.Where(f => f.Required || f.Recommended);

    private static IEnumerable<VaultItem> Active(IEnumerable<VaultItem> items) =>
        items.Where(i => !i.IsDeleted && i.Status == ObjectStatus.Active);

    private int Percent(IReadOnlyCollection<VaultItem> items) => (int)Math.Round(items.Average(Score) * 100, MidpointRounding.AwayFromZero);
}
