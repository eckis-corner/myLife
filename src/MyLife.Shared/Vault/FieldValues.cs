using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MyLife.Shared.Vault;

/// <summary>
/// Kanonische Form, Prüfung und Anzeige von Feldwerten. Kanonisch heißt: Zahlen invariant mit Punkt, Datum
/// <c>yyyy-MM-dd</c>, IBAN ohne Leerzeichen, Ja/Nein als <c>true</c>/<c>false</c>, Verweise als Guid.
/// Fehlercodes sind Ressourcenschlüssel-Endungen (<c>FieldError_{Code}</c>).
/// </summary>
public static partial class FieldValues
{
    public const string DateFormat = "yyyy-MM-dd";
    public const int MaxTextLength = 500;
    public const int MaxLongTextLength = 20_000;

    public static string? Validate(FieldDefinition field, string? value)
    {
        ArgumentNullException.ThrowIfNull(field);
        if (string.IsNullOrWhiteSpace(value))
        {
            return field.Required ? "required" : null;
        }

        var max = field.Type == FieldType.LongText ? MaxLongTextLength : MaxTextLength;
        if (value.Length > max)
        {
            return "too-long";
        }

        return field.Type switch
        {
            FieldType.Number or FieldType.Money or FieldType.Percent => TryNumber(value, out _) ? null : "number",
            FieldType.Date => TryDate(value, out _) ? null : "date",
            FieldType.Iban => IsIban(value) ? null : "iban",
            FieldType.Bic => BicPattern().IsMatch(value) ? null : "bic",
            FieldType.Email => EmailPattern().IsMatch(value) ? null : "email",
            FieldType.Url => Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) ? null : "url",
            FieldType.Phone => PhonePattern().IsMatch(value) ? null : "phone",
            FieldType.Last4 => Last4Pattern().IsMatch(value) ? null : "last4",
            FieldType.YesNo => value is "true" or "false" ? null : "invalid",
            FieldType.Reference => Guid.TryParse(value, out _) ? null : "invalid",
            FieldType.Choice => field.OptionSet is not null && ObjectCatalog.OptionSets.TryGetValue(field.OptionSet, out var options) && options.Contains(value) ? null : "invalid",
            _ => null,
        };
    }

    /// <summary>Prüft alle sichtbaren Felder und die gemeinsamen Angaben. Schlüssel: Feldschlüssel bzw. <c>title</c>/<c>originalLocation</c>.</summary>
    public static Dictionary<string, string> ValidateObject(ObjectTypeDefinition type, VaultPayload payload)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(payload);
        var errors = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(payload.Title))
        {
            errors["title"] = "required";
        }
        else if (payload.Title.Length > 200)
        {
            errors["title"] = "too-long";
        }

        if (type.OriginalLocationRequired && string.IsNullOrWhiteSpace(payload.OriginalLocation))
        {
            errors["originalLocation"] = "required";
        }

        foreach (var field in type.Fields.Where(f => IsVisible(f, payload)))
        {
            if (Validate(field, payload.Get(field.Key)) is { } error)
            {
                errors[field.Key] = error;
            }
        }

        return errors;
    }

    public static bool IsVisible(FieldDefinition field, VaultPayload payload)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(payload);
        return field.VisibleWhen is not { } condition || condition.Values.Contains(payload.Get(condition.FieldKey));
    }

    public static bool TryNumber(string? value, out decimal number) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out number);

    public static bool TryDate(string? value, out DateOnly date) =>
        DateOnly.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    public static string FromNumber(decimal? number) => number?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    public static string FromDate(DateOnly? date) => date?.ToString(DateFormat, CultureInfo.InvariantCulture) ?? string.Empty;

    /// <summary>IBAN ohne Leerzeichen und groß geschrieben.</summary>
    public static string NormalizeIban(string? value) =>
        new((value ?? string.Empty).Where(c => !char.IsWhiteSpace(c)).Select(char.ToUpperInvariant).ToArray());

    /// <summary>Prüfziffer nach ISO 13616 (Modulo 97).</summary>
    public static bool IsIban(string? value)
    {
        var iban = NormalizeIban(value);
        if (!IbanPattern().IsMatch(iban))
        {
            return false;
        }

        var rearranged = iban[4..] + iban[..4];
        var remainder = 0;
        foreach (var c in rearranged)
        {
            var digits = char.IsDigit(c) ? (c - '0').ToString(CultureInfo.InvariantCulture) : (c - 'A' + 10).ToString(CultureInfo.InvariantCulture);
            foreach (var d in digits)
            {
                remainder = ((remainder * 10) + (d - '0')) % 97;
            }
        }

        return remainder == 1;
    }

    /// <summary>Anzeige ohne Lokalisierung von Auswahlwerten und Verweisen (die übernimmt der Client).</summary>
    public static string Format(FieldDefinition field, string? value, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(field);
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return field.Type switch
        {
            FieldType.Money when TryNumber(value, out var amount) => amount.ToString("N2", culture) + " €",
            FieldType.Percent when TryNumber(value, out var percent) => percent.ToString("0.###", culture) + " %",
            FieldType.Number when TryNumber(value, out var number) => number.ToString(field.Key.EndsWith("Year", StringComparison.Ordinal) ? "0" : "G", culture),
            FieldType.Date when TryDate(value, out var date) => date.ToString("d", culture),
            FieldType.Iban => GroupIban(value),
            _ => value,
        };
    }

    public static string GroupIban(string value)
    {
        var iban = NormalizeIban(value);
        var builder = new StringBuilder();
        for (var i = 0; i < iban.Length; i++)
        {
            if (i > 0 && i % 4 == 0)
            {
                builder.Append(' ');
            }

            builder.Append(iban[i]);
        }

        return builder.ToString();
    }

    [GeneratedRegex("^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$")]
    private static partial Regex IbanPattern();

    [GeneratedRegex("^[A-Za-z]{6}[A-Za-z0-9]{2}([A-Za-z0-9]{3})?$")]
    private static partial Regex BicPattern();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"^\+?[0-9 ()/\-.]{3,30}$")]
    private static partial Regex PhonePattern();

    [GeneratedRegex("^[0-9]{4}$")]
    private static partial Regex Last4Pattern();
}
