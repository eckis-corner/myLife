using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyLife.Shared.Vault;

/// <summary>
/// Inhalt eines Objekts. Wird im Browser als JSON serialisiert und mit dem Bereichsschlüssel verschlüsselt
/// (Kontext <c>object:{Typ}:{Id}</c>); der Server sieht davon nichts. Felder sind Zeichenketten in kanonischer Form
/// (<see cref="FieldValues"/>), damit neue Felder keine Migration brauchen.
/// </summary>
public sealed class VaultPayload
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

    /// <summary>Ablageort des Originals.</summary>
    public string? OriginalLocation { get; set; }

    /// <summary>Ablaufdatum, Bedeutung je Objekttyp (Entscheidung 39).</summary>
    public DateOnly? ExpiresOn { get; set; }

    public string? Notes { get; set; }

    public List<CustomField> CustomFields { get; set; } = [];

    /// <summary>Zuletzt geprüft („Als geprüft markieren“).</summary>
    public DateOnly? ReviewedOn { get; set; }

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

    /// <summary>Alle Objekte, auf die dieses Objekt verweist: Ansprechpartner, Verweisfelder und Verknüpfungen.</summary>
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

    /// <summary>Ersetzt Verweise, etwa beim Import, wenn Objekte neue IDs bekommen. Unbekannte Verweise entfallen.</summary>
    public void RemapReferences(ObjectTypeDefinition? type, IReadOnlyDictionary<Guid, Guid> map)
    {
        ContactId = ContactId is { } contact && map.TryGetValue(contact, out var newContact) ? newContact : null;
        Links = [.. Links.Where(map.ContainsKey).Select(l => map[l]).Distinct()];
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
