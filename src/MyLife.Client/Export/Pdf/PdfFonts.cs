using System.Text;

namespace MyLife.Client.Export.Pdf;

/// <summary>
/// Die Standardschriften Helvetica und Helvetica-Bold (ohne Einbettung, in jedem PDF-Betrachter vorhanden) mit
/// WinAnsiEncoding. Breiten in Tausendstel der Schriftgröße nach den Adobe-Metriken (AFM) für ASCII; andere
/// Zeichen über ihren Grundbuchstaben (Ä wie A) oder einen Näherungswert.
/// </summary>
public static class PdfFonts
{
    // ASCII 32–126.
    private static readonly short[] Regular =
    [
        278, 278, 355, 556, 556, 889, 667, 191, 333, 333, 389, 584, 278, 333, 278, 278,
        556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 278, 278, 584, 584, 584, 556,
        1015, 667, 667, 722, 722, 667, 611, 778, 722, 278, 500, 667, 556, 833, 722, 778,
        667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 278, 278, 278, 469, 556,
        333, 556, 556, 500, 556, 556, 278, 556, 556, 222, 222, 500, 222, 833, 556, 556,
        556, 556, 333, 500, 278, 556, 500, 722, 500, 500, 500, 334, 260, 334, 584,
    ];

    private static readonly short[] Bold =
    [
        278, 333, 474, 556, 556, 889, 722, 238, 333, 333, 389, 584, 278, 333, 278, 278,
        556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 333, 333, 584, 584, 584, 611,
        975, 722, 722, 722, 722, 667, 611, 778, 722, 278, 556, 722, 611, 833, 722, 778,
        667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 333, 278, 333, 584, 556,
        333, 556, 611, 556, 611, 556, 333, 611, 611, 278, 278, 556, 278, 889, 611, 611,
        611, 611, 389, 556, 333, 611, 556, 778, 556, 556, 500, 389, 280, 389, 584,
    ];

    // Unicode → WinAnsi (0x80–0x9F); 0xA0–0xFF stimmen mit Latin-1 überein.
    private static readonly Dictionary<char, byte> WinAnsiSpecials = new()
    {
        ['€'] = 0x80, ['‚'] = 0x82, ['ƒ'] = 0x83, ['„'] = 0x84, ['…'] = 0x85, ['†'] = 0x86, ['‡'] = 0x87, ['ˆ'] = 0x88,
        ['‰'] = 0x89, ['Š'] = 0x8A, ['‹'] = 0x8B, ['Œ'] = 0x8C, ['Ž'] = 0x8E, ['‘'] = 0x91, ['’'] = 0x92, ['“'] = 0x93,
        ['”'] = 0x94, ['•'] = 0x95, ['–'] = 0x96, ['—'] = 0x97, ['˜'] = 0x98, ['™'] = 0x99, ['š'] = 0x9A, ['›'] = 0x9B,
        ['œ'] = 0x9C, ['ž'] = 0x9E, ['Ÿ'] = 0x9F,
    };

    private static readonly Dictionary<char, short> SpecialWidths = new()
    {
        ['ß'] = 611, ['€'] = 556, ['„'] = 333, ['“'] = 333, ['”'] = 333, ['‚'] = 222, ['‘'] = 222, ['’'] = 222,
        ['–'] = 556, ['—'] = 1000, ['…'] = 1000, ['•'] = 350, ['°'] = 400, ['§'] = 556, ['©'] = 737, ['®'] = 737,
        ['×'] = 584, ['·'] = 278, ['«'] = 556, ['»'] = 556, ['µ'] = 556, ['²'] = 333, ['³'] = 333, ['¹'] = 333,
        ['½'] = 834, ['¼'] = 834, ['¾'] = 834, [' '] = 278, ['¿'] = 611, ['¡'] = 333, ['æ'] = 889, ['Æ'] = 1000,
        ['ø'] = 611, ['Ø'] = 778, ['™'] = 1000, ['‰'] = 1000,
    };

    /// <summary>Breite eines Texts in Punkt.</summary>
    public static float Measure(string text, float size, bool bold)
    {
        ArgumentNullException.ThrowIfNull(text);
        var units = 0;
        foreach (var c in text)
        {
            units += Width(c, bold);
        }

        return units * size / 1000f;
    }

    public static int Width(char c, bool bold)
    {
        var table = bold ? Bold : Regular;
        if (c is >= ' ' and <= '~')
        {
            return table[c - ' '];
        }

        if (SpecialWidths.TryGetValue(c, out var special))
        {
            return bold && special < 1000 ? (int)(special * 1.05) : special;
        }

        // Buchstaben mit Akzent wie den Grundbuchstaben messen (ä wie a).
        var decomposed = c.ToString().Normalize(NormalizationForm.FormD);
        if (decomposed.Length > 0 && decomposed[0] is >= ' ' and <= '~')
        {
            return table[decomposed[0] - ' '];
        }

        return 556;
    }

    /// <summary>Text als WinAnsi-Bytes; nicht darstellbare Zeichen werden zu „?“, Tabulatoren zu Leerzeichen.</summary>
    public static byte[] Encode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var bytes = new byte[text.Length];
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            bytes[i] = c switch
            {
                '\t' => (byte)' ',
                >= ' ' and <= '~' => (byte)c,
                >= ' ' and <= 'ÿ' => (byte)c,
                _ when WinAnsiSpecials.TryGetValue(c, out var mapped) => mapped,
                _ => (byte)'?',
            };
        }

        return bytes;
    }
}
