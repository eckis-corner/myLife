using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace MyLife.Client.Export.Pdf;

/// <summary>Eine fertig gesetzte Seite: PDF-Operatoren ohne Fußzeile.</summary>
public sealed class PdfPage
{
    internal StringBuilder Content { get; } = new();
}

/// <summary>
/// Setzt Text auf A4-Seiten im Design der Vorlage „docs/design/Vorlage - eckis-corner.docx“ (Entscheidung 68): Ränder, Titel
/// zwischen zwei Linien, Untertitel, Überschriften in #355269, Fließtext 11 pt im Blocksatz. Statt Tahoma steht die
/// Standardschrift Helvetica (Tahoma darf nicht eingebettet weitergegeben werden). Bewusst einfach: kein eingebettetes Bild
/// (das Logo ist Vektorgrafik, <see cref="PdfLogo"/>), keine Tabelle über Seiten. Seiten entstehen beim Schreiben; die Fußzeile mit Seitenzahlen setzt <see cref="PdfWriter"/>.
/// </summary>
public sealed class PdfLayout
{
    public const float PageWidth = 595.28f;
    public const float PageHeight = 841.89f;

    // Ränder der Vorlage (Twips / 20): links 1393, rechts 871, oben 1006; unten bleibt Platz für die Fußzeile.
    public const float LeftMargin = 69.65f;
    public const float RightMargin = 43.55f;
    public const float TopMargin = 50.3f;
    public const float BottomMargin = 85f;
    public const float FooterBaseline = 45f;
    public const float LabelWidth = 165f;
    public const float BodySize = 11f;

    /// <summary>Akzentfarbe der Vorlage #355269 als PDF-RGB.</summary>
    internal const string AccentColor = "0.208 0.322 0.412";

    private readonly List<PdfPage> _pages = [];
    private PdfPage? _page;
    private float _y;

    private enum Tone
    {
        Normal,
        Muted,
        Accent,
    }

    public IReadOnlyList<PdfPage> Pages => _pages;

    /// <summary>Index der aktuellen Seite (0-basiert); legt eine an, falls noch keine da ist.</summary>
    public int CurrentPageIndex
    {
        get
        {
            EnsurePage();
            return _pages.Count - 1;
        }
    }

    public static float ContentWidth => PageWidth - LeftMargin - RightMargin;

    public void NewPage()
    {
        _page = new PdfPage();
        _pages.Add(_page);
        _y = PageHeight - TopMargin;
    }

    /// <summary>Logo mit Schriftzug, zentriert (Deckblatt).</summary>
    public void Logo(float iconSize = 40, float textSize = 26)
    {
        Reserve(iconSize + 16);
        var gap = iconSize * 0.3f;
        var x = LeftMargin + ((ContentWidth - (iconSize + gap + PdfLogo.WordmarkWidth(textSize))) / 2);
        var bottom = _y - iconSize;

        // Grundlinie so, dass die Versalhöhe von Helvetica (0,718) mittig zum Icon steht.
        var baseline = bottom + ((iconSize - (textSize * 0.718f)) / 2);
        _page!.Content.Append(PdfLogo.Icon(x, bottom, iconSize)).Append(PdfLogo.Wordmark(x + iconSize + gap, baseline, textSize));
        _y = bottom - 16;
    }

    /// <summary>Titel der Vorlage: 28 pt fett, zentriert, zwischen zwei feinen Linien.</summary>
    public void Title(string text)
    {
        const float size = 28;
        const float indent = 8.5f;
        var lines = Wrap(text, size, true, ContentWidth - (2 * indent) - 8);
        var lineHeight = size * 1.5f;
        EnsurePage();
        if (_y < PageHeight - TopMargin)
        {
            _y -= 12;
        }

        Reserve((lineHeight * Math.Min(lines.Count, 2)) + 12);
        HorizontalLine(LeftMargin + indent, PageWidth - RightMargin - indent, _y, 0.25f, "0 G");
        _y -= 4;
        foreach (var line in lines)
        {
            Reserve(lineHeight);
            _y -= lineHeight;
            Text(Centered(line, size, true), _y + ((lineHeight - (size * 0.7f)) / 2), line, size, true, Tone.Accent);
        }

        HorizontalLine(LeftMargin + indent, PageWidth - RightMargin - indent, _y, 0.25f, "0 G");
        _y -= 6;
    }

    /// <summary>Untertitel der Vorlage: 18 pt, zentriert, Akzentfarbe.</summary>
    public void Subtitle(string text, float size = 18) => Block(text, size, bold: false, before: 3, after: 6, Tone.Accent, centered: true);

    /// <summary>Überschrift 1 der Vorlage (18 pt fett).</summary>
    public void Heading(string text) => Block(text, 18, bold: true, before: 12, after: 6, Tone.Accent, keepWithNext: 80);

    /// <summary>Überschrift 2 der Vorlage (16 pt fett).</summary>
    public void Section(string text) => Block(text, 16, bold: true, before: 10, after: 6, Tone.Accent, keepWithNext: 60);

    /// <summary>Überschrift 3 der Vorlage (14 pt fett).</summary>
    public void Subheading(string text) => Block(text, 14, bold: true, before: 7, after: 4, Tone.Accent, keepWithNext: 50);

    /// <summary>Textkörper der Vorlage: 11 pt, Zeilenabstand 1,2, Blocksatz, 14 pt Abstand danach.</summary>
    public void Body(string text, bool bold = false) => Block(text, BodySize, bold, before: 0, after: 14, Tone.Normal, justify: true, lineFactor: 1.2f);

    public void Paragraph(string text, float size = BodySize, bool bold = false, bool muted = false) =>
        Block(text, size, bold, before: 0, after: 4, muted ? Tone.Muted : Tone.Normal);

    /// <summary>Zeile „Bezeichnung | Wert“; der Wert bricht in seiner Spalte um.</summary>
    public void Row(string label, string value, float size = 10)
    {
        var labelLines = Wrap(label, size, true, LabelWidth - 8);
        var valueLines = value.ReplaceLineEndings("\n").Split('\n').SelectMany(l => Wrap(l, size, false, ContentWidth - LabelWidth)).ToList();
        var lineHeight = size * 1.35f;
        var lines = Math.Max(labelLines.Count, valueLines.Count);
        for (var i = 0; i < lines; i++)
        {
            Reserve(lineHeight);
            _y -= lineHeight;
            if (i < labelLines.Count)
            {
                Text(LeftMargin, _y, labelLines[i], size - 0.5f, true, Tone.Muted);
            }

            if (i < valueLines.Count)
            {
                Text(LeftMargin + LabelWidth, _y, valueLines[i], size, false, Tone.Normal);
            }
        }

        _y -= 2;
    }

    /// <summary>Eintrag im Inhaltsverzeichnis mit Seitenzahl rechts.</summary>
    public void TocEntry(string text, int page, float size = BodySize)
    {
        var lineHeight = size * 1.6f;
        Reserve(lineHeight);
        _y -= lineHeight;
        var number = page.ToString(CultureInfo.InvariantCulture);
        Text(LeftMargin, _y, text, size, false, Tone.Normal);
        Text(PageWidth - RightMargin - PdfFonts.Measure(number, size, false), _y, number, size, false, Tone.Normal);
    }

    public void Rule()
    {
        Reserve(10);
        _y -= 5;
        HorizontalLine(LeftMargin, PageWidth - RightMargin, _y, 0.5f, "0.8 G");
        _y -= 5;
    }

    public void Space(float points)
    {
        EnsurePage();
        _y -= points;
    }

    /// <summary>Bricht einen Text auf die Breite um; zu lange Wörter werden zeichenweise getrennt.</summary>
    public static List<string> Wrap(string text, float size, bool bold, float width) =>
        [.. WrapParagraphs(text, size, bold, width).Select(l => l.Text)];

    /// <summary>Wie <see cref="Wrap"/>, markiert aber die letzte Zeile jedes Absatzes (für den Blocksatz).</summary>
    private static List<(string Text, bool EndsParagraph)> WrapParagraphs(string text, float size, bool bold, float width)
    {
        var lines = new List<(string, bool)>();
        foreach (var paragraph in (text ?? string.Empty).ReplaceLineEndings("\n").Split('\n'))
        {
            var line = new StringBuilder();
            foreach (var word in paragraph.Split(' '))
            {
                var candidate = line.Length == 0 ? word : line + " " + word;
                if (PdfFonts.Measure(candidate, size, bold) <= width)
                {
                    line.Clear().Append(candidate);
                    continue;
                }

                if (line.Length > 0)
                {
                    lines.Add((line.ToString(), false));
                    line.Clear();
                }

                var rest = word;
                while (PdfFonts.Measure(rest, size, bold) > width && rest.Length > 1)
                {
                    var take = rest.Length - 1;
                    while (take > 1 && PdfFonts.Measure(rest[..take], size, bold) > width)
                    {
                        take--;
                    }

                    lines.Add((rest[..take], false));
                    rest = rest[take..];
                }

                line.Append(rest);
            }

            lines.Add((line.ToString(), true));
        }

        return lines;
    }

    private void Block(string text, float size, bool bold, float before, float after, Tone tone, float keepWithNext = 0,
        bool centered = false, bool justify = false, float lineFactor = 1.3f)
    {
        var lines = WrapParagraphs(text, size, bold, ContentWidth);
        var lineHeight = size * lineFactor;
        EnsurePage();
        if (_y < PageHeight - TopMargin)
        {
            _y -= before;
        }

        Reserve((lineHeight * Math.Min(lines.Count, 2)) + keepWithNext);
        foreach (var (line, endsParagraph) in lines)
        {
            Reserve(lineHeight);
            _y -= lineHeight;
            var x = centered ? Centered(line, size, bold) : LeftMargin;
            var spacing = justify && !endsParagraph ? WordSpacing(line, size, bold) : 0;
            Text(x, _y, line, size, bold, tone, spacing);
        }

        _y -= after;
    }

    /// <summary>Zusätzlicher Wortabstand (Tw), damit die Zeile die volle Breite füllt.</summary>
    private static float WordSpacing(string line, float size, bool bold)
    {
        var spaces = line.Count(c => c == ' ');
        return spaces == 0 ? 0 : (ContentWidth - PdfFonts.Measure(line, size, bold)) / spaces;
    }

    private static float Centered(string line, float size, bool bold) => LeftMargin + ((ContentWidth - PdfFonts.Measure(line, size, bold)) / 2);

    private void EnsurePage()
    {
        if (_page is null)
        {
            NewPage();
        }
    }

    /// <summary>Neue Seite, wenn nicht mehr genug Platz ist.</summary>
    private void Reserve(float height)
    {
        EnsurePage();
        if (_y - height < BottomMargin)
        {
            NewPage();
        }
    }

    private void HorizontalLine(float from, float to, float y, float width, string stroke) =>
        _page!.Content.Append(CultureInfo.InvariantCulture, $"{stroke} {width:0.##} w {from:0.##} {y:0.##} m {to:0.##} {y:0.##} l S 0 G\n");

    private void Text(float x, float y, string text, float size, bool bold, Tone tone, float wordSpacing = 0)
    {
        if (text.Length == 0)
        {
            return;
        }

        var content = _page!.Content;
        content.Append(tone switch
        {
            Tone.Muted => "0.35 g ",
            Tone.Accent => AccentColor + " rg ",
            _ => string.Empty,
        });

        // Tw gehört zum Grafikzustand und gilt über ET hinaus: nach der Zeile zurücksetzen.
        var spacing = wordSpacing > 0 ? string.Create(CultureInfo.InvariantCulture, $"{wordSpacing:0.###} Tw ") : string.Empty;
        content.Append(CultureInfo.InvariantCulture, $"BT /{(bold ? "F2" : "F1")} {size:0.##} Tf {spacing}{x:0.##} {y:0.##} Td <{Convert.ToHexString(PdfFonts.Encode(text))}> Tj ET");
        content.Append(wordSpacing > 0 ? " 0 Tw" : string.Empty);
        content.Append(tone == Tone.Normal ? "\n" : " 0 g\n");
    }
}

/// <summary>
/// Schreibt die Seiten als PDF 1.7 (mit Adobe-Erweiterung 8 für AES-256, sonst kompatibel zu PDF 2.0):
/// Standardschriften, komprimierte Inhalte, optional mit Passwort (<see cref="PdfEncryptor"/>).
/// </summary>
public static class PdfWriter
{
    /// <param name="footer">Fußzeile mit Platzhaltern {0} = Seite, {1} = Seitenzahl gesamt.</param>
    public static async Task<byte[]> WriteAsync(IReadOnlyList<PdfPage> pages, string title, string footer, PdfEncryptor? encryptor, DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(pages);
        var objects = new List<byte[]>();
        int Reserve()
        {
            objects.Add([]);
            return objects.Count;
        }

        void Set(int number, string body) => objects[number - 1] = Encoding.ASCII.GetBytes(body);

        async Task<string> StringAsync(string value)
        {
            var bytes = Encoding.BigEndianUnicode.GetPreamble().Concat(Encoding.BigEndianUnicode.GetBytes(value)).ToArray();
            return "<" + Convert.ToHexString(encryptor is null ? bytes : await encryptor.EncryptAsync(bytes)) + ">";
        }

        var catalog = Reserve();
        var pagesNumber = Reserve();
        var regular = Reserve();
        var bold = Reserve();
        var info = Reserve();
        var encrypt = encryptor is null ? 0 : Reserve();

        var kids = new List<int>();
        for (var i = 0; i < pages.Count; i++)
        {
            var text = pages[i].Content.ToString() + FooterOperators(string.Format(CultureInfo.InvariantCulture, footer, i + 1, pages.Count));
            var compressed = Compress(Encoding.ASCII.GetBytes(text));
            var data = encryptor is null ? compressed : await encryptor.EncryptAsync(compressed);
            var content = Reserve();
            objects[content - 1] =
            [
                .. Encoding.ASCII.GetBytes(string.Create(CultureInfo.InvariantCulture, $"<< /Length {data.Length} /Filter /FlateDecode >>\nstream\n")),
                .. data,
                .. Encoding.ASCII.GetBytes("\nendstream"),
            ];
            var page = Reserve();
            Set(page, string.Create(
                CultureInfo.InvariantCulture,
                $"<< /Type /Page /Parent {pagesNumber} 0 R /MediaBox [0 0 {PdfLayout.PageWidth:0.##} {PdfLayout.PageHeight:0.##}] " +
                $"/Resources << /Font << /F1 {regular} 0 R /F2 {bold} 0 R >> >> /Contents {content} 0 R >>"));
            kids.Add(page);
        }

        Set(catalog, string.Create(
            CultureInfo.InvariantCulture,
            $"<< /Type /Catalog /Pages {pagesNumber} 0 R /Extensions << /ADBE << /BaseVersion /1.7 /ExtensionLevel 8 >> >> >>"));
        Set(pagesNumber, string.Create(CultureInfo.InvariantCulture, $"<< /Type /Pages /Kids [{string.Join(' ', kids.Select(k => $"{k} 0 R"))}] /Count {kids.Count} >>"));
        Set(regular, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        Set(bold, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");
        Set(info, $"<< /Title {await StringAsync(title)} /Producer {await StringAsync("myLife")} " +
                  $"/CreationDate {await StringAsync(createdAt.UtcDateTime.ToString("'D:'yyyyMMddHHmmss'Z'", CultureInfo.InvariantCulture))} >>");
        if (encryptor is not null)
        {
            Set(encrypt, encryptor.Dictionary());
        }

        var id = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        using var output = new MemoryStream();
        Write(output, "%PDF-1.7\n%âãÏÓ\n");
        var offsets = new long[objects.Count];
        for (var i = 0; i < objects.Count; i++)
        {
            offsets[i] = output.Position;
            Write(output, string.Create(CultureInfo.InvariantCulture, $"{i + 1} 0 obj\n"));
            output.Write(objects[i]);
            Write(output, "\nendobj\n");
        }

        var xref = output.Position;
        var table = new StringBuilder();
        table.Append(CultureInfo.InvariantCulture, $"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            table.Append(CultureInfo.InvariantCulture, $"{offset:0000000000} 00000 n \n");
        }

        table.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {objects.Count + 1} /Root {catalog} 0 R /Info {info} 0 R /ID [<{id}> <{id}>]");
        if (encryptor is not null)
        {
            table.Append(CultureInfo.InvariantCulture, $" /Encrypt {encrypt} 0 R");
        }

        table.Append(CultureInfo.InvariantCulture, $" >>\nstartxref\n{xref}\n%%EOF\n");
        Write(output, table.ToString());
        return output.ToArray();
    }

    private static string FooterOperators(string footer)
    {
        // Wie die Fußzeile der Vorlage: linksbündig am Satzspiegel, darüber eine feine Linie in der Akzentfarbe;
        // rechts das Logo als kleines Icon.
        const float size = 8;
        const float icon = 12;
        var left = PdfLayout.LeftMargin;
        var right = PdfLayout.PageWidth - PdfLayout.RightMargin;
        var line = PdfLayout.FooterBaseline + 12;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{PdfLayout.AccentColor} RG 0.5 w {left:0.##} {line:0.##} m {right:0.##} {line:0.##} l S 0 G " +
            $"0.45 g BT /F1 {size} Tf {left:0.##} {PdfLayout.FooterBaseline:0.##} Td <{Convert.ToHexString(PdfFonts.Encode(footer))}> Tj ET 0 g\n") +
            PdfLogo.Icon(right - icon, PdfLayout.FooterBaseline - 3, icon);
    }

    private static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(data);
        }

        return output.ToArray();
    }

    // Der Kopf enthält Bytes über 127, damit Programme die Datei als binär erkennen: Latin-1 schreibt sie unverändert.
    private static void Write(Stream stream, string text) => stream.Write(Encoding.Latin1.GetBytes(text));
}
