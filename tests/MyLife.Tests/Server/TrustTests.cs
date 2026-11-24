using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyLife.Client.Export;
using MyLife.Client.Services;
using MyLife.Crypto;
using MyLife.Server.Data;
using MyLife.Shared.Trust;
using MyLife.Shared.Vault;
using static MyLife.Tests.Server.TestToken;

namespace MyLife.Tests.Server;

/// <summary>Vertrauenspersonen, Umschläge, „Sofort teilen“ und Briefe je Empfänger (M2.3), mit der Browser-Logik gegen den Server.</summary>
public sealed class TrustTests(ServerFixture server) : IClassFixture<ServerFixture>, IAsyncLifetime
{
    private AuthTestSupport _account = null!;

    public async ValueTask InitializeAsync() => _account = await AuthTestSupport.ForAsync(server);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task LiveSharingIsReadableOnlyWhileSharedAndEndsWithANewAreaKey()
    {
        var owner = await OwnerAsync();
        var lea = await InviteAsync(owner, "lea@example.org", "Lea");
        var max = await InviteAsync(owner, "max@example.org", "Max");
        var policy = await owner.Vault.Vault.CreateAsync("insurance", new VaultPayload { Title = "Haftpflicht", Fields = { ["insuranceKind"] = "liability", ["insurer"] = "HUK" } });
        var file = await owner.Vault.Vault.UploadAsync(policy.Id, new MemoryStream([4, 2, 4, 2]), new DocumentMetadata { FileName = "police.pdf", Size = 4 });

        await owner.Trust.SaveAsync(await PersonAsync(owner, lea), new TrusteeProfile("Tochter", "liest mit"), true,
            new HashSet<(int, GrantMode)> { (4, GrantMode.Live), (1, GrantMode.Death) }, new HashSet<(Guid, GrantMode)>(), null);
        await owner.Trust.SaveAsync(await PersonAsync(owner, max), new TrusteeProfile("Sohn", null), false,
            new HashSet<(int, GrantMode)> { (4, GrantMode.Death) }, new HashSet<(Guid, GrantMode)>(), null);

        var trustees = await owner.Trust.GetTrusteesAsync();
        var leaTrustee = trustees.Single(t => t.Person.Person.Email == "lea@example.org");
        Assert.Equal("Tochter", leaTrustee.Profile.Relationship);
        Assert.True(leaTrustee.CanRequestAccess);
        Assert.Equal(2, leaTrustee.Grants.Count);
        Assert.False(leaTrustee.Stale);

        // Lea sieht nur, was sofort geteilt ist, und kann es lesen; die Signatur des Eigentümers ist geprüft.
        var leaBrowser = await LoginAsync(lea);
        var shared = (await leaBrowser.Shared.GetOwnersAsync()).Single();
        var grant = Assert.Single(shared.Grants);
        Assert.True(grant.Verified);
        Assert.Equal(GrantMode.Live, grant.Dto.Mode);
        var content = await leaBrowser.Shared.OpenAsync(shared.OwnerId, grant.Dto);
        Assert.Contains(content.Items, i => i.Id == policy.Id && i.Title == "Haftpflicht");
        Assert.Equal([4, 2, 4, 2], await leaBrowser.Shared.DownloadAsync(content, content.Files.Single(f => f.Id == file.Id)));
        Assert.Contains((await owner.Vault.Api.GetAuditAsync(0, 50, Ct)).Items, e => e.Action == AuditActions.SharedRead && e.ActorName == "Lea");

        // Max hat nur einen Umschlag für den Todesfall: Den liefert der Server (noch) nicht aus.
        Assert.Empty(await (await LoginAsync(max)).Shared.GetOwnersAsync());

        // Teilen beenden: neuer Bereichsschlüssel, alles bleibt lesbar, Lea bekommt nichts mehr.
        var oldWrapped = owner.Vault.Vault.WrappedAreaKey(4);
        await owner.Trust.SaveAsync(await PersonAsync(owner, lea), leaTrustee.Profile, true,
            new HashSet<(int, GrantMode)> { (1, GrantMode.Death) }, new HashSet<(Guid, GrantMode)>(), leaTrustee);
        Assert.False(owner.Vault.Vault.WrappedAreaKey(4)!.AsSpan().SequenceEqual(oldWrapped));
        Assert.Empty(await leaBrowser.Shared.GetOwnersAsync());
        await Assert.ThrowsAsync<TrustException>(() => leaBrowser.TrustApi.GetSharedAreaAsync(shared.OwnerId, 4, Ct));

        var reopened = await VaultBrowser.OpenAsync(_account);
        Assert.Equal("Haftpflicht", reopened.Vault.Find(policy.Id)!.Title);
        Assert.Equal([4, 2, 4, 2], await reopened.Vault.DownloadAsync(reopened.Vault.Files.Single(f => f.Id == file.Id)));

        // Der Umschlag von Max wurde für den neuen Schlüssel neu versiegelt und öffnet den Bereich.
        var maxBrowser = await LoginAsync(max);
        await using var scope = server.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MyLifeDbContext>();
        var maxGrant = await db.Grants.AsNoTracking().SingleAsync(g => g.TrusteeId == maxBrowser.Session.Session!.UserId && g.AreaKey == 4, Ct);
        var areaKey = await maxBrowser.Crypto.OpenSealedKeyAsync(maxBrowser.Session.EncryptionKey!.Value, maxGrant.Envelope, KeyContexts.AreaEnvelope(shared.OwnerId, 4));
        var payload = (await db.Objects.AsNoTracking().SingleAsync(o => o.Id == policy.Id, Ct)).Payload;
        Assert.Equal("Haftpflicht", VaultPayload.FromUtf8(await maxBrowser.Crypto.DecryptAsync(areaKey, payload, KeyContexts.VaultObject("insurance", policy.Id))).Title);
    }

    [Fact]
    public async Task LettersHaveTheirOwnKeyAndGoOnlyToTheirRecipient()
    {
        var owner = await OwnerAsync();
        var nora = await InviteAsync(owner, "nora@example.org", "Nora");
        var letter = await owner.Vault.Vault.CreateAsync("letter", new VaultPayload { Title = "Für Nora", Fields = { ["recipients"] = "Nora", ["letterText"] = "Liebe Nora …" } });
        Assert.NotNull(letter.WrappedKey);
        var scan = await owner.Vault.Vault.UploadAsync(letter.Id, new MemoryStream([9, 9]), new DocumentMetadata { FileName = "brief.jpg", Size = 2 });
        Assert.Equal([9, 9], await owner.Vault.Vault.DownloadAsync(scan));

        var person = await PersonAsync(owner, nora);
        await Assert.ThrowsAsync<TrustException>(() => owner.Trust.SaveAsync(person, new TrusteeProfile("Enkelin", null), false,
            new HashSet<(int, GrantMode)>(), new HashSet<(Guid, GrantMode)> { (letter.Id, GrantMode.Live) }, null));
        await owner.Trust.SaveAsync(person, new TrusteeProfile("Enkelin", null), false,
            new HashSet<(int, GrantMode)>(), new HashSet<(Guid, GrantMode)> { (letter.Id, GrantMode.Death) }, null);

        // Der Umschlag öffnet nur den Brief, nicht den Bereich 11, und auch den Scan.
        var noraBrowser = await LoginAsync(nora);
        await using var scope = server.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MyLifeDbContext>();
        var grant = await db.Grants.AsNoTracking().SingleAsync(g => g.ObjectId == letter.Id, Ct);
        var letterKey = await noraBrowser.Crypto.OpenSealedKeyAsync(noraBrowser.Session.EncryptionKey!.Value, grant.Envelope, KeyContexts.LetterEnvelope(owner.Vault.Session.Session!.UserId, letter.Id));
        var stored = await db.Objects.AsNoTracking().SingleAsync(o => o.Id == letter.Id, Ct);
        Assert.Equal("Liebe Nora …", VaultPayload.FromUtf8(await noraBrowser.Crypto.DecryptAsync(letterKey, stored.Payload, KeyContexts.VaultObject("letter", letter.Id))).Get("letterText"));
        var document = await db.Documents.AsNoTracking().SingleAsync(d => d.Id == scan.Id, Ct);
        await noraBrowser.Crypto.UnwrapKeyAsync(letterKey, document.WrappedFileKey, KeyContexts.FileKey(scan.Id));
        await Assert.ThrowsAnyAsync<Exception>(() => noraBrowser.Crypto.DecryptAsync(letterKey, stored.Payload, KeyContexts.VaultObject("letter", Guid.NewGuid())));
        Assert.Empty(await noraBrowser.Shared.GetOwnersAsync());

        // Ein Wechsel des Bereichsschlüssels packt nur den Briefschlüssel um; Inhalt und Scan bleiben lesbar.
        await owner.Trust.RotateAsync(11);
        var reopened = await VaultBrowser.OpenAsync(_account);
        Assert.Equal("Liebe Nora …", reopened.Vault.Find(letter.Id)!.Payload.Get("letterText"));
        Assert.Equal([9, 9], await reopened.Vault.DownloadAsync(reopened.Vault.Files.Single(f => f.Id == scan.Id)));
        var after = await db.Grants.AsNoTracking().SingleAsync(g => g.ObjectId == letter.Id, Ct);
        Assert.Equal(grant.Envelope, after.Envelope);
    }

    [Fact]
    public async Task ChangedKeysGetNoEnvelopesAndUnconfirmedOnesArePinned()
    {
        var owner = await OwnerAsync();
        var otto = await InviteAsync(owner, "otto@example.org", "Otto");
        await using (var scope = server.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MyLifeDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == "otto@example.org", Ct);
            user.PublicEncryptionKey = (await db.Users.SingleAsync(u => u.Email == AuthTestSupport.Email, Ct)).PublicEncryptionKey;
            await db.SaveChangesAsync(Ct);
        }

        var changed = await PersonAsync(owner, otto);
        Assert.Equal(TrustState.Changed, changed.State);
        Assert.Equal(TrustErrors.KeyChanged, (await Assert.ThrowsAsync<TrustException>(() => owner.Trust.SaveAsync(changed, new TrusteeProfile("Bruder", null), false,
            new HashSet<(int, GrantMode)> { (4, GrantMode.Death) }, new HashSet<(Guid, GrantMode)>(), null))).Message);

        // Ein veralteter Fingerprint wird vom Server abgelehnt.
        await owner.Vault.Vault.CreateAsync("insurance", new VaultPayload { Title = "Hausrat", Fields = { ["insuranceKind"] = "household", ["insurer"] = "Allianz" } });
        var stale = new SaveTrusteeRequest([1, 2, 3], false, [new GrantRequest(GrantKind.Area, 4, null, GrantMode.Death, [1], new byte[64], new byte[32])]);
        Assert.Equal(TrustErrors.KeyChanged, (await Assert.ThrowsAsync<TrustException>(() => owner.TrustApi.SaveTrusteeAsync(changed.Person.Id, stale, Ct))).Message);

        // Jemand ohne Einladung ist noch unbestätigt; beim Anlegen merkt sich der Browser den Schlüssel.
        await VaultIsolationTests.CreateUserAsync(server, "paula@example.org");
        var paula = (await owner.Keys.GetPeopleAsync()).Single(p => p.Person.Email == "paula@example.org");
        Assert.Equal(TrustState.Unconfirmed, paula.State);
        await owner.Trust.SaveAsync(paula, new TrusteeProfile("Nachbarin", null), false,
            new HashSet<(int, GrantMode)> { (10, GrantMode.Incapacity) }, new HashSet<(Guid, GrantMode)>(), null);
        Assert.Equal(TrustState.Pinned, (await owner.Keys.GetPeopleAsync()).Single(p => p.Person.Email == "paula@example.org").State);
    }

    [Fact]
    public async Task ForgedEnvelopesAreNotVerifiedAndRemovingATrusteeEndsSharing()
    {
        var owner = await OwnerAsync();
        var rita = await InviteAsync(owner, "rita@example.org", "Rita");
        await owner.Vault.Vault.CreateAsync("contact", new VaultPayload { Title = "Hausarzt", Fields = { ["contactName"] = "Dr. Muster" } });
        await owner.Trust.SaveAsync(await PersonAsync(owner, rita), new TrusteeProfile("Schwester", null), false,
            new HashSet<(int, GrantMode)> { (10, GrantMode.Live) }, new HashSet<(Guid, GrantMode)>(), null);

        var ritaBrowser = await LoginAsync(rita);
        Assert.True((await ritaBrowser.Shared.GetOwnersAsync()).Single().Grants.Single().Verified);

        await using (var scope = server.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MyLifeDbContext>();
            var grant = await db.Grants.SingleAsync(g => g.TrusteeId == ritaBrowser.Session.Session!.UserId, Ct);
            grant.Signature = new byte[64];
            await db.SaveChangesAsync(Ct);
        }

        Assert.False((await ritaBrowser.Shared.GetOwnersAsync()).Single().Grants.Single().Verified);

        var trustee = (await owner.Trust.GetTrusteesAsync()).Single(t => t.Person.Person.Email == "rita@example.org");
        await owner.Trust.RemoveAsync(trustee);
        Assert.Empty(await ritaBrowser.Shared.GetOwnersAsync());
        Assert.DoesNotContain(await owner.Trust.GetTrusteesAsync(), t => t.Person.Person.Email == "rita@example.org");
        Assert.Contains((await owner.Vault.Api.GetAuditAsync(0, 50, Ct)).Items, e => e.Action == AuditActions.AreaKeyRotated);
    }

    [Fact]
    public async Task ExportAndImportKeepLettersReadable()
    {
        var owner = await OwnerAsync();
        var letter = await owner.Vault.Vault.CreateAsync("letter", new VaultPayload { Title = "An alle", Fields = { ["recipients"] = "Familie", ["letterText"] = "Danke." } });
        await owner.Vault.Vault.UploadAsync(letter.Id, new MemoryStream([5]), new DocumentMetadata { FileName = "foto.jpg", Size = 1 });
        var export = new VaultExport(owner.Vault.Api, owner.Vault.Vault, owner.Vault.Session, owner.Crypto);
        var file = VaultExport.Parse(await export.ExportAsync(true, DateTimeOffset.UtcNow));
        Assert.NotNull(file.Objects.Single(o => o.Id == letter.Id).WrappedKey);

        // IDs sind instanzweit eindeutig: Brief endgültig löschen, dann aus dem Export zurückholen.
        await owner.Vault.Vault.DeleteAsync(letter.Id);
        await owner.Vault.Vault.PurgeAsync(letter.Id);
        var result = await export.ImportAsync(file, SetupTestSupport.Password);
        Assert.True(result.Objects >= 1);
        Assert.Equal(0, result.Unreadable);
        var imported = owner.Vault.Vault.Find(letter.Id)!;
        Assert.NotNull(imported.WrappedKey);
        Assert.Equal("Danke.", imported.Payload.Get("letterText"));
        Assert.Equal([5], await owner.Vault.Vault.DownloadAsync(owner.Vault.Vault.FilesOf(letter.Id).Single()));
    }

    private sealed record Browser(VaultBrowser Vault, FamilyClient Family, KeyTrustService Keys, TrustClient TrustApi, TrustService Trust, SharedService Shared)
    {
        public ReferenceMyLifeCrypto Crypto => Vault.Crypto;

        public SessionService Session => Vault.Session;
    }

    private sealed record Invited(string Email, string TotpSecret);

    private async Task<Browser> OwnerAsync() => Wrap(await VaultBrowser.OpenAsync(_account));

    private async Task<Browser> LoginAsync(Invited person) =>
        Wrap(await VaultBrowser.OpenAsync(_account, person.Email, () => FamilyTests.Code(server, person.TotpSecret)));

    /// <summary>Einladen, annehmen und prüfen: danach ist die Person beim Eigentümer gepinnt und umgekehrt.</summary>
    private async Task<Invited> InviteAsync(Browser owner, string email, string name)
    {
        var link = await owner.Keys.InviteAsync(email, "de", sendMail: false, "https://localhost/");
        var secret = await FamilyTests.AcceptInvitationAsync(server, link.Link, name);
        await owner.Keys.SyncAsync();
        return new Invited(email, secret);
    }

    private static async Task<PersonTrust> PersonAsync(Browser owner, Invited person) =>
        (await owner.Keys.GetPeopleAsync()).Single(p => p.Person.Email == person.Email);

    private static Browser Wrap(VaultBrowser vault)
    {
        var family = new FamilyClient(vault.Http);
        var keys = new KeyTrustService(family, vault.Crypto, vault.Session);
        var trustApi = new TrustClient(vault.Http);
        return new Browser(vault, family, keys, trustApi, new TrustService(trustApi, family, keys, vault.Vault, vault.Crypto, vault.Session),
            new SharedService(trustApi, keys, vault.Vault, vault.Crypto, vault.Session));
    }
}
