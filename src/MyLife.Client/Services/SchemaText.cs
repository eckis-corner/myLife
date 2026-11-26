using System.Globalization;
using Microsoft.Extensions.Localization;
using MyLife.Client.Resources;
using MyLife.Shared.Vault;

namespace MyLife.Client.Services;

/// <summary>Eine Zeile im Datenblatt: Bezeichnung und angezeigter Wert, optional mit Verweis auf ein Objekt.</summary>
public sealed record DatasheetRow(string Label, string Value, Guid? Reference = null, bool Multiline = false);

/// <summary>
/// Lokalisierte Texte zum Schema (Bereiche, Objekttypen, Felder, Auswahllisten) und das Datenblatt eines Objekts.
/// Formular, Datenblatt, Notfallordner und Suche nutzen dieselben Texte (Architektur 6.2).
/// </summary>
public sealed class SchemaText(IStringLocalizer<SharedResource> l)
{
    public string Area(int number) => l[$"Area_{number.ToString(CultureInfo.InvariantCulture)}"];

    public string AreaText(int number) => l[$"AreaText_{number.ToString(CultureInfo.InvariantCulture)}"];

    public string Type(string key) => l[$"Type_{key}"];

    public string Type(ObjectTypeDefinition type) => Type(type.Key);

    public string Field(ObjectTypeDefinition type, FieldDefinition field)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(field);
        var specific = l[$"Field_{type.Key}_{field.Key}"];
        return specific.ResourceNotFound ? l[$"Field_{field.Key}"] : specific;
    }

    public string? Hint(FieldDefinition field) => field is { HasHint: true } ? l[$"FieldHint_{field.Key}"].Value : null;

    public string Option(string set, string value) => l[$"Option_{set}_{value}"];

    public string Wish(string value) => Option("wish", value);

    /// <summary>Beschriftung des Wunschs für die Hinterbliebenen.</summary>
    public string WishLabel => l["CommonWish"];

    /// <summary>Beschriftung des Ablageorts des Originals.</summary>
    public string OriginalLocation => l["CommonOriginalLocation"];

    public string Expiry(ObjectTypeDefinition type) => type is { HasExpiryLabel: true } ? l[$"Expiry_{type.Key}"] : l["Expiry_default"];

    public string Status(ObjectStatus status) => l[$"ObjectStatus_{status}"];

    public string Error(string code) => l[$"FieldError_{code}"];

    /// <summary>Angezeigter Wert eines Felds: Auswahl und Ja/Nein übersetzt, Verweise als Titel, Zahlen und Daten in der Kultur.</summary>
    public string Display(FieldDefinition field, string? value, Func<Guid, string?> titleOf)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(titleOf);
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return field.Type switch
        {
            FieldType.Choice when field.OptionSet is not null => Option(field.OptionSet, value),
            FieldType.YesNo => value == "true" ? l["Yes"] : l["No"],
            FieldType.Reference => Guid.TryParse(value, out var id) ? titleOf(id) ?? l["ReferenceMissing"] : value,
            _ => FieldValues.Format(field, value, CultureInfo.CurrentCulture),
        };
    }

    /// <summary>Alle ausgefüllten Angaben eines Objekts in der Reihenfolge des Schemas, dann die gemeinsamen Angaben.</summary>
    public IReadOnlyList<DatasheetRow> Rows(VaultItem item, Func<Guid, string?> titleOf)
    {
        ArgumentNullException.ThrowIfNull(item);
        var payload = item.Payload;
        var rows = new List<DatasheetRow>();
        foreach (var field in item.Type.Fields.Where(f => FieldValues.IsVisible(f, payload)))
        {
            var value = payload.Get(field.Key);
            if (value.Length == 0)
            {
                continue;
            }

            Guid? reference = field.Type switch
            {
                FieldType.Reference when Guid.TryParse(value, out var id) => id,
                FieldType.Location => payload.LocationId(field.Key),
                _ => null,
            };
            rows.Add(new DatasheetRow(Field(item.Type, field), Display(field, value, titleOf), reference, field.Type == FieldType.LongText));
        }

        if (payload.ExpiresOn is { } expires)
        {
            rows.Add(new DatasheetRow(Expiry(item.Type), expires.ToString("d", CultureInfo.CurrentCulture)));
        }

        if (!string.IsNullOrWhiteSpace(payload.OriginalLocation))
        {
            rows.Add(new DatasheetRow(l["CommonOriginalLocation"], payload.OriginalLocation, payload.LocationId(VaultPayload.OriginalLocationKey)));
        }

        if (payload.ContactId is { } contact)
        {
            rows.Add(new DatasheetRow(l["CommonContact"], titleOf(contact) ?? l["ReferenceMissing"], contact));
        }

        if (!string.IsNullOrWhiteSpace(payload.Wish))
        {
            var wish = Wish(payload.Wish);
            rows.Add(new DatasheetRow(l["CommonWish"], string.IsNullOrWhiteSpace(payload.WishNote) ? wish : $"{wish} – {payload.WishNote}", Multiline: true));
        }
        else if (!string.IsNullOrWhiteSpace(payload.WishNote))
        {
            rows.Add(new DatasheetRow(l["CommonWish"], payload.WishNote, Multiline: true));
        }

        foreach (var custom in payload.CustomFields.Where(c => !string.IsNullOrWhiteSpace(c.Value)))
        {
            rows.Add(new DatasheetRow(string.IsNullOrWhiteSpace(custom.Label) ? l["CommonCustomField"] : custom.Label, custom.Value, Multiline: true));
        }

        if (!string.IsNullOrWhiteSpace(payload.Notes))
        {
            rows.Add(new DatasheetRow(l["CommonNotes"], payload.Notes, Multiline: true));
        }

        if (payload.HasFollowUp)
        {
            rows.Add(new DatasheetRow(l["CommonFollowUp"], FollowUp(payload), Multiline: true));
        }

        if (payload.ReviewedOn is { } reviewed)
        {
            rows.Add(new DatasheetRow(l["CommonReviewedOn"], reviewed.ToString("d", CultureInfo.CurrentCulture)));
        }

        return rows;
    }

    /// <summary>Wiedervorlage als Text: Datum und Hinweis, je nachdem, was gesetzt ist.</summary>
    public static string FollowUp(VaultPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var date = payload.FollowUpOn?.ToString("d", CultureInfo.CurrentCulture);
        var note = string.IsNullOrWhiteSpace(payload.FollowUpNote) ? null : payload.FollowUpNote.Trim();
        return date is null ? note ?? string.Empty : note is null ? date : $"{date} – {note}";
    }

    /// <summary>Kurzinfo für Listen: die ersten zwei ausgefüllten Pflicht- oder Auswahlfelder.</summary>
    public string Summary(VaultItem item, Func<Guid, string?> titleOf)
    {
        ArgumentNullException.ThrowIfNull(item);
        var parts = item.Type.Fields
            .Where(f => f.Type is not (FieldType.LongText or FieldType.Reference) && item.Payload.Get(f.Key).Length > 0)
            .OrderByDescending(f => f.Required || f.Type == FieldType.Choice)
            .Take(2)
            .Select(f => Display(f, item.Payload.Get(f.Key), titleOf));
        return string.Join(" · ", parts);
    }
}
