using System.Globalization;
using System.Text;

namespace MyLife.Client.Export.Pdf;

/// <summary>
/// Das myLife-Logo (docs/design/logo/icon.svg) als PDF-Vektorgrafik: Ordner mit Benutzersymbol auf abgerundetem Quadrat,
/// daneben der Schriftzug „myLife“ in Helvetica. Kein eingebettetes Bild, daher keine zusätzlichen Objekte und nichts,
/// was die Verschlüsselung berücksichtigen müsste. Der Graphit-Verlauf des SVG wird hier zur einfarbigen Fläche.
/// </summary>
internal static class PdfLogo
{
    private const string Graphite = "0.122 0.157 0.188";
    private const string SteelBlue = "0.345 0.471 0.58";
    private const string Paper = "0.929 0.949 0.965";
    private const string Orange = "0.98 0.647 0";
    private const string Amber = "0.525 0.333 0";

    // Kreisnäherung mit kubischen Bézierkurven.
    private const float Kappa = 0.5523f;

    /// <summary>Breite des Schriftzugs „myLife“ in der angegebenen Größe.</summary>
    public static float WordmarkWidth(float size) => PdfFonts.Measure("my", size, false) + PdfFonts.Measure("Life", size, true);

    /// <summary>Zeichnet das Icon mit der linken unteren Ecke bei (x, y) und der Kantenlänge size.</summary>
    public static string Icon(float x, float y, float size)
    {
        var s = new StringBuilder();

        // In SVG-Koordinaten zeichnen: 512er-Raster, y nach unten.
        s.Append(CultureInfo.InvariantCulture, $"q {size / 512:0.#####} 0 0 {-size / 512:0.#####} {x:0.##} {y + size:0.##} cm\n");
        s.Append(Graphite).Append(" rg ");
        RoundedRect(s, 0, 0, 512, 512, 112);
        s.Append("f\n");

        // Motiv wie im SVG um 18 % vergrößert: translate(256 262) scale(1.18) translate(-256 -261).
        s.Append("1.18 0 0 1.18 -46.08 -45.98 cm\n");
        s.Append(SteelBlue).Append(" rg 96 196 m 96 156 l 96 138.67 104.67 130 122 130 c 204 130 l 212 130 218.67 135.33 224 138 c ")
            .Append("248 162 l 390 162 l 407.33 162 416 170.67 416 188 c 416 196 l h f\n");
        s.Append(Paper).Append(" rg ");
        RoundedRect(s, 96, 186, 320, 206, 26);
        s.Append("f\n");
        s.Append(Orange).Append(" rg ");
        Circle(s, 256, 262, 27);
        s.Append("f\n");
        s.Append("202 344 m 202 336 l 202 310.67 220 298 256 298 c 292 298 310 310.67 310 336 c 310 344 l h f\n");
        s.Append("Q 0 g\n");
        return s.ToString();
    }

    /// <summary>Schriftzug „myLife“: „my“ normal in Bernstein (Orange-Ton mit genug Kontrast auf Weiß), „Life“ fett in Graphit.</summary>
    public static string Wordmark(float x, float baseline, float size)
    {
        var life = x + PdfFonts.Measure("my", size, false);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Amber} rg BT /F1 {size:0.##} Tf {x:0.##} {baseline:0.##} Td <{Convert.ToHexString(PdfFonts.Encode("my"))}> Tj ET " +
            $"{PdfLayout.AccentColor} rg BT /F2 {size:0.##} Tf {life:0.##} {baseline:0.##} Td <{Convert.ToHexString(PdfFonts.Encode("Life"))}> Tj ET 0 g\n");
    }

    private static void RoundedRect(StringBuilder s, float x, float y, float w, float h, float r)
    {
        var k = r * Kappa;
        s.Append(CultureInfo.InvariantCulture, $"{x + r:0.##} {y:0.##} m {x + w - r:0.##} {y:0.##} l ");
        s.Append(CultureInfo.InvariantCulture, $"{x + w - r + k:0.##} {y:0.##} {x + w:0.##} {y + r - k:0.##} {x + w:0.##} {y + r:0.##} c ");
        s.Append(CultureInfo.InvariantCulture, $"{x + w:0.##} {y + h - r:0.##} l ");
        s.Append(CultureInfo.InvariantCulture, $"{x + w:0.##} {y + h - r + k:0.##} {x + w - r + k:0.##} {y + h:0.##} {x + w - r:0.##} {y + h:0.##} c ");
        s.Append(CultureInfo.InvariantCulture, $"{x + r:0.##} {y + h:0.##} l ");
        s.Append(CultureInfo.InvariantCulture, $"{x + r - k:0.##} {y + h:0.##} {x:0.##} {y + h - r + k:0.##} {x:0.##} {y + h - r:0.##} c ");
        s.Append(CultureInfo.InvariantCulture, $"{x:0.##} {y + r:0.##} l ");
        s.Append(CultureInfo.InvariantCulture, $"{x:0.##} {y + r - k:0.##} {x + r - k:0.##} {y:0.##} {x + r:0.##} {y:0.##} c h ");
    }

    private static void Circle(StringBuilder s, float cx, float cy, float r)
    {
        var k = r * Kappa;
        s.Append(CultureInfo.InvariantCulture, $"{cx + r:0.##} {cy:0.##} m ");
        s.Append(CultureInfo.InvariantCulture, $"{cx + r:0.##} {cy + k:0.##} {cx + k:0.##} {cy + r:0.##} {cx:0.##} {cy + r:0.##} c ");
        s.Append(CultureInfo.InvariantCulture, $"{cx - k:0.##} {cy + r:0.##} {cx - r:0.##} {cy + k:0.##} {cx - r:0.##} {cy:0.##} c ");
        s.Append(CultureInfo.InvariantCulture, $"{cx - r:0.##} {cy - k:0.##} {cx - k:0.##} {cy - r:0.##} {cx:0.##} {cy - r:0.##} c ");
        s.Append(CultureInfo.InvariantCulture, $"{cx + k:0.##} {cy - r:0.##} {cx + r:0.##} {cy - k:0.##} {cx + r:0.##} {cy:0.##} c h ");
    }
}
