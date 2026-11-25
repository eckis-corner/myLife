using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MyLife.Client.Export;
using MyLife.Client.Resources;
using MyLife.Crypto;
using MyLife.Shared.Vault;
using MyLife.Tests.Server;
using static MyLife.Tests.Server.TestToken;

namespace MyLife.Tests.Export;

/// <summary>
/// Notfallpaket (M2.7): Das im Browser erzeugte Paket öffnet sich mit seinen 24 Wörtern, in .NET und mit dem Kern
/// des OfflineViewers in Node.js; ohne die Wörter steht nichts Lesbares in der Datei.
/// </summary>
public sealed partial class PackageTests(ServerFixture server) : IClassFixture<ServerFixture>, IAsyncLifetime
{
    private AuthTestSupport _account = null!;

    public async ValueTask InitializeAsync() => _account = await AuthTestSupport.ForAsync(server);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task ThePackageOpensWithItsWordsAndShowsOnlyTheChosenAreas()
    {
        var a = await VaultBrowser.OpenAsync(_account);
        var bank = await a.Vault.CreateAsync("bankAccount", VaultSamples.BankAccount("Girokonto Paket", "Paket"));
        var document = Encoding.UTF8.GetBytes("%PDF-1.4 Kontoauszug für das Paket");
        await a.Vault.UploadAsync(bank.Id, new MemoryStream(document),
            new DocumentMetadata { Label = "Kontoauszug", FileName = "auszug.pdf", ContentType = "application/pdf", Size = document.Length });
        await a.Vault.CreateAsync("insurance", new VaultPayload { Title = "Hausrat Paket", Fields = { ["insuranceKind"] = "household", ["insurer"] = "HUK" } });

        var text = VaultBrowser.Text();
        var factory = new ResourceManagerStringLocalizerFactory(Options.Create(new LocalizationOptions()), NullLoggerFactory.Instance);
        var folder = new EmergencyFolder(a.Vault, text, new StringLocalizer<SharedResource>(factory), new DotNetPdfCrypto());
        var package = new EmergencyPackage(a.Vault, folder, text, a.Crypto);
        Assert.Equal(document.Length, package.DocumentBytes([1], includeEnded: false));

        var result = await package.CreateAsync([1], includeEnded: false, withDocuments: true, "Daniel", DateTimeOffset.UtcNow);
        Assert.Equal(24, result.Words.Count);
        Assert.Equal(1, result.Documents);

        // In der Datei steht nichts Lesbares; sie lädt nichts nach und schickt nichts weg.
        var html = Encoding.UTF8.GetString(result.Html);
        Assert.DoesNotContain("Girokonto Paket", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Kontoauszug", html, StringComparison.Ordinal);
        Assert.Contains("default-src 'none'", html, StringComparison.Ordinal);
        Assert.DoesNotContain("http://", Block(html, "mylife-core"), StringComparison.Ordinal);

        // .NET öffnet das Paket mit den Wörtern.
        Assert.True(Bip39.TryDecode(string.Join(' ', result.Words), out var key));
        using var header = JsonDocument.Parse(Block(html, "mylife-package"));
        var handle = await a.Crypto.ImportPackageKeyAsync(key);
        var plain = await a.Crypto.DecryptAsync(handle, Convert.FromBase64String(header.RootElement.GetProperty("data").GetString()!), EmergencyPackage.Context);
        using var content = JsonDocument.Parse(plain);
        AssertContent(content.RootElement, document);

        // Der Viewer-Kern in Node.js öffnet es auch mit Nummern und nur vier Buchstaben je Wort; andere Wörter nicht.
        var typed = string.Join(' ', result.Words.Select((w, i) => $"{i + 1}. {w[..Math.Min(4, w.Length)].ToUpperInvariant()}"));
        var other = string.Join(' ', Bip39.Encode(await a.Crypto.RandomBytesAsync(32)));
        var path = Path.Combine(Path.GetTempPath(), $"mylife-package-{Guid.NewGuid():N}.html");
        await File.WriteAllBytesAsync(path, result.Html, Ct);
        try
        {
            using var viewer = JsonDocument.Parse(await RunViewerAsync(path, typed, other));
            AssertContent(viewer.RootElement.GetProperty("content"), document);
            Assert.Equal("wrongKey", viewer.RootElement.GetProperty("wrongKey").GetString());
            Assert.Equal("wordCount", viewer.RootElement.GetProperty("tooShort").GetString());
            Assert.Equal("unknownWord", viewer.RootElement.GetProperty("unknown").GetString());
            // Vertauschte Wörter fallen fast immer an der Prüfsumme auf, sonst am Schlüssel.
            Assert.True(viewer.RootElement.GetProperty("swapped").GetString() is "checksum" or "wrongKey");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TheTemplateHasBothPlaceholdersAndNoNetworkAccess()
    {
        var template = EmergencyPackage.Template();
        Assert.Contains("id=\"mylife-words\">null</script>", template, StringComparison.Ordinal);
        Assert.Contains("id=\"mylife-package\">null</script>", template, StringComparison.Ordinal);
        Assert.DoesNotContain("fetch(", template, StringComparison.Ordinal);
        Assert.DoesNotContain("XMLHttpRequest", template, StringComparison.Ordinal);
        Assert.DoesNotContain("innerHTML", template, StringComparison.Ordinal);
    }

    private static void AssertContent(JsonElement content, byte[] document)
    {
        Assert.Equal("mylife-package-content", content.GetProperty("format").GetString());
        Assert.Equal("Daniel", content.GetProperty("owner").GetString());
        var area = Assert.Single(content.GetProperty("areas").EnumerateArray());
        Assert.Equal(1, area.GetProperty("area").GetInt32());
        var item = area.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("title").GetString() == "Girokonto Paket");
        Assert.Contains(item.GetProperty("rows").EnumerateArray(), r => r.GetProperty("value").GetString() == "DE89 3704 0044 0532 0130 00");
        var file = Assert.Single(item.GetProperty("documents").EnumerateArray());
        Assert.Equal("Kontoauszug", file.GetProperty("label").GetString());
        Assert.Equal(document, Convert.FromBase64String(file.GetProperty("data").GetString()!));
        Assert.DoesNotContain("Hausrat Paket", content.GetRawText(), StringComparison.Ordinal);
    }

    private static string Block(string html, string id)
    {
        var match = Regex.Match(html, $"<script[^>]*id=\"{id}\"[^>]*>([\\s\\S]*?)</script>");
        Assert.True(match.Success, $"Block {id} fehlt.");
        return match.Groups[1].Value;
    }

    private static async Task<string> RunViewerAsync(string htmlPath, string phrase, string wrongPhrase)
    {
        var root = ResourceParityTests.FindRepositoryRoot();
        var start = new ProcessStartInfo("node")
        {
            ArgumentList = { Path.Combine(root, "tests", "MyLife.Tests", "Export", "viewer-harness.mjs"), htmlPath, phrase, wrongPhrase },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        Process process;
        try
        {
            process = Process.Start(start) ?? throw new InvalidOperationException("node konnte nicht gestartet werden.");
        }
        catch (Win32Exception) when (Environment.GetEnvironmentVariable("CI") is null)
        {
            Assert.Skip("Node.js ist nicht installiert; der Test des OfflineViewers läuft in der CI.");
            throw;
        }

        using (process)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
            timeout.CancelAfter(TimeSpan.FromMinutes(1));
            var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            Assert.True(process.ExitCode == 0, $"viewer-harness.mjs ist fehlgeschlagen:\n{await stderr}");
            return await stdout;
        }
    }
}
