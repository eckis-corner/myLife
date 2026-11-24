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
/// Setzt Text auf A4-Seiten: Überschriften, Absätze mit Umbruch, Zeilen „Bezeichnung – Wert“. Bewusst einfach:
/// nur die Standardschriften, kein Bild, keine Tabelle über Seiten. Seiten entstehen beim Schreiben; die Fußzeile
/// mit Seitenzahlen setzt <see cref="PdfWriter"/>.
/// </summary>
public sealed class PdfLayout
{
    public const float PageWidth = 595.28f;
    public const float PageHeight = 841.89f;
    public const float Margin = 56f;
    public const float BottomMargin = 60f;
    public const float LabelWidth = 165f;

    private readonly List<PdfPage> _pages = [];
    private PdfPage? _page;
    private float _y;

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

    private static float ContentWidth => PageWidth - (2 * Margin);

    public void NewPage()
    {
        _page = new PdfPage();
        _pages.Add(_page);
        _y = PageHeight - Margin;
    }

    public void Title(string text) => Block(text, 24, bold: true, before: 0, after: 10);

    public void Heading(string text) => Block(text, 16, bold: true, before: 6, after: 6, keepWithNext: 80);

    public void Subheading(string text) => Block(text, 12, bold: true, before: 10, after: 4, keepWithNext: 50);

    public void Paragraph(string text, float size = 10, bool bold = false, bool muted = false) =>
        Block(text, size, bold, before: 0, after: 4, muted: muted);

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
                Text(Margin, _y, labelLines[i], size - 0.5f, bold: true, muted: true);
            }

            if (i < valueLines.Count)
            {
                Text(Margin + LabelWidth, _y, valueLines[i], size, bold: false, muted: false);
            }
        }

        _y -= 2;
    }

    /// <summary>Eintrag im Inhaltsverzeichnis mit Seitenzahl rechts.</summary>
    public void TocEntry(string text, int page, float size = 11)
    {
        var lineHeight = size * 1.6f;
        Reserve(lineHeight);
        _y -= lineHeight;
        var number = page.ToString(CultureInfo.InvariantCulture);
        Text(Margin, _y, text, size, false, false);
        Text(PageWidth - Margin - PdfFonts.Measure(number, size, false), _y, number, size, false, false);
    }

    public void Rule()
    {
        Reserve(10);
        _y -= 5;
        _page!.Content.Append(CultureInfo.InvariantCulture, $"0.8 G 0.5 w {Margin:0.##} {_y:0.##} m {PageWidth - Margin:0.##} {_y:0.##} l S 0 G\n");
        _y -= 5;
    }

    public void Space(float points)
    {
        EnsurePage();
        _y -= points;
    }

    /// <summary>Bricht einen Text auf die Breite um; zu lange Wörter werden zeichenweise getrennt.</summary>
    public static List<string> Wrap(string text, float size, bool bold, float width)
    {
        var lines = new List<string>();
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
                    lines.Add(line.ToString());
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

                    lines.Add(rest[..take]);
                    rest = rest[take..];
                }

                line.Append(rest);
            }

            lines.Add(line.ToString());
        }

        return lines;
    }

    private void Block(string text, float size, bool bold, float before, float after, bool muted = false, float keepWithNext = 0)
    {
        var lines = Wrap(text, size, bold, ContentWidth);
        var lineHeight = size * 1.3f;
        EnsurePage();
        if (_y < PageHeight - Margin)
        {
            _y -= before;
        }

        Reserve((lineHeight * Math.Min(lines.Count, 2)) + keepWithNext);
        foreach (var line in lines)
        {
            Reserve(lineHeight);
            _y -= lineHeight;
            Text(Margin, _y, line, size, bold, muted);
        }

        _y -= after;
    }

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

    private void Text(float x, float y, string text, float size, bool bold, bool muted)
    {
        if (text.Length == 0)
        {
            return;
        }

        var content = _page!.Content;
        if (muted)
        {
            content.Append("0.35 g ");
        }

        content.Append(CultureInfo.InvariantCulture, $"BT /{(bold ? "F2" : "F1")} {size:0.##} Tf {x:0.##} {y:0.##} Td <{Convert.ToHexString(PdfFonts.Encode(text))}> Tj ET");
        content.Append(muted ? " 0 g\n" : "\n");
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
        const float size = 8;
        var width = PdfFonts.Measure(footer, size, false);
        var x = (PdfLayout.PageWidth - width) / 2;
        return string.Create(CultureInfo.InvariantCulture, $"0.45 g BT /F1 {size} Tf {x:0.##} 30 Td <{Convert.ToHexString(PdfFonts.Encode(footer))}> Tj ET 0 g\n");
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
