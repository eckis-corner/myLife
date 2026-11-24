using System.Text.RegularExpressions;
using MyLife.Crypto;
using MyLife.Shared.Vault;

namespace MyLife.Client.Services;

/// <summary>
/// Prüfungen „Was gehört nicht hinein“ (Architektur 6.2), nur im Browser: vollständige Kartennummern (Luhn),
/// PIN, TAN und ähnliche Begriffe mit Wert, Passwörter mit Wert und Seed-Phrasen (12 bis 24 Wörter der BIP-39-Liste).
/// Sie warnen nur; speichern lässt sich trotzdem.
/// </summary>
public static partial class ContentChecks
{
    public const string CardNumber = "card-number";
    public const string Secret = "secret";
    public const string SeedPhrase = "seed-phrase";

    private static readonly Lazy<HashSet<string>> SeedWords = new(() => new HashSet<string>(Bip39.Words, StringComparer.Ordinal));

    /// <summary>Codes der gefundenen Probleme (<see cref="CardNumber"/>, <see cref="Secret"/>, <see cref="SeedPhrase"/>), ohne Doppelte.</summary>
    public static IReadOnlyList<string> Check(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var found = new List<string>();
        if (ContainsCardNumber(text))
        {
            found.Add(CardNumber);
        }

        if (SecretTerm().IsMatch(text) || SecretWithValue().IsMatch(text))
        {
            found.Add(Secret);
        }

        if (ContainsSeedPhrase(text))
        {
            found.Add(SeedPhrase);
        }

        return found;
    }

    /// <summary>Prüft alle Freitexte eines Objekts (nicht IBAN, Zahlen oder Auswahlwerte).</summary>
    public static IReadOnlyList<string> Check(ObjectTypeDefinition type, VaultPayload payload)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(payload);
        IEnumerable<string?> texts =
        [
            payload.Title, payload.Notes, payload.WishNote, payload.OriginalLocation, payload.FollowUpNote,
            .. type.Fields.Where(f => f.Type is FieldType.Text or FieldType.LongText).Select(f => payload.Get(f.Key)),
            .. payload.CustomFields.SelectMany(c => new[] { c.Label, c.Value }),
        ];
        return [.. texts.SelectMany(Check).Distinct()];
    }

    public static bool ContainsCardNumber(string text)
    {
        foreach (Match match in DigitRun().Matches(text))
        {
            var digits = new string([.. match.Value.Where(char.IsAsciiDigit)]);
            if (digits.Length is >= 13 and <= 19 && Luhn(digits))
            {
                return true;
            }
        }

        return false;
    }

    public static bool Luhn(string digits)
    {
        ArgumentNullException.ThrowIfNull(digits);
        var sum = 0;
        var doubleIt = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var d = digits[i] - '0';
            if (doubleIt)
            {
                d *= 2;
                if (d > 9)
                {
                    d -= 9;
                }
            }

            sum += d;
            doubleIt = !doubleIt;
        }

        return sum % 10 == 0;
    }

    public static bool ContainsSeedPhrase(string text)
    {
        var run = 0;
        foreach (var word in Words().Matches(text).Select(m => m.Value.ToLowerInvariant()))
        {
            run = SeedWords.Value.Contains(word) ? run + 1 : 0;
            if (run >= 12)
            {
                return true;
            }
        }

        return false;
    }

    // Ziffernfolgen mit einzelnen Leerzeichen oder Bindestrichen, wie Kartennummern geschrieben werden.
    [GeneratedRegex(@"\d(?:[ \-]?\d){12,22}")]
    private static partial Regex DigitRun();

    // PIN, TAN, PUK, CVC/CVV als eigenes Wort: kommen in Notizen sonst kaum vor.
    [GeneratedRegex(@"\b(PIN|TAN|PUK|CVC|CVV|CVV2|mTAN)\b", RegexOptions.CultureInvariant)]
    private static partial Regex SecretTerm();

    // Passwort, Kennwort, Password nur mit Wert dahinter („Passwort: …“).
    [GeneratedRegex(@"\b(passwort|kennwort|password|pw)\s*[:=]\s*\S+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SecretWithValue();

    [GeneratedRegex(@"[A-Za-z]+")]
    private static partial Regex Words();
}
