using System.Globalization;
using System.Xml.Linq;
using MyLife.Shared.Vault;

namespace MyLife.Decrypt;

/// <summary>Beschriftungen aus den Ressourcen der App (Deutsch oder Englisch), damit die Datenblätter so aussehen wie in myLife.</summary>
internal sealed class Texts
{
    private readonly Dictionary<string, string> _texts;

    private Texts(Dictionary<string, string> texts, CultureInfo culture)
    {
        _texts = texts;
        Culture = culture;
    }

    public CultureInfo Culture { get; }

    public static Texts Load(string language)
    {
        var english = language == "en";
        var name = english ? "MyLife.Decrypt.texts.en.resx" : "MyLife.Decrypt.texts.de.resx";
        using var stream = typeof(Texts).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException($"{name} fehlt.");
        var texts = XDocument.Load(stream).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty, StringComparer.Ordinal);
        return new Texts(texts, CultureInfo.GetCultureInfo(english ? "en" : "de"));
    }

    public string this[string key] => _texts.TryGetValue(key, out var value) ? value : key;

    public string Area(int area) => this[$"Area_{area.ToString(CultureInfo.InvariantCulture)}"];

    public string Type(string typeKey) => this[$"Type_{typeKey}"];

    public string Field(ObjectTypeDefinition type, FieldDefinition field) =>
        _texts.TryGetValue($"Field_{type.Key}_{field.Key}", out var specific) ? specific : this[$"Field_{field.Key}"];

    public string Option(string set, string value) => this[$"Option_{set}_{value}"];

    public string Expiry(ObjectTypeDefinition type) => type.HasExpiryLabel ? this[$"Expiry_{type.Key}"] : this["Expiry_default"];

    /// <summary>Datenblatt eines Eintrags als Zeilen „Bezeichnung: Wert“, wie in der App (ohne Dokumente).</summary>
    public IEnumerable<(string Label, string Value)> Rows(ObjectTypeDefinition type, VaultPayload payload, Func<Guid, string?> titleOf)
    {
        foreach (var field in type.Fields.Where(f => FieldValues.IsVisible(f, payload)))
        {
            var value = payload.Get(field.Key);
            if (value.Length == 0)
            {
                continue;
            }

            yield return (Field(type, field), field.Type switch
            {
                FieldType.Choice when field.OptionSet is not null => Option(field.OptionSet, value),
                FieldType.YesNo => value == "true" ? this["Yes"] : this["No"],
                FieldType.Reference => Guid.TryParse(value, out var id) ? titleOf(id) ?? this["ReferenceMissing"] : value,
                _ => FieldValues.Format(field, value, Culture),
            });
        }

        if (payload.ExpiresOn is { } expires)
        {
            yield return (Expiry(type), expires.ToString("d", Culture));
        }

        if (!string.IsNullOrWhiteSpace(payload.OriginalLocation))
        {
            yield return (this["CommonOriginalLocation"], payload.OriginalLocation);
        }

        if (payload.ContactId is { } contact)
        {
            yield return (this["CommonContact"], titleOf(contact) ?? this["ReferenceMissing"]);
        }

        if (!string.IsNullOrWhiteSpace(payload.Wish) || !string.IsNullOrWhiteSpace(payload.WishNote))
        {
            var wish = string.IsNullOrWhiteSpace(payload.Wish) ? null : Option("wish", payload.Wish);
            yield return (this["CommonWish"], string.Join(" – ", new[] { wish, payload.WishNote }.Where(v => !string.IsNullOrWhiteSpace(v))));
        }

        foreach (var custom in payload.CustomFields.Where(c => !string.IsNullOrWhiteSpace(c.Value)))
        {
            yield return (string.IsNullOrWhiteSpace(custom.Label) ? this["CommonCustomField"] : custom.Label, custom.Value);
        }

        if (!string.IsNullOrWhiteSpace(payload.Notes))
        {
            yield return (this["CommonNotes"], payload.Notes);
        }

        if (payload.HasFollowUp)
        {
            yield return (this["CommonFollowUp"], string.Join(" – ",
                new[] { payload.FollowUpOn?.ToString("d", Culture), payload.FollowUpNote }.Where(v => !string.IsNullOrWhiteSpace(v))));
        }

        var links = payload.Links.Select(titleOf).OfType<string>().ToList();
        if (links.Count > 0)
        {
            yield return (this["CommonLinks"], string.Join(", ", links));
        }
    }
}
