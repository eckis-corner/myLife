using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace MyLife.Client.Help;

/// <summary>
/// Kleines, sicheres Markdown für Tutorial und Anleitung. Erlaubt sind Absätze, Aufzählungen (<c>- </c>),
/// nummerierte Listen (<c>1. </c>), Hinweise (<c>&gt; </c>), einfache Tabellen (<c>| … |</c>) und im Text
/// <c>**fett**</c> und <c>`Code`</c>. Jeder Text wird zuerst HTML-kodiert; eigenes HTML gibt es also nicht.
/// </summary>
public static partial class HelpMarkdown
{
    /// <summary>Wandelt den Text in HTML um.</summary>
    public static string ToHtml(string markdown)
    {
        var html = new StringBuilder();
        foreach (var block in Blocks(markdown))
        {
            var lines = block.Split('\n');
            if (lines.All(l => l.StartsWith("- ", StringComparison.Ordinal)))
            {
                html.Append("<ul>");
                foreach (var line in lines)
                {
                    html.Append("<li>").Append(Inline(line[2..])).Append("</li>");
                }

                html.Append("</ul>");
            }
            else if (lines.All(l => NumberedItem().IsMatch(l)))
            {
                html.Append("<ol>");
                foreach (var line in lines)
                {
                    html.Append("<li>").Append(Inline(NumberedItem().Replace(line, string.Empty))).Append("</li>");
                }

                html.Append("</ol>");
            }
            else if (lines.All(l => l.StartsWith("> ", StringComparison.Ordinal)))
            {
                html.Append("<blockquote>")
                    .Append(Inline(string.Join(' ', lines.Select(l => l[2..]))))
                    .Append("</blockquote>");
            }
            else if (lines.Length >= 2 && lines.All(l => l.StartsWith('|') && l.EndsWith('|')) && IsSeparator(lines[1]))
            {
                html.Append("<table><thead><tr>");
                foreach (var cell in Cells(lines[0]))
                {
                    html.Append("<th>").Append(Inline(cell)).Append("</th>");
                }

                html.Append("</tr></thead><tbody>");
                foreach (var row in lines.Skip(2))
                {
                    html.Append("<tr>");
                    foreach (var cell in Cells(row))
                    {
                        html.Append("<td>").Append(Inline(cell)).Append("</td>");
                    }

                    html.Append("</tr>");
                }

                html.Append("</tbody></table>");
            }
            else
            {
                html.Append("<p>").Append(Inline(string.Join(' ', lines))).Append("</p>");
            }
        }

        return html.ToString();
    }

    /// <summary>
    /// Was an dem Text nicht in den erlaubten Umfang passt: gemischte Blöcke, unpaarige Hervorhebungen und HTML.
    /// Leer, wenn alles in Ordnung ist.
    /// </summary>
    public static IReadOnlyList<string> Validate(string markdown)
    {
        var errors = new List<string>();
        foreach (var block in Blocks(markdown))
        {
            var lines = block.Split('\n');
            var kinds = lines.Select(Kind).Distinct().ToList();
            if (kinds.Count > 1)
            {
                errors.Add($"Gemischter Block: {lines[0]}");
            }

            if (kinds[0] == "table" && (lines.Length < 2 || !IsSeparator(lines[1])))
            {
                errors.Add($"Tabelle ohne Trennzeile: {lines[0]}");
            }

            if (kinds[0] == "table" && lines.Select(l => Cells(l).Length).Distinct().Count() > 1)
            {
                errors.Add($"Tabelle mit ungleich vielen Spalten: {lines[0]}");
            }

            foreach (var line in lines)
            {
                if (CountOf(line, "**") % 2 != 0)
                {
                    errors.Add($"Unpaariges **: {line}");
                }

                if (line.Count(c => c == '`') % 2 != 0)
                {
                    errors.Add($"Unpaariges `: {line}");
                }

                // In `Code` sind spitze Klammern erlaubt, etwa `<Datei>`: Sie werden ohnehin kodiert.
                if (HtmlTag().IsMatch(Code().Replace(line, string.Empty)))
                {
                    errors.Add($"HTML ist nicht erlaubt: {line}");
                }

                if (line.StartsWith('#'))
                {
                    errors.Add($"Überschriften sind nicht erlaubt, besser **fett**: {line}");
                }
            }
        }

        return errors;
    }

    private static IEnumerable<string> Blocks(string markdown) =>
        markdown.ReplaceLineEndings("\n")
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(b => string.Join('\n', b.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0)))
            .Where(b => b.Length > 0);

    private static string Kind(string line) =>
        line.StartsWith("- ", StringComparison.Ordinal) ? "list"
        : NumberedItem().IsMatch(line) ? "numbered"
        : line.StartsWith("> ", StringComparison.Ordinal) ? "note"
        : line.StartsWith('|') ? "table"
        : "text";

    private static bool IsSeparator(string line) => Cells(line).All(c => c.Length >= 3 && c.All(ch => ch is '-' or ':'));

    private static string[] Cells(string line) => line.Trim('|').Split('|').Select(c => c.Trim()).ToArray();

    private static int CountOf(string text, string part)
    {
        var count = 0;
        for (var index = text.IndexOf(part, StringComparison.Ordinal); index >= 0; index = text.IndexOf(part, index + part.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    private static string Inline(string text)
    {
        var encoded = WebUtility.HtmlEncode(text);
        encoded = Code().Replace(encoded, "<code>$1</code>");
        return Bold().Replace(encoded, "<strong>$1</strong>");
    }

    [GeneratedRegex(@"^\d+\. ")]
    private static partial Regex NumberedItem();

    [GeneratedRegex(@"\*\*(.+?)\*\*")]
    private static partial Regex Bold();

    [GeneratedRegex("`(.+?)`")]
    private static partial Regex Code();

    [GeneratedRegex(@"<[a-zA-Z/!]")]
    private static partial Regex HtmlTag();
}
