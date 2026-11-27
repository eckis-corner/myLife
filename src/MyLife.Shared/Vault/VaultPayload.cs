using System.Text.Json;
using System.Text.Json.Serialization;
using MyCommon.Catalog;

namespace MyLife.Shared.Vault;

/// <summary>
/// Inhalt eines Objekts. Wird im Browser als JSON serialisiert und mit dem Bereichsschlüssel verschlüsselt
/// (Kontext <c>object:{Typ}:{Id}</c>); der Server sieht davon nichts. Felder sind Zeichenketten in kanonischer Form
/// (<see cref="FieldValues"/>), damit neue Felder keine Migration brauchen.
/// </summary>
public sealed class VaultPayload : IFieldValues
{
    public int SchemaVersion { get; set; } = 1;

    public string Title { get; set; } = string.Empty;

    public Dictionary<string, string> Fields { get; set; } = [];

    /// <summary>Wunsch für die Hinterbliebenen, ein Wert der Auswahlliste <c>wish</c>.</summary>
    public string? Wish { get; set; }

    public string? WishNote { get; set; }

    /// <summary>Ansprechpartner: Verweis auf ein Objekt vom Typ <c>contact</c>.</summary>
    public Guid? ContactId { get; set; }

    /// <summary>Verknüpfte Objekte; im Datenblatt in beide Richtungen sichtbar (Entscheidung 40).</summary>
    public List<Guid> Links { get; set; } = [];

    /// <summary>Ablageort des Originals: Name des Ablageorts (Verweis unter <see cref="OriginalLocationKey"/> in <see cref="Locations"/>).</summary>
    public string? OriginalLocation { get; set; }

    /// <summary>
    /// Gewählte Ablageorte (Entscheidung 143): Feldschlüssel bzw. <see cref="OriginalLocationKey"/> → ID des Ablageorts.
    /// Der Name steht zusätzlich im Feld selbst, damit ihn jeder sieht, der diesen Eintrag sehen darf, auch ohne
    /// Freigabe des Bereichs „Ablageorte“. Beim Umbenennen eines Ablageorts zieht myLife die Namen nach.
    /// </summary>
    public Dictionary<string, Guid> Locations { get; set; } = [];

    /// <summary>Ablaufdatum, Bedeutung je Objekttyp (Entscheidung 39).</summary>
    public DateOnly? ExpiresOn { get; set; }

    public string? Notes { get; set; }

    public List<CustomField> CustomFields { get; set; } = [];

    /// <summary>Zuletzt geprüft („Als geprüft markieren“).</summary>
    public DateOnly? ReviewedOn { get; set; }

    /// <summary>Wiedervorlage: Datum, an dem das Objekt wieder angesehen werden soll. Optional (Entscheidung 69).</summary>
    public DateOnly? FollowUpOn { get; set; }

    /// <summary>Hinweis zur Wiedervorlage, etwa „Beitrag vergleichen“. Optional.</summary>
    public string? FollowUpNote { get; set; }

    /// <summary>Hat das Objekt eine Wiedervorlage (Datum oder Hinweis)?</summary>
    public bool HasFollowUp => FollowUpOn is not null || !string.IsNullOrWhiteSpace(FollowUpNote);

    /// <summary>Ist die Wiedervorlage zum Stichtag fällig (Datum erreicht oder überschritten)?</summary>
    public bool IsFollowUpDue(DateOnly today) => FollowUpOn is { } date && date <= today;

    /// <summary>Schlüssel des gemeinsamen Felds „Ablageort des Originals“ in <see cref="Locations"/>.</summary>
    public const string OriginalLocationKey = "originalLocation";

    /// <summary>Name im Ablageort-Feld <paramref name="key"/> (Feldschlüssel oder <see cref="OriginalLocationKey"/>).</summary>
    public string LocationText(string key) => key == OriginalLocationKey ? OriginalLocation ?? string.Empty : Get(key);

    /// <summary>Gewählter Ablageort im Feld <paramref name="key"/>; null bei leerem Feld oder altem Freitext.</summary>
    public Guid? LocationId(string key) => Locations.TryGetValue(key, out var id) ? id : null;

    /// <summary>Setzt ein Ablageort-Feld auf einen Ablageort aus der Tabelle (Verweis und Name) oder leert es.</summary>
    public void SetLocation(string key, Guid? id, string? name)
    {
        if (id is { } location && !string.IsNullOrWhiteSpace(name))
        {
            Locations[key] = location;
            SetLocationText(key, name);
        }
        else
        {
            Locations.Remove(key);
            SetLocationText(key, null);
        }
    }

    /// <summary>Übernimmt den neuen Namen eines Ablageorts in alle Felder, die ihn gewählt haben. Liefert, ob sich etwas geändert hat.</summary>
    public bool RenameLocation(Guid id, string name)
    {
        var changed = false;
        foreach (var key in Locations.Where(l => l.Value == id).Select(l => l.Key).ToList())
        {
            if (LocationText(key) != name.Trim())
            {
                SetLocationText(key, name);
                changed = true;
            }
        }

        return changed;
    }

    private void SetLocationText(string key, string? value)
    {
        if (key == OriginalLocationKey)
        {
            OriginalLocation = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
        else
        {
            Set(key, value);
        }
    }

    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string Get(string key) => Fields.TryGetValue(key, out var value) ? value : string.Empty;

    /// <summary>Setzt einen Wert; leere Werte werden entfernt.</summary>
    public void Set(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Fields.Remove(key);
        }
        else
        {
            Fields[key] = value.Trim();
        }
    }

    public byte[] ToUtf8() => JsonSerializer.SerializeToUtf8Bytes(this, JsonOptions);

    public static VaultPayload FromUtf8(byte[] json) =>
        JsonSerializer.Deserialize<VaultPayload>(json, JsonOptions) ?? throw new JsonException("Leere Nutzlast.");

    public VaultPayload Clone() => FromUtf8(ToUtf8());

    /// <summary>Alle Objekte, auf die dieses Objekt verweist: Ansprechpartner, Verweisfelder, Ablageorte und Verknüpfungen.</summary>
    public IEnumerable<Guid> References(ObjectTypeDefinition? type)
    {
        if (ContactId is { } contact)
        {
            yield return contact;
        }

        foreach (var link in Links)
        {
            yield return link;
        }

        foreach (var location in Locations.Values.Distinct())
        {
            yield return location;
        }

        if (type is null)
        {
            yield break;
        }

        foreach (var field in type.Fields.Where(f => f.Type == FieldType.Reference))
        {
            if (Guid.TryParse(Get(field.Key), out var id))
            {
                yield return id;
            }
        }
    }

    /// <summary>
    /// Ersetzt Verweise, etwa beim Import, wenn Objekte neue IDs bekommen. Unbekannte Verweise entfallen; bei
    /// Ablageorten bleibt dann der Name als Freitext stehen.
    /// </summary>
    public void RemapReferences(ObjectTypeDefinition? type, IReadOnlyDictionary<Guid, Guid> map)
    {
        ContactId = ContactId is { } contact && map.TryGetValue(contact, out var newContact) ? newContact : null;
        Links = [.. Links.Where(map.ContainsKey).Select(l => map[l]).Distinct()];
        Locations = Locations.Where(l => map.ContainsKey(l.Value)).ToDictionary(l => l.Key, l => map[l.Value]);
        if (type is null)
        {
            return;
        }

        foreach (var field in type.Fields.Where(f => f.Type == FieldType.Reference))
        {
            Set(field.Key, Guid.TryParse(Get(field.Key), out var id) && map.TryGetValue(id, out var newId) ? newId.ToString() : null);
        }
    }
}

/// <summary>Frei benanntes Zusatzfeld.</summary>
public sealed record CustomField(string Label, string Value);

/// <summary>Metadaten eines Dokuments, verschlüsselt mit dem Bereichsschlüssel (Kontext <c>document-meta:{Id}</c>).</summary>
public sealed class DocumentMetadata
{
    /// <summary>Bezeichnung, etwa „Police 2024“.</summary>
    public string Label { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = "application/octet-stream";

    /// <summary>Größe des Klartexts in Byte.</summary>
    public long Size { get; set; }

    public DateOnly? Date { get; set; }

    public byte[] ToUtf8() => JsonSerializer.SerializeToUtf8Bytes(this, VaultPayload.JsonOptions);

    public static DocumentMetadata FromUtf8(byte[] json) =>
        JsonSerializer.Deserialize<DocumentMetadata>(json, VaultPayload.JsonOptions) ?? throw new JsonException("Leere Metadaten.");
}
