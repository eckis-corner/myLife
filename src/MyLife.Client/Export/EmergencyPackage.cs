using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MyLife.Client.Services;
using MyLife.Crypto;
using MyLife.Shared.Vault;

namespace MyLife.Client.Export;

/// <summary>Ergebnis: die fertige HTML-Datei und die 24 Wörter für das Schlüsselblatt (nur dieses eine Mal sichtbar).</summary>
public sealed record PackageResult(byte[] Html, IReadOnlyList<string> Words, int Items, int Documents, DateTimeOffset CreatedAt);

/// <summary>
/// Notfallpaket für Vertrauenspersonen (M2.7, Architektur 7, Entscheidung 4b): eine einzelne HTML-Datei, die ohne
/// Server und ohne Netz die ausgewählten Bereiche als Datenblätter samt Dokumenten zeigt. Der Inhalt ist mit einem
/// eigenen Paketschlüssel verschlüsselt, den es nur gedruckt als 24 Wörter gibt. Alles entsteht im Browser; der
/// Server sieht weder Inhalt noch Schlüssel. Format: docs/format.md, Abschnitt „Notfallpaket“.
/// </summary>
public sealed class EmergencyPackage(VaultService vault, EmergencyFolder folder, SchemaText text, IMyLifeCrypto crypto)
{
    /// <summary>Kontext des verschlüsselten Inhalts (kryptoformat.md).</summary>
    public const string Context = "package";

    private const string WordsPlaceholder = "<script type=\"application/json\" id=\"mylife-words\">null</script>";
    private const string PackagePlaceholder = "<script type=\"application/json\" id=\"mylife-package\">null</script>";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    /// <summary>Gesamtgröße der Dokumente in diesen Bereichen (zur Anzeige vor dem Erzeugen).</summary>
    public long DocumentBytes(IReadOnlyCollection<int> areas, bool includeEnded)
    {
        ArgumentNullException.ThrowIfNull(areas);
        return areas.SelectMany(area => folder.ItemsIn(area, includeEnded))
            .SelectMany(item => vault.FilesOf(item.Id).Where(f => !f.IsDeleted)).Sum(f => f.Metadata.Size);
    }

    public async Task<PackageResult> CreateAsync(IReadOnlyCollection<int> areas, bool includeEnded, bool withDocuments, string ownerName, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(areas);
        await vault.EnsureLoadedAsync();
        var content = new PackageContent("mylife-package-content", 1, ownerName, now, Language(), []);
        var items = 0;
        var documents = 0;
        foreach (var area in ObjectCatalog.Areas.Where(a => areas.Contains(a.Number)).Select(a => a.Number))
        {
            var inArea = folder.ItemsIn(area, includeEnded);
            if (inArea.Count == 0)
            {
                continue;
            }

            var packed = new List<PackageItem>();
            foreach (var item in inArea)
            {
                var files = new List<PackageDocument>();
                foreach (var file in withDocuments ? vault.FilesOf(item.Id).Where(f => !f.IsDeleted).ToList() : [])
                {
                    files.Add(new PackageDocument(
                        string.IsNullOrWhiteSpace(file.Metadata.Label) ? null : file.Metadata.Label, file.Metadata.FileName, file.Metadata.ContentType,
                        file.Metadata.Size, Convert.ToBase64String(await vault.DownloadAsync(file))));
                }

                var kind = text.Type(item.Type);
                packed.Add(new PackageItem(
                    item.Title,
                    item.Status == ObjectStatus.Ended ? $"{kind} · {text.Status(item.Status)}" : kind,
                    [.. text.Rows(item, vault.TitleOf).Select(r => new PackageRow(r.Label, r.Value, r.Multiline ? true : null))],
                    [.. item.Payload.Links.Select(vault.TitleOf).Concat(vault.ReferencesTo(item.Id).Select(r => r.Title)).OfType<string>().Distinct()],
                    files));
                items++;
                documents += files.Count;
            }

            content.Areas.Add(new PackageArea(area, text.Area(area), text.AreaText(area), packed));
        }

        // Eigener Schlüssel nur für dieses Paket: 32 Zufallsbytes, gedruckt als 24 Wörter, im Browser sofort verworfen.
        var packageKey = await crypto.RandomBytesAsync(Bip39.EntropyBytes);
        var words = Bip39.Encode(packageKey);
        var handle = await crypto.ImportPackageKeyAsync(packageKey);
        Array.Clear(packageKey);
        byte[] blob;
        try
        {
            blob = await crypto.EncryptAsync(handle, JsonSerializer.SerializeToUtf8Bytes(content, Json), Context);
        }
        finally
        {
            await crypto.ReleaseAsync(handle);
        }

        var header = new PackageHeader("mylife-package", 1, CryptoFormat.CurrentVersion, content.Language, now, Convert.ToBase64String(blob));
        return new PackageResult(Fill(header), words, items, documents, now);
    }

    /// <summary>Setzt Wortliste und Paket in die Vorlage. System.Text.Json maskiert &lt; und &gt;, ein Skriptende kann nicht entstehen.</summary>
    internal static byte[] Fill(PackageHeader header)
    {
        var template = Template();
        if (!template.Contains(WordsPlaceholder, StringComparison.Ordinal) || !template.Contains(PackagePlaceholder, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Vorlage des OfflineViewers ohne Platzhalter.");
        }

        var html = template
            .Replace(WordsPlaceholder, WordsPlaceholder.Replace(">null<", $">{JsonSerializer.Serialize(Bip39.Words)}<", StringComparison.Ordinal), StringComparison.Ordinal)
            .Replace(PackagePlaceholder, PackagePlaceholder.Replace(">null<", $">{JsonSerializer.Serialize(header, Json)}<", StringComparison.Ordinal), StringComparison.Ordinal);
        return Encoding.UTF8.GetBytes(html);
    }

    /// <summary>Vorlage aus <c>src/MyLife.OfflineViewer/OfflineViewer.html</c>, als Ressource eingebettet.</summary>
    public static string Template()
    {
        using var stream = typeof(EmergencyPackage).Assembly.GetManifestResourceStream("MyLife.Client.OfflineViewer.html")
            ?? throw new InvalidOperationException("OfflineViewer.html fehlt.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string Language() => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en" ? "en" : "de";

    internal sealed record PackageHeader(string Format, int Version, int CryptoFormat, string Language, DateTimeOffset CreatedAt, string Data);

    internal sealed record PackageContent(string Format, int Version, string Owner, DateTimeOffset CreatedAt, string Language, List<PackageArea> Areas);

    internal sealed record PackageArea(int Area, string Title, string Text, IReadOnlyList<PackageItem> Items);

    internal sealed record PackageItem(string Title, string Kind, IReadOnlyList<PackageRow> Rows, IReadOnlyList<string> Links, IReadOnlyList<PackageDocument> Documents);

    internal sealed record PackageRow(string Label, string Value, bool? Multiline);

    internal sealed record PackageDocument(string? Label, string FileName, string ContentType, long Size, string Data);
}
