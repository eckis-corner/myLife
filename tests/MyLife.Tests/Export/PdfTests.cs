using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MyLife.Client.Export.Pdf;

namespace MyLife.Tests.Export;

/// <summary>Krypto für den PDF-Schutz mit .NET statt Web Crypto (gleiches Verhalten: AES-CBC mit PKCS#7).</summary>
internal sealed class DotNetPdfCrypto : IPdfCrypto
{
    public Task<byte[]> DigestAsync(int bits, byte[] data) => Task.FromResult(bits switch
    {
        256 => SHA256.HashData(data),
        384 => SHA384.HashData(data),
        512 => SHA512.HashData(data),
        _ => throw new ArgumentOutOfRangeException(nameof(bits)),
    });

    public Task<byte[]> AesCbcEncryptAsync(byte[] key, byte[] iv, byte[] data)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        return Task.FromResult(aes.EncryptCbc(data, iv, PaddingMode.PKCS7));
    }
}

/// <summary>Liest die von <see cref="PdfWriter"/> erzeugten Dateien so, wie ein Betrachter es täte (Teil der Prüfung).</summary>
internal static partial class PdfReader
{
    /// <summary>Algorithmus 2.A (R6): Passwort prüfen und den Dateischlüssel aus UE gewinnen; prüft auch /Perms.</summary>
    public static async Task<byte[]?> OpenAsync(byte[] pdf, string password)
    {
        var text = Encoding.Latin1.GetString(pdf);
        var u = Hex(Field(text, "U"));
        var ue = Hex(Field(text, "UE"));
        var perms = Hex(Field(text, "Perms"));
        var crypto = new DotNetPdfCrypto();
        var bytes = PdfEncryptor.PasswordBytes(password);
        var check = await PdfEncryptor.HashAsync(crypto, bytes, u[32..40], []);
        if (!check.AsSpan().SequenceEqual(u.AsSpan(0, 32)))
        {
            return null;
        }

        var intermediate = await PdfEncryptor.HashAsync(crypto, bytes, u[40..48], []);
        using var aes = Aes.Create();
        aes.Key = intermediate;
        var fileKey = aes.DecryptCbc(ue, new byte[16], PaddingMode.None);
        aes.Key = fileKey;
        var permsPlain = aes.DecryptEcb(perms, PaddingMode.None);
        Assert.Equal("adb"u8.ToArray(), permsPlain[9..12]);
        Assert.Equal(PdfEncryptor.Permissions, BitConverter.ToInt32(permsPlain, 0));
        return fileKey;
    }

    /// <summary>Inhalt aller Streams, entschlüsselt (falls Schlüssel) und entpackt.</summary>
    public static List<string> Streams(byte[] pdf, byte[]? fileKey)
    {
        var text = Encoding.Latin1.GetString(pdf);
        var result = new List<string>();
        foreach (Match match in StreamHeader().Matches(text))
        {
            var length = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var data = pdf.AsSpan(match.Index + match.Length, length).ToArray();
            if (fileKey is not null)
            {
                using var aes = Aes.Create();
                aes.Key = fileKey;
                data = aes.DecryptCbc(data[16..], data[..16], PaddingMode.PKCS7);
            }

            using var zlib = new ZLibStream(new MemoryStream(data), CompressionMode.Decompress);
            using var output = new MemoryStream();
            zlib.CopyTo(output);
            result.Add(Encoding.Latin1.GetString(output.ToArray()));
        }

        return result;
    }

    /// <summary>Alle Texte aus den Hex-Strings der Inhalte (WinAnsi ≈ Latin-1 für die geprüften Zeichen).</summary>
    public static string ShownText(IEnumerable<string> streams) =>
        string.Join("\n", streams.SelectMany(s => HexText().Matches(s).Select(m => Encoding.Latin1.GetString(Convert.FromHexString(m.Groups[1].Value)))));

    public static void AssertXrefIsConsistent(byte[] pdf)
    {
        var text = Encoding.Latin1.GetString(pdf);
        var startxref = int.Parse(StartXref().Match(text).Groups[1].Value, CultureInfo.InvariantCulture);
        Assert.StartsWith("xref", text[startxref..], StringComparison.Ordinal);
        var entries = XrefEntry().Matches(text[startxref..]).Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).ToList();
        for (var i = 0; i < entries.Count; i++)
        {
            Assert.StartsWith($"{i + 1} 0 obj", text[entries[i]..], StringComparison.Ordinal);
        }
    }

    private static string Field(string text, string name) => Regex.Match(text, $"/{name} <([0-9A-F]+)>").Groups[1].Value;

    private static byte[] Hex(string value) => Convert.FromHexString(value);

    [GeneratedRegex(@"/Length (\d+) /Filter /FlateDecode >>\nstream\n")]
    private static partial Regex StreamHeader();

    [GeneratedRegex("<([0-9A-F]+)> Tj")]
    private static partial Regex HexText();

    [GeneratedRegex(@"startxref\n(\d+)")]
    private static partial Regex StartXref();

    [GeneratedRegex(@"(\d{10}) 00000 n ")]
    private static partial Regex XrefEntry();
}

public sealed class PdfTests
{
    [Fact]
    public void FontTablesCoverPrintableAscii()
    {
        foreach (var c in Enumerable.Range(32, 95).Select(i => (char)i))
        {
            Assert.InRange(PdfFonts.Width(c, bold: false), 150, 1100);
            Assert.InRange(PdfFonts.Width(c, bold: true), 150, 1100);
        }

        Assert.Equal(PdfFonts.Width('a', false), PdfFonts.Width('ä', false));
        Assert.Equal(PdfFonts.Width('A', false), PdfFonts.Width('Ä', false));
        Assert.Equal([0x80, 0xE4, 0x84, (byte)'?'], PdfFonts.Encode("€ä„✓"));
    }

    [Fact]
    public void PasswordFollowsTheStandardEncoding()
    {
        // ISO 32000-2: SASLprep, dann UTF-8. Zusammengesetzt (NFC), auch wenn es zerlegt eingegeben wurde.
        Assert.Equal("ab\u00FC"u8.ToArray(), PdfEncryptor.PasswordBytes("abu\u0308"));
        Assert.Equal(127, PdfEncryptor.PasswordBytes(new string('x', 200)).Length);
    }

    [Fact]
    public void WrapsLongTextWithinWidth()
    {
        const string text = "Eine Lebensversicherung mit Bezugsrecht für beide Kinder, Meldefrist 48 Stunden nach dem Todesfall bei der Gesellschaft.";
        var lines = PdfLayout.Wrap(text, 10, false, 150);

        Assert.True(lines.Count > 2);
        Assert.All(lines, l => Assert.True(PdfFonts.Measure(l, 10, false) <= 150));
        Assert.Equal(text, string.Join(" ", lines));
    }

    [Fact]
    public async Task UnprotectedPdfHasConsistentStructureAndText()
    {
        var pdf = await SampleAsync(null);

        Assert.StartsWith("%PDF-1.7", Encoding.ASCII.GetString(pdf, 0, 8), StringComparison.Ordinal);
        PdfReader.AssertXrefIsConsistent(pdf);
        var shown = PdfReader.ShownText(PdfReader.Streams(pdf, null));
        Assert.Contains("Girokonto Sparkasse", shown, StringComparison.Ordinal);
        Assert.Contains("Schließfach", shown, StringComparison.Ordinal);
        Assert.Contains("Seite 2 von 2", shown, StringComparison.Ordinal);
        Assert.DoesNotContain("/Encrypt", Encoding.Latin1.GetString(pdf), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProtectedPdfOpensOnlyWithItsPassword()
    {
        const string password = "Notfall-Ordner 2026!";
        var pdf = await SampleAsync(password);
        var raw = Encoding.Latin1.GetString(pdf);

        Assert.Contains("/Filter /Standard /V 5 /R 6", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("Girokonto", raw, StringComparison.Ordinal);
        PdfReader.AssertXrefIsConsistent(pdf);
        Assert.Null(await PdfReader.OpenAsync(pdf, "falsches Passwort"));

        var fileKey = await PdfReader.OpenAsync(pdf, password);
        Assert.NotNull(fileKey);
        var shown = PdfReader.ShownText(PdfReader.Streams(pdf, fileKey));
        Assert.Contains("Girokonto Sparkasse", shown, StringComparison.Ordinal);
        Assert.Contains("DE89 3704 0044 0532 0130 00", shown, StringComparison.Ordinal);

        // Zum Prüfen mit einem echten Betrachter: MYLIFE_PDF_OUT=/pfad dotnet test …
        if (Environment.GetEnvironmentVariable("MYLIFE_PDF_OUT") is { Length: > 0 } directory)
        {
            await File.WriteAllBytesAsync(Path.Combine(directory, "protected.pdf"), pdf, TestContext.Current.CancellationToken);
            await File.WriteAllBytesAsync(Path.Combine(directory, "plain.pdf"), await SampleAsync(null), TestContext.Current.CancellationToken);
        }
    }

    private static async Task<byte[]> SampleAsync(string? password)
    {
        var layout = new PdfLayout();
        layout.Title("Notfallordner");
        layout.Subtitle("Max Mustermann");
        layout.Subtitle("Stand: 24. November 2026", 11);
        layout.Body(string.Join(" ", Enumerable.Repeat("Dieser Ordner fasst alles zusammen, was Angehörige im Ernstfall brauchen.", 5)));
        layout.Section("Inhalt");
        layout.TocEntry("Bankkonten & Depots", 2);
        layout.Heading("Bankkonten & Depots");
        layout.Subheading("Girokonto Sparkasse");
        layout.Row("IBAN", "DE89 3704 0044 0532 0130 00");
        layout.Row("Notizen", string.Join(" ", Enumerable.Repeat("Ein langer Hinweis mit Umlauten äöü und „Anführungszeichen“.", 8)));
        layout.Rule();
        layout.NewPage();
        layout.Subheading("Schließfach");
        layout.Row("Fachnummer", "4711");
        var encryptor = password is null ? null : await PdfEncryptor.CreateAsync(new DotNetPdfCrypto(), password);
        return await PdfWriter.WriteAsync(layout.Pages, "Notfallordner", "myLife – Seite {0} von {1}", encryptor, DateTimeOffset.UtcNow);
    }
}
