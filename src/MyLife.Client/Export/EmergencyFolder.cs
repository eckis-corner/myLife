using System.Globalization;
using Microsoft.Extensions.Localization;
using MyLife.Client.Export.Pdf;
using MyLife.Client.Resources;
using MyLife.Client.Services;
using MyLife.Shared.Vault;

namespace MyLife.Client.Export;

/// <summary>
/// Notfallordner als PDF (Architektur 7, Entscheidung 20): Deckblatt mit Inhaltsverzeichnis, je Bereich die
/// Datenblätter aller Objekte mit Verknüpfungen und der Liste der Dokumente. Entsteht vollständig im Browser;
/// mit Passwort AES-256-verschlüsselt. Dazu das einzelne Datenblatt als PDF.
/// </summary>
public sealed class EmergencyFolder(VaultService vault, SchemaText text, IStringLocalizer<SharedResource> l, IPdfCrypto crypto)
{
    /// <summary>Objekte eines Bereichs in Katalog-Reihenfolge der Typen, dann nach Titel.</summary>
    public IReadOnlyList<VaultItem> ItemsIn(int area, bool includeEnded) =>
    [
        .. vault.Current
            .Where(i => i.Area == area && (includeEnded || i.Status == ObjectStatus.Active))
            .OrderBy(i => IndexOfType(i.Type))
            .ThenBy(i => i.Title, StringComparer.CurrentCultureIgnoreCase),
    ];

    public async Task<byte[]> CreatePdfAsync(IReadOnlyCollection<int> areas, bool includeEnded, string? password, string ownerName, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(areas);
        var content = new PdfLayout();
        var starts = new List<(int Area, int PageIndex)>();
        foreach (var area in ObjectCatalog.Areas.Where(a => areas.Contains(a.Number)).Select(a => a.Number))
        {
            var items = ItemsIn(area, includeEnded);
            if (items.Count == 0)
            {
                continue;
            }

            content.NewPage();
            starts.Add((area, content.CurrentPageIndex));
            content.Heading(text.Area(area));
            content.Paragraph(text.AreaText(area), 9, muted: true);
            foreach (var item in items)
            {
                WriteItem(content, item);
            }
        }

        // Das Deckblatt kennt die Seitenzahlen erst, wenn der Inhalt gesetzt ist.
        var front = new PdfLayout();
        var frontPages = 1;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            front = new PdfLayout();
            WriteFront(front, ownerName, now, starts, frontPages);
            if (front.Pages.Count == frontPages)
            {
                break;
            }

            frontPages = front.Pages.Count;
        }

        var title = l["FolderTitle"].Value;
        return await PdfWriter.WriteAsync([.. front.Pages, .. content.Pages], title, $"myLife – {Escape(title)} – {l["PdfPage"].Value}", await EncryptorAsync(password), now);
    }

    public async Task<byte[]> CreateDatasheetPdfAsync(VaultItem item, string? password, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(item);
        var layout = new PdfLayout();
        layout.Paragraph($"{text.Area(item.Area)} · {l["DatasheetTitle"].Value} · {now.ToLocalTime().ToString("d", CultureInfo.CurrentCulture)}", 9, muted: true);
        WriteItem(layout, item, asTitle: true);
        return await PdfWriter.WriteAsync(layout.Pages, item.Title, $"myLife – {Escape(item.Title)} – {l["PdfPage"].Value}", await EncryptorAsync(password), now);
    }

    private void WriteFront(PdfLayout layout, string ownerName, DateTimeOffset now, List<(int Area, int PageIndex)> starts, int frontPages)
    {
        layout.Logo();
        layout.Space(18);
        layout.Title(l["FolderTitle"]);
        layout.Subtitle(ownerName);
        layout.Subtitle(l["FolderAsOf", now.ToLocalTime().ToString("D", CultureInfo.CurrentCulture)], 11);
        layout.Space(24);
        layout.Body(l["FolderIntro"]);
        layout.Body(l["FolderSensitive"], bold: true);
        layout.Section(l["FolderContents"]);
        foreach (var (area, pageIndex) in starts)
        {
            layout.TocEntry(text.Area(area), pageIndex + frontPages + 1);
        }

        if (starts.Count == 0)
        {
            layout.Paragraph(l["FolderEmpty"], muted: true);
        }
    }

    private void WriteItem(PdfLayout layout, VaultItem item, bool asTitle = false)
    {
        var kind = text.Type(item.Type);
        var caption = item.Status == ObjectStatus.Ended ? $"{kind} · {text.Status(item.Status)}" : kind;
        if (asTitle)
        {
            layout.Title(item.Title);
            layout.Subtitle(caption);
            layout.Space(8);
        }
        else
        {
            layout.Subheading(item.Title);
            layout.Paragraph(caption, 9, muted: true);
        }

        foreach (var row in text.Rows(item, vault.TitleOf))
        {
            layout.Row(row.Label, row.Value);
        }

        var links = item.Payload.Links.Select(vault.TitleOf).Concat(vault.ReferencesTo(item.Id).Select(r => r.Title))
            .Where(t => t is not null).Distinct().ToList();
        if (links.Count > 0)
        {
            layout.Row(l["CommonLinks"], string.Join("\n", links));
        }

        var files = vault.FilesOf(item.Id).Where(f => !f.IsDeleted).ToList();
        if (files.Count > 0)
        {
            layout.Row(l["DocumentsTitle"], string.Join("\n", files.Select(f => string.IsNullOrWhiteSpace(f.Metadata.Label) ? f.Metadata.FileName : $"{f.Metadata.Label} ({f.Metadata.FileName})")));
        }

        layout.Rule();
    }

    private static string Escape(string value) => value.Replace("{", "{{", StringComparison.Ordinal).Replace("}", "}}", StringComparison.Ordinal);

    private async Task<PdfEncryptor?> EncryptorAsync(string? password) =>
        string.IsNullOrEmpty(password) ? null : await PdfEncryptor.CreateAsync(crypto, password);

    private static int IndexOfType(ObjectTypeDefinition type)
    {
        for (var i = 0; i < ObjectCatalog.Types.Count; i++)
        {
            if (ObjectCatalog.Types[i].Key == type.Key)
            {
                return i;
            }
        }

        return int.MaxValue;
    }
}
