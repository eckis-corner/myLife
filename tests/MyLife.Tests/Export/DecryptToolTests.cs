using System.Security.Cryptography;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MyLife.Client.Export;
using MyLife.Client.Resources;
using MyLife.Crypto;
using MyLife.Decrypt;
using MyLife.Shared.Vault;
using MyLife.Tests.Server;
using static MyLife.Tests.Server.TestToken;

namespace MyLife.Tests.Export;

/// <summary>
/// mylife-decrypt (M3.5): Ein echter JSON-Export öffnet sich mit dem Master-Passwort und mit den 24 Wörtern des
/// Recovery-Kits, ein Notfallpaket mit den Wörtern vom Schlüsselblatt; heraus kommen lesbare Datenblätter und die Originale.
/// </summary>
public sealed class DecryptToolTests(ServerFixture server) : IClassFixture<ServerFixture>, IAsyncLifetime
{
    private AuthTestSupport _account = null!;
    private readonly string _work = Path.Combine(Path.GetTempPath(), "mylife-decrypt-tests", Guid.NewGuid().ToString("N"));

    public async ValueTask InitializeAsync()
    {
        _account = await AuthTestSupport.ForAsync(server);
        Directory.CreateDirectory(_work);
    }

    public ValueTask DisposeAsync()
    {
        Directory.Delete(_work, recursive: true);
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task AJsonExportOpensWithThePasswordOrTheRecoveryWords()
    {
        var a = await VaultBrowser.OpenAsync(_account);
        var contact = await a.Vault.CreateAsync("contact", new VaultPayload { Title = "Notarin Weber", Fields = { ["name"] = "Weber" } });
        var will = await a.Vault.CreateAsync("legalDocument", new VaultPayload
        {
            Title = "Testament Decrypt", OriginalLocation = "Schreibtisch", ContactId = contact.Id, Fields = { ["documentKind"] = "will" },
        });
        var document = RandomNumberGenerator.GetBytes(VaultLimits.ChunkSize + 4321);
        await a.Vault.UploadAsync(will.Id, new MemoryStream(document), new DocumentMetadata { Label = "Scan", FileName = "testament.pdf", ContentType = "application/pdf", Size = document.Length });
        await a.Vault.CreateAsync("bankAccount", VaultSamples.BankAccount("Girokonto Decrypt", "Decrypt"));
        var exportFile = Path.Combine(_work, "export.mylife.json");
        await File.WriteAllBytesAsync(exportFile, await new VaultExport(a.Api, a.Vault, a.Session, a.Crypto).ExportAsync(includeDocuments: true, DateTimeOffset.UtcNow), Ct);

        // Mit dem Master-Passwort.
        var target = Path.Combine(_work, "passwort");
        var (code, output) = await RunAsync(["export", exportFile, target], SetupTestSupport.Password);
        Assert.Equal(0, code);
        Assert.Contains("Fertig", output, StringComparison.Ordinal);
        var index = await File.ReadAllTextAsync(Path.Combine(target, "index.txt"), Ct);
        Assert.Contains("Testament Decrypt (Urkunde / Dokument)", index, StringComparison.Ordinal);

        var sheet = await File.ReadAllTextAsync(Directory.GetFiles(target, "Testament Decrypt*.txt", SearchOption.AllDirectories).Single(), Ct);
        Assert.Contains("Art: Testament", sheet, StringComparison.Ordinal);
        Assert.Contains("Ablageort des Originals: Schreibtisch", sheet, StringComparison.Ordinal);
        Assert.Contains("Ansprechpartner: Notarin Weber", sheet, StringComparison.Ordinal);
        Assert.Contains("Scan: testament.pdf", sheet, StringComparison.Ordinal);
        Assert.Equal(document, await File.ReadAllBytesAsync(Directory.GetFiles(target, "testament.pdf", SearchOption.AllDirectories).Single(), Ct));
        var account = await File.ReadAllTextAsync(Directory.GetFiles(target, "Girokonto Decrypt*.txt", SearchOption.AllDirectories).Single(), Ct);
        Assert.Contains("IBAN: DE89 3704 0044 0532 0130 00", account, StringComparison.Ordinal);
        Assert.Contains("Wunsch für die Hinterbliebenen: auflösen", account, StringComparison.Ordinal);

        // Mit den 24 Wörtern des Recovery-Kits, englisch beschriftet.
        var english = Path.Combine(_work, "recovery");
        Assert.Equal(0, (await RunAsync(["export", exportFile, english, "--en"], string.Join(' ', Bip39.Encode(_account.RecoveryKey)))).Code);
        Assert.Contains("Will", await File.ReadAllTextAsync(Directory.GetFiles(english, "Testament Decrypt*.txt", SearchOption.AllDirectories).Single(), Ct), StringComparison.Ordinal);

        // Falsches Passwort, belegter Zielordner, falscher Aufruf.
        Assert.Equal(1, (await RunAsync(["export", exportFile, Path.Combine(_work, "falsch")], "falsches-Passwort")).Code);
        Assert.Equal(2, (await RunAsync(["export", exportFile, target], SetupTestSupport.Password)).Code);
        Assert.Equal(2, (await RunAsync(["auspacken"], null)).Code);
    }

    [Fact]
    public async Task AnEmergencyPackageOpensWithItsWords()
    {
        var a = await VaultBrowser.OpenAsync(_account);
        var item = await a.Vault.CreateAsync("bankAccount", VaultSamples.BankAccount("Paketkonto Decrypt", "Paket"));
        var document = "%PDF Paket"u8.ToArray();
        await a.Vault.UploadAsync(item.Id, new MemoryStream(document), new DocumentMetadata { Label = "Auszug", FileName = "auszug.pdf", ContentType = "application/pdf", Size = document.Length });
        var text = VaultBrowser.Text();
        var factory = new ResourceManagerStringLocalizerFactory(Options.Create(new LocalizationOptions()), NullLoggerFactory.Instance);
        var folder = new EmergencyFolder(a.Vault, text, new StringLocalizer<SharedResource>(factory), new DotNetPdfCrypto());
        var package = await new EmergencyPackage(a.Vault, folder, text, a.Crypto).CreateAsync([1], false, true, "Daniel", DateTimeOffset.UtcNow);
        var file = Path.Combine(_work, "paket.html");
        await File.WriteAllBytesAsync(file, package.Html, Ct);

        var target = Path.Combine(_work, "paket");
        Assert.Equal(0, (await RunAsync(["package", file, target], string.Join(' ', package.Words))).Code);
        var sheet = await File.ReadAllTextAsync(Directory.GetFiles(target, "Paketkonto Decrypt*.txt", SearchOption.AllDirectories).Single(), Ct);
        Assert.Contains("IBAN: DE89 3704 0044 0532 0130 00", sheet, StringComparison.Ordinal);
        Assert.Equal(document, await File.ReadAllBytesAsync(Directory.GetFiles(target, "auszug.pdf", SearchOption.AllDirectories).Single(), Ct));

        Assert.Equal(1, (await RunAsync(["package", file, Path.Combine(_work, "paket-falsch")], string.Join(' ', Bip39.Encode(RandomNumberGenerator.GetBytes(32))))).Code);
    }

    [Fact]
    public void FileNamesStayInsideTheTargetFolder()
    {
        Assert.Equal("_.._geheim", DecryptTool.SafeName("/../geheim"));
        Assert.Equal("a_b", DecryptTool.SafeName("a\\b"));
        Assert.Equal("_", DecryptTool.SafeName(".."));
    }

    private static async Task<(int Code, string Output)> RunAsync(string[] args, string? secret)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = await DecryptTool.RunAsync(args, new StringReader(string.Empty), output, error, secret);
        return (code, output + error.ToString());
    }
}
