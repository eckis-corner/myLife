using System.Globalization;
using MyLife.Shared.Vault;

namespace MyLife.Client.Services;

/// <summary>Treffer der Volltextsuche mit dem Textstück, in dem der erste Suchbegriff vorkommt.</summary>
public sealed record SearchHit(VaultItem Item, string Snippet, int Score);

/// <summary>
/// Volltextsuche im Browser über die entschlüsselten Objekte (Entscheidung 19): Titel, Typ, Bereich, alle Felder,
/// Wunsch, Notizen, Zusatzfelder und Bezeichnungen der Dokumente. Ein Objekt trifft, wenn alle Begriffe vorkommen;
/// Groß- und Kleinschreibung sowie Akzente spielen keine Rolle. Der Inhalt von PDFs wird nicht durchsucht.
/// </summary>
public sealed class VaultSearch(VaultService vault, SchemaText text)
{
    private const CompareOptions Options = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    public IReadOnlyList<SearchHit> Search(string? query, bool includeEnded = true, int limit = 200)
    {
        var terms = (query ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (terms.Length == 0)
        {
            return [];
        }

        var compare = CultureInfo.CurrentCulture.CompareInfo;
        var hits = new List<SearchHit>();
        foreach (var item in vault.Current.Where(i => includeEnded || i.Status == ObjectStatus.Active))
        {
            var texts = IndexTexts(item).Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
            if (!terms.All(term => texts.Any(t => compare.IndexOf(t, term, Options) >= 0)))
            {
                continue;
            }

            var score = terms.Sum(term => compare.IndexOf(item.Title, term, Options) >= 0 ? 10 : 1);
            var snippet = texts.Skip(1).FirstOrDefault(t => compare.IndexOf(t, terms[0], Options) >= 0) ?? string.Empty;
            hits.Add(new SearchHit(item, Shorten(snippet, terms[0], compare), score));
        }

        return [.. hits.OrderByDescending(h => h.Score).ThenBy(h => h.Item.Title, StringComparer.CurrentCultureIgnoreCase).Take(limit)];
    }

    /// <summary>Alle durchsuchbaren Texte eines Objekts; der Titel steht vorn.</summary>
    public IEnumerable<string> IndexTexts(VaultItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        yield return item.Title;
        yield return text.Type(item.Type);
        yield return text.Area(item.Area);
        foreach (var row in text.Rows(item, vault.TitleOf))
        {
            yield return row.Value;
        }

        foreach (var custom in item.Payload.CustomFields)
        {
            yield return custom.Label;
        }

        // IBAN auch ohne Leerzeichen finden.
        foreach (var field in item.Type.Fields.Where(f => f.Type == FieldType.Iban))
        {
            yield return item.Payload.Get(field.Key);
        }

        foreach (var file in vault.FilesOf(item.Id).Where(f => !f.IsDeleted))
        {
            yield return file.Metadata.Label;
            yield return file.Metadata.FileName;
        }
    }

    private static string Shorten(string value, string term, CompareInfo compare)
    {
        var flat = value.ReplaceLineEndings(" ");
        if (flat.Length <= 120)
        {
            return flat;
        }

        var position = Math.Max(compare.IndexOf(flat, term, Options), 0);
        var start = Math.Max(position - 40, 0);
        var length = Math.Min(120, flat.Length - start);
        return (start > 0 ? "…" : string.Empty) + string.Concat(flat.AsSpan(start, length), start + length < flat.Length ? "…" : string.Empty);
    }
}
