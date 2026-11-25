using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MyLife.Client.Export;
using MyLife.Client.Resources;
using MyLife.Client.Services;
using MyLife.Crypto;
using MyLife.Server.Api;
using MyLife.Server.Data;
using MyLife.Server.Infrastructure;
using MyLife.Server.Infrastructure.Auth;
using MyLife.Shared.Vault;
using static MyLife.Tests.Server.TestToken;

namespace MyLife.Tests.Server;

/// <summary>Ein angemeldeter „Browser“ mit entsperrtem Tresor, wie im echten Client verdrahtet.</summary>
internal sealed record VaultBrowser(SessionService Session, HttpClient Http, VaultClient Api, VaultService Vault, ReferenceMyLifeCrypto Crypto)
{
    public static async Task<VaultBrowser> OpenAsync(AuthTestSupport account, string email = AuthTestSupport.Email, Func<string>? code = null)
    {
        var (session, _, http, crypto) = account.NewBrowserWithCrypto();
        var challenge = await session.BeginLoginAsync(email, SetupTestSupport.Password);
        await session.CompleteLoginWithTotpAsync(challenge.Pending, (code ?? account.NextCode)());
        var api = new VaultClient(http);
        var vault = new VaultService(api, crypto, session);
        await vault.EnsureLoadedAsync();
        return new VaultBrowser(session, http, api, vault, crypto);
    }

    public static SchemaText Text()
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de");
        var factory = new ResourceManagerStringLocalizerFactory(Options.Create(new LocalizationOptions()), NullLoggerFactory.Instance);
        return new SchemaText(new StringLocalizer<SharedResource>(factory));
    }
}

internal static class VaultSamples
{
    public static VaultPayload BankAccount(string title, string marker) => new()
    {
        Title = title,
        Fields = { ["bank"] = "Sparkasse " + marker, ["iban"] = "DE89370400440532013000", ["accountKind"] = "checking" },
        Notes = "Notiz " + marker,
        Wish = "close",
    };
}

public sealed class VaultTests(ServerFixture server) : IClassFixture<ServerFixture>, IAsyncLifetime
{
    private AuthTestSupport _account = null!;

    public async ValueTask InitializeAsync() => _account = await AuthTestSupport.ForAsync(server);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task ObjectsAreEncryptedInTheBrowserAndReadableInAnotherSession()
    {
        var marker = "Klartext-" + Guid.NewGuid().ToString("N");
        var a = await VaultBrowser.OpenAsync(_account);
        var contact = await a.Vault.CreateAsync("contact", new VaultPayload { Title = "Bankberaterin", Fields = { ["name"] = "Frau Muster" } });
        var payload = VaultSamples.BankAccount("Girokonto " + marker, marker);
        payload.ContactId = contact.Id;
        var created = await a.Vault.CreateAsync("bankAccount", payload);

        await using (var scope = server.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MyLifeDbContext>();
            var stored = await db.Objects.AsNoTracking().SingleAsync(o => o.Id == created.Id, Ct);
            Assert.Equal("bankAccount", stored.TypeKey);
            Assert.Equal(1, stored.AreaKey);
            Assert.Equal([CryptoFormat.CurrentVersion, (byte)CryptoKind.Data], stored.Payload[..2]);
            Assert.DoesNotContain(marker, Encoding.UTF8.GetString(stored.Payload), StringComparison.Ordinal);
            Assert.DoesNotContain("DE89", Encoding.UTF8.GetString(stored.Payload), StringComparison.Ordinal);
            var area = await db.Areas.AsNoTracking().SingleAsync(x => x.AreaKey == 1, Ct);
            Assert.Equal(62, area.WrappedAreaKey.Length);
        }

        var b = await VaultBrowser.OpenAsync(_account);
        var read = b.Vault.Find(created.Id)!;
        Assert.Equal("Girokonto " + marker, read.Title);
        Assert.Equal("DE89370400440532013000", read.Payload.Get("iban"));
        Assert.Equal(contact.Id, read.Payload.ContactId);
        Assert.Contains(b.Vault.ReferencesTo(contact.Id), i => i.Id == created.Id);
        Assert.Equal(0, b.Vault.UnreadableCount);
    }

    [Fact]
    public async Task StaleVersionIsRejectedWithConflict()
    {
        var a = await VaultBrowser.OpenAsync(_account);
        var item = await a.Vault.CreateAsync("contract", new VaultPayload { Title = "Strom", Fields = { ["contractKind"] = "electricity", ["provider"] = "Stadtwerke" } });
        var b = await VaultBrowser.OpenAsync(_account);

        var changed = item.Payload.Clone();
        changed.Notes = "von A";
        await a.Vault.UpdateAsync(item, changed, ObjectStatus.Ended);

        var stale = b.Vault.Find(item.Id)!;
        var other = stale.Payload.Clone();
        other.Notes = "von B";
        var conflict = await Assert.ThrowsAsync<VaultException>(() => b.Vault.UpdateAsync(stale, other, ObjectStatus.Active));
        Assert.Equal(VaultErrors.Conflict, conflict.Message);

        await b.Vault.ReloadAsync();
        Assert.Equal("von A", b.Vault.Find(item.Id)!.Payload.Notes);
        Assert.Equal(ObjectStatus.Ended, b.Vault.Find(item.Id)!.Status);
    }

    [Fact]
    public async Task TrashRestoreAndPurge()
    {
        var a = await VaultBrowser.OpenAsync(_account);
        var item = await a.Vault.CreateAsync("pet", new VaultPayload { Title = "Minka", Fields = { ["petName"] = "Minka" } });

        var notDeleted = await Assert.ThrowsAsync<VaultException>(() => a.Vault.PurgeAsync(item.Id));
        Assert.Equal(VaultErrors.Conflict, notDeleted.Message);

        await a.Vault.DeleteAsync(item.Id);
        Assert.True(a.Vault.Find(item.Id)!.IsDeleted);
        Assert.DoesNotContain(a.Vault.Current, i => i.Id == item.Id);
        var update = await Assert.ThrowsAsync<VaultException>(() => a.Vault.UpdateAsync(item, item.Payload, ObjectStatus.Active));
        Assert.Equal(VaultErrors.NotFound, update.Message);

        await a.Vault.RestoreAsync(item.Id);
        Assert.False(a.Vault.Find(item.Id)!.IsDeleted);

        await a.Vault.DeleteAsync(item.Id);
        await a.Vault.PurgeAsync(item.Id);
        Assert.Null(a.Vault.Find(item.Id));
        var b = await VaultBrowser.OpenAsync(_account);
        Assert.Null(b.Vault.Find(item.Id));
    }

    [Fact]
    public async Task DocumentsAreChunkedEncryptedAndRoundTrip()
    {
        var a = await VaultBrowser.OpenAsync(_account);
        var item = await a.Vault.CreateAsync("insurance", new VaultPayload { Title = "Hausrat", Fields = { ["insuranceKind"] = "household", ["insurer"] = "Versicherer" } });
        var content = RandomNumberGenerator.GetBytes((2 * VaultLimits.ChunkSize) + 12345);
        var marker = Encoding.ASCII.GetBytes("POLICE-KLARTEXT-" + Guid.NewGuid().ToString("N"));
        marker.CopyTo(content, 100);
        var metadata = new DocumentMetadata { Label = "Police 2026", FileName = "police.pdf", ContentType = "application/pdf", Size = content.Length };

        var file = await a.Vault.UploadAsync(item.Id, new MemoryStream(content), metadata);
        Assert.Equal(VaultLimits.CipherSize(content.Length), file.CipherSize);
        Assert.Equal(3 * 16, file.CipherSize - content.Length);

        await using (var scope = server.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MyLifeDbContext>();
            var stored = await db.Documents.AsNoTracking().SingleAsync(d => d.Id == file.Id, Ct);
            Assert.True(stored.IsComplete);
            var path = Path.Combine(server.Services.GetRequiredService<IOptions<MyLifeOptions>>().Value.DataDirectory, "files", stored.StorageKey);
            var onDisk = await File.ReadAllBytesAsync(path, Ct);
            Assert.Equal(stored.CipherSize, onDisk.Length);
            Assert.Equal(SHA256.HashData(onDisk), stored.CipherSha256);
            Assert.Equal(-1, onDisk.AsSpan().IndexOf(marker));
            Assert.DoesNotContain("police.pdf", Encoding.UTF8.GetString(stored.Metadata), StringComparison.Ordinal);
        }

        var b = await VaultBrowser.OpenAsync(_account);
        var seen = b.Vault.FilesOf(item.Id).Single();
        Assert.Equal("Police 2026", seen.Metadata.Label);
        Assert.Equal(content, await b.Vault.DownloadAsync(seen));

        // Inhalt nur einmal: Der Dateischlüssel gehört zu genau einer Datei.
        var again = await Assert.ThrowsAsync<VaultException>(() => b.Api.UploadContentAsync(file.Id, new byte[file.CipherSize], Ct));
        Assert.Equal(VaultErrors.Conflict, again.Message);

        await b.Vault.DeleteFileAsync(seen);
        await b.Vault.PurgeFileAsync(seen);
        Assert.Empty(b.Vault.FilesOf(item.Id));
    }

    [Fact]
    public async Task ServerRejectsPlaintextAndInconsistentTypes()
    {
        var a = await VaultBrowser.OpenAsync(_account);
        await a.Vault.CreateAsync("taxes", new VaultPayload { Title = "Steuern", Fields = { ["taxOffice"] = "Finanzamt" } });
        var plaintext = Encoding.UTF8.GetBytes("{\"title\":\"Klartext\"}");

        var notEncrypted = await Assert.ThrowsAsync<VaultException>(() =>
            a.Api.CreateObjectAsync(new CreateObjectRequest(Guid.NewGuid(), 13, "taxes", plaintext, 1, ObjectStatus.Active), Ct));
        Assert.Equal(VaultErrors.Invalid, notEncrypted.Message);

        var wrongArea = await Assert.ThrowsAsync<VaultException>(() =>
            a.Api.CreateObjectAsync(new CreateObjectRequest(Guid.NewGuid(), 4, "taxes", [1, 1, .. new byte[40]], 1, ObjectStatus.Active), Ct));
        Assert.Equal(VaultErrors.Invalid, wrongArea.Message);

        var unknownType = await Assert.ThrowsAsync<VaultException>(() =>
            a.Api.CreateObjectAsync(new CreateObjectRequest(Guid.NewGuid(), 13, "zahnbuerste", [1, 1, .. new byte[40]], 1, ObjectStatus.Active), Ct));
        Assert.Equal(VaultErrors.Invalid, unknownType.Message);

        var secondKey = await Assert.ThrowsAsync<VaultException>(() => a.Api.CreateAreaKeyAsync(13, [1, 2, .. new byte[60]], Ct));
        Assert.Equal(VaultErrors.AreaKeyExists, secondKey.Message);

        var unknownArea = await Assert.ThrowsAsync<VaultException>(() => a.Api.CreateAreaKeyAsync(14, [1, 2, .. new byte[60]], Ct));
        Assert.Equal(VaultErrors.Invalid, unknownArea.Message);
    }

    [Fact]
    public async Task ApiNeedsSignInAndSendsNoStore()
    {
        using var anonymous = server.CreateApiClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("api/vault/objects", Ct)).StatusCode);

        var a = await VaultBrowser.OpenAsync(_account);
        using var response = await a.Http.GetAsync("api/vault/areas", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Contains("frame-src 'self' blob:", response.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchFindsDecryptedContentOnlyInTheBrowser()
    {
        var a = await VaultBrowser.OpenAsync(_account);
        var unique = "Zauberwort" + Guid.NewGuid().ToString("N")[..6];
        var hit = await a.Vault.CreateAsync("onlineAccount", new VaultPayload
        {
            Title = "Mail-Konto", Fields = { ["onlineAccountKind"] = "email", ["service"] = "Posteo", ["username"] = unique },
        });
        await a.Vault.UploadAsync(hit.Id, new MemoryStream([1, 2, 3]), new DocumentMetadata { Label = "Kündigungsbestätigung", FileName = "k.pdf", Size = 3 });

        var search = new VaultSearch(a.Vault, VaultBrowser.Text());

        Assert.Equal(hit.Id, Assert.Single(search.Search(unique.ToUpperInvariant())).Item.Id);
        Assert.Contains(search.Search("posteo " + unique), h => h.Item.Id == hit.Id);
        Assert.Contains(search.Search("kundigungsbestatigung"), h => h.Item.Id == hit.Id);
        Assert.Contains(search.Search("Digitaler Nachlass " + unique[..8]), h => h.Item.Id == hit.Id);
        Assert.DoesNotContain(search.Search(unique + " Sparbuch"), h => h.Item.Id == hit.Id);
        Assert.Empty(search.Search("   "));
    }

    [Fact]
    public async Task AuditLogRecordsActionsWithoutContent()
    {
        var a = await VaultBrowser.OpenAsync(_account);
        var item = await a.Vault.CreateAsync("device", new VaultPayload { Title = "Geheimes Handy", Fields = { ["deviceKind"] = "smartphone", ["deviceName"] = "Pixel" } });
        await a.Vault.MarkAreaReviewedAsync(8);
        await a.Api.ReportAsync(AuditActions.ExportPdf, "protected", Ct);

        var forbidden = await Assert.ThrowsAsync<VaultException>(() => a.Api.ReportAsync(AuditActions.ObjectPurged, null, Ct));
        Assert.Equal(VaultErrors.Invalid, forbidden.Message);

        var page = await a.Api.GetAuditAsync(0, 100, Ct);
        var actions = page.Items.Select(e => e.Action).ToList();
        Assert.Contains(AuditActions.Login, actions);
        Assert.Contains(AuditActions.ExportPdf, actions);
        Assert.Contains(AuditActions.AreaReviewed, actions);
        var created = page.Items.First(e => e.Action == AuditActions.ObjectCreated && e.TargetId == item.Id);
        Assert.Equal("device", created.TargetType);
        Assert.Equal("Daniel", created.ActorName);
        Assert.DoesNotContain(page.Items, e => (e.Details ?? string.Empty).Contains("Geheim", StringComparison.Ordinal));
        Assert.True(page.Total >= page.Items.Count);
        Assert.Single((await a.Api.GetAuditAsync(0, 1, Ct)).Items);
    }

    [Fact]
    public void IpAddressesAreShortened()
    {
        Assert.Equal("192.168.17.0/24", AuditLog.Shorten(IPAddress.Parse("192.168.17.42")));
        Assert.Equal("192.168.17.0/24", AuditLog.Shorten(IPAddress.Parse("::ffff:192.168.17.42")));
        Assert.Equal("2001:db8:1234::/48", AuditLog.Shorten(IPAddress.Parse("2001:db8:1234:5678::1")));
        Assert.Null(AuditLog.Shorten(null));
    }
}

/// <summary>Zweites Konto: Kein Nutzer sieht oder ändert den Tresor eines anderen.</summary>
public sealed class VaultIsolationTests(ServerFixture server) : IClassFixture<ServerFixture>, IAsyncLifetime
{
    private const string OtherEmail = "zweite@example.org";
    private AuthTestSupport _account = null!;
    private string _otherSecret = null!;

    public async ValueTask InitializeAsync()
    {
        _account = await AuthTestSupport.ForAsync(server);
        _otherSecret = await CreateUserAsync(server, OtherEmail);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task OtherUsersSeeNothingAndCannotTouchForeignObjects()
    {
        var a = await VaultBrowser.OpenAsync(_account);
        var item = await a.Vault.CreateAsync("safeDeposit", new VaultPayload { Title = "Schließfach", Fields = { ["bank"] = "Volksbank" } });
        var file = await a.Vault.UploadAsync(item.Id, new MemoryStream([7, 7, 7]), new DocumentMetadata { FileName = "x.txt", Size = 3 });

        var b = await VaultBrowser.OpenAsync(_account, OtherEmail, NextOtherCode);
        Assert.Empty(b.Vault.Items);
        Assert.Empty(b.Vault.Files);
        Assert.Empty(await b.Api.GetAreasAsync(Ct));

        Assert.Equal(VaultErrors.NotFound, (await Assert.ThrowsAsync<VaultException>(() => b.Api.DeleteObjectAsync(item.Id, Ct))).Message);
        Assert.Equal(VaultErrors.NotFound, (await Assert.ThrowsAsync<VaultException>(() =>
            b.Api.UpdateObjectAsync(item.Id, new UpdateObjectRequest([1, 1, .. new byte[40]], 1, ObjectStatus.Active, 0), Ct))).Message);
        Assert.Equal(VaultErrors.NotFound, (await Assert.ThrowsAsync<VaultException>(() => b.Api.DownloadContentAsync(file.Id, Ct))).Message);
        Assert.Equal(VaultErrors.NotFound, (await Assert.ThrowsAsync<VaultException>(() =>
            b.Api.CreateDocumentAsync(new CreateDocumentRequest(Guid.NewGuid(), item.Id, [1, 2, .. new byte[60]], [1, 1, .. new byte[40]], 100), Ct))).Message);
        Assert.DoesNotContain((await b.Api.GetAuditAsync(0, 100, Ct)).Items, e => e.TargetId == item.Id);

        // Die ID ist vergeben; ein fremdes Objekt mit derselben ID lässt sich nicht anlegen.
        await b.Vault.CreateAsync("pet", new VaultPayload { Title = "Bello", Fields = { ["petName"] = "Bello" } });
        Assert.Equal(VaultErrors.Conflict, (await Assert.ThrowsAsync<VaultException>(() =>
            b.Vault.CreateAsync("pet", new VaultPayload { Title = "Kopie", Fields = { ["petName"] = "Kopie" } }, id: item.Id))).Message);

        Assert.False((await VaultBrowser.OpenAsync(_account)).Vault.Find(item.Id)!.IsDeleted);
    }

    private string NextOtherCode()
    {
        server.Clock.Advance(TimeSpan.FromSeconds(Totp.PeriodSeconds));
        return Totp.Compute(Base32.Decode(_otherSecret), Totp.StepAt(server.Clock.GetUtcNow()));
    }

    /// <summary>Legt ein weiteres Konto direkt in der Datenbank an (Einladungen folgen mit M2).</summary>
    internal static async Task<string> CreateUserAsync(ServerFixture server, string email)
    {
        await using var crypto = new ReferenceMyLifeCrypto();
        var setup = new VaultSetupService(crypto);
        var recoveryKey = await setup.CreateRecoveryKeyAsync();
        var request = await setup.CreateVaultAsync(new SetupAccount("Zweite", email, "de"), SetupTestSupport.Password, recoveryKey, "-", "-");
        var secret = Base32.Encode(Totp.NewSecret());
        await using var scope = server.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MyLifeDbContext>();
        var protection = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>();
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = "Zweite",
            Status = UserStatus.Active,
            Language = "de",
            AuthHash = await AuthHashHasher.HashAsync(request.AuthHash),
            KdfSalt = request.KdfSalt,
            KdfMemoryKiB = request.KdfMemoryKiB,
            KdfIterations = request.KdfIterations,
            KdfParallelism = request.KdfParallelism,
            WrappedVmk = request.WrappedVmk,
            WrappedVmkByRecovery = request.WrappedVmkByRecovery,
            PublicEncryptionKey = request.PublicEncryptionKey,
            WrappedEncryptionPrivateKey = request.WrappedEncryptionPrivateKey,
            PublicSigningKey = request.PublicSigningKey,
            WrappedSigningPrivateKey = request.WrappedSigningPrivateKey,
            SecurityStamp = SecurityStamps.New(),
            TotpSecret = protection.CreateProtector(SetupEndpoints.TotpSecretPurpose).Protect(secret),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return secret;
    }
}

/// <summary>Papierkorb nach 30 Tagen, abgebrochene Uploads nach einem Tag.</summary>
public sealed class TrashPurgeTests(ServerFixture server) : IClassFixture<ServerFixture>, IAsyncLifetime
{
    private AuthTestSupport _account = null!;

    public async ValueTask InitializeAsync() => _account = await AuthTestSupport.ForAsync(server);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task OldTrashAndAbandonedUploadsAreRemoved()
    {
        var a = await VaultBrowser.OpenAsync(_account);
        var keep = await a.Vault.CreateAsync("asset", new VaultPayload { Title = "Uhr", Fields = { ["assetKind"] = "jewelry" } });
        var trashed = await a.Vault.CreateAsync("asset", new VaultPayload { Title = "Alt", Fields = { ["assetKind"] = "art" } });
        var file = await a.Vault.UploadAsync(trashed.Id, new MemoryStream([1, 2, 3, 4]), new DocumentMetadata { FileName = "a.jpg", Size = 4 });
        await a.Vault.DeleteAsync(trashed.Id);
        var abandoned = Guid.NewGuid();
        await a.Api.CreateDocumentAsync(new CreateDocumentRequest(abandoned, keep.Id, [1, 2, .. new byte[60]], [1, 1, .. new byte[40]], 100), Ct);

        var purge = server.Services.GetRequiredService<TrashPurgeService>();
        server.Clock.Advance(TimeSpan.FromDays(2));
        await purge.PurgeOnceAsync(Ct);

        await using (var scope = server.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MyLifeDbContext>();
            Assert.False(await db.Documents.AnyAsync(d => d.Id == abandoned, Ct));
            Assert.True(await db.Objects.AnyAsync(o => o.Id == trashed.Id, Ct));
        }

        server.Clock.Advance(TimeSpan.FromDays(VaultLimits.TrashDays));
        await purge.PurgeOnceAsync(Ct);

        await using (var scope = server.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MyLifeDbContext>();
            Assert.False(await db.Objects.AnyAsync(o => o.Id == trashed.Id, Ct));
            Assert.False(await db.Documents.AnyAsync(d => d.Id == file.Id, Ct));
            Assert.True(await db.Objects.AnyAsync(o => o.Id == keep.Id, Ct));
            Assert.True(await db.AuditLog.AnyAsync(e => e.Action == AuditActions.ObjectPurged && e.TargetId == trashed.Id && e.Details == "auto", Ct));
        }
    }
}

/// <summary>JSON-Export und -Import (M1.9).</summary>
public sealed class ExportImportTests(ServerFixture server) : IClassFixture<ServerFixture>, IAsyncLifetime
{
    private AuthTestSupport _account = null!;

    public async ValueTask InitializeAsync() => _account = await AuthTestSupport.ForAsync(server);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task ExportRestoresWithPasswordOrRecoveryKey()
    {
        var a = await VaultBrowser.OpenAsync(_account);
        var contact = await a.Vault.CreateAsync("contact", new VaultPayload { Title = "Notar Muster", Fields = { ["name"] = "Muster" } });
        var will = await a.Vault.CreateAsync("legalDocument", new VaultPayload
        {
            Title = "Testament", OriginalLocation = "Notar", ContactId = contact.Id, Fields = { ["documentKind"] = "will" },
        });
        var document = RandomNumberGenerator.GetBytes(VaultLimits.ChunkSize + 99);
        await a.Vault.UploadAsync(will.Id, new MemoryStream(document), new DocumentMetadata { Label = "Scan", FileName = "testament.pdf", ContentType = "application/pdf", Size = document.Length });

        var export = new VaultExport(a.Api, a.Vault, a.Session, a.Crypto);
        var json = await export.ExportAsync(includeDocuments: true, DateTimeOffset.UtcNow);
        var text = Encoding.UTF8.GetString(json);
        Assert.Contains("\"format\": \"mylife-export\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Testament", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Notar", text, StringComparison.Ordinal);
        var file = VaultExport.Parse(json);
        Assert.Equal(AuthTestSupport.Email, file.Email);

        // Alles weg: löschen und endgültig entfernen.
        foreach (var item in a.Vault.Current.ToList())
        {
            await a.Vault.DeleteAsync(item.Id);
            await a.Vault.PurgeAsync(item.Id);
        }

        Assert.Equal(ClientVaultErrors.ImportWrongSecret, (await Assert.ThrowsAsync<VaultException>(() => export.ImportAsync(file, "falsch"))).Message);

        var result = await export.ImportAsync(file, SetupTestSupport.Password);
        Assert.Equal(file.Objects.Count, result.Objects);
        Assert.Equal(file.Documents.Count, result.Documents);
        Assert.Equal(0, result.Unreadable);

        var b = await VaultBrowser.OpenAsync(_account);
        var restored = b.Vault.Find(will.Id)!;
        Assert.Equal("Notar", restored.Payload.OriginalLocation);
        Assert.Equal(contact.Id, restored.Payload.ContactId);
        Assert.Equal(document, await b.Vault.DownloadAsync(b.Vault.FilesOf(will.Id).Single()));

        // Nochmals einspielen, diesmal mit den 24 Wörtern: alles schon da.
        var words = string.Join(' ', Bip39.Encode(_account.RecoveryKey));
        var again = await new VaultExport(b.Api, b.Vault, b.Session, b.Crypto).ImportAsync(file, words);
        Assert.Equal(0, again.Objects);
        Assert.Equal(file.Objects.Count, again.Skipped);
        Assert.Equal(0, again.Documents);
    }

    [Fact]
    public void RejectsForeignFiles()
    {
        Assert.Equal(ClientVaultErrors.ImportInvalid, Assert.Throws<VaultException>(() => VaultExport.Parse("{\"format\":\"anders\"}"u8.ToArray())).Message);
        Assert.Equal(ClientVaultErrors.ImportInvalid, Assert.Throws<VaultException>(() => VaultExport.Parse("kein json"u8.ToArray())).Message);
    }

    [Fact]
    public async Task EmergencyFolderPdfContainsTheEntries()
    {
        var a = await VaultBrowser.OpenAsync(_account);
        await a.Vault.CreateAsync("bankAccount", VaultSamples.BankAccount("Tagesgeld", "PDF"));
        var folder = new EmergencyFolder(a.Vault, VaultBrowser.Text(), LocalizerForTests(), new MyLife.Tests.Export.DotNetPdfCrypto());

        var pdf = await folder.CreatePdfAsync([1, 13], includeEnded: false, "Ordner-Passwort1", "Daniel", DateTimeOffset.UtcNow);

        var key = await MyLife.Tests.Export.PdfReader.OpenAsync(pdf, "Ordner-Passwort1");
        var shown = MyLife.Tests.Export.PdfReader.ShownText(MyLife.Tests.Export.PdfReader.Streams(pdf, key));
        Assert.Contains("Notfallordner", shown, StringComparison.Ordinal);
        Assert.Contains("Tagesgeld", shown, StringComparison.Ordinal);
        Assert.Contains("DE89 3704 0044 0532 0130 00", shown, StringComparison.Ordinal);
        Assert.Contains("Bankkonten & Depots", shown, StringComparison.Ordinal);
        Assert.Contains("auflösen", shown, StringComparison.Ordinal);
    }

    private static StringLocalizer<SharedResource> LocalizerForTests()
    {
        var factory = new ResourceManagerStringLocalizerFactory(Options.Create(new LocalizationOptions()), NullLoggerFactory.Instance);
        return new StringLocalizer<SharedResource>(factory);
    }
}
