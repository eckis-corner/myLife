using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyLife.Client.Services;
using MyLife.Server.Data;
using MyLife.Shared;
using MyLife.Shared.Vault;
using static MyLife.Tests.Server.TestToken;

namespace MyLife.Tests.Server;

/// <summary>myDocs als Attrappe: liefert den Stand im Brückenformat, Dokumente und nimmt die Quittung an.</summary>
internal sealed class FakeMyDocs : HttpMessageHandler
{
    public const string Url = "https://mydocs.example.test/";
    public const string Token = "mdl_testtoken";

    public MyDocsExport Export { get; set; } = Empty();

    public Dictionary<Guid, byte[]> Contents { get; } = [];

    public List<string> Confirmed { get; } = [];

    public static MyDocsExport Empty(params MyDocsObject[] objects) =>
        new(MyDocsExport.FormatName, 1, DateTimeOffset.UtcNow, Convert.ToHexStringLower(Guid.NewGuid().ToByteArray()).PadRight(64, '0'),
            new MyDocsInstance(Url), new MyDocsPerson(Guid.NewGuid(), "Bea"), objects);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Headers.Authorization?.ToString() != $"Bearer {Token}")
        {
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }

        var path = request.RequestUri!.AbsolutePath;
        if (request.Method == HttpMethod.Get && path == "/api/mylife/export")
        {
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Export, options: new JsonSerializerOptions(JsonSerializerDefaults.Web)) };
        }

        if (request.Method == HttpMethod.Get && path.StartsWith("/api/mylife/documents/", StringComparison.Ordinal)
            && Guid.TryParse(path.Split('/')[4], out var id) && Contents.TryGetValue(id, out var bytes))
        {
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
        }

        if (request.Method == HttpMethod.Post && path == "/api/mylife/synced" && request.Headers.Contains("X-MyDocs-Request"))
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            Confirmed.Add(body.RootElement.GetProperty("fingerprint").GetString()!);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    public static MyDocsObject Object(Guid id, string type, string title, Dictionary<string, string> fields, Dictionary<string, Guid>? locations = null,
        bool ended = false, DateOnly? overdue = null, params MyDocsDocument[] documents) =>
        new(id, type, 1, title, fields, locations ?? [], null, ended ? "ended" : "active", null, ended, new DateOnly(2026, 10, 1), overdue, DateTimeOffset.UtcNow, documents);
}

public sealed class MyDocsBridgeTests(ServerFixture server) : IClassFixture<ServerFixture>, IAsyncLifetime
{
    private AuthTestSupport _account = null!;

    public async ValueTask InitializeAsync() => _account = await AuthTestSupport.ForAsync(server);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static MyDocsBridge Bridge(VaultBrowser browser, FakeMyDocs fake) =>
        new(browser.Api, browser.Vault, browser.Crypto, browser.Session, () => fake);

    [Fact]
    public async Task TheConnectionIsStoredOnlyAsCiphertextAndTheCspAllowsMyDocs()
    {
        var browser = await VaultBrowser.OpenAsync(_account);
        var bridge = Bridge(browser, new FakeMyDocs());
        await bridge.ConnectAsync("https://mydocs.example.test", FakeMyDocs.Token);

        await using (var scope = server.Services.CreateAsyncScope())
        {
            var stored = await scope.ServiceProvider.GetRequiredService<MyLifeDbContext>().Users.AsNoTracking().Select(u => u.MyDocsBridge).SingleAsync(Ct);
            Assert.NotNull(stored);
            Assert.DoesNotContain(FakeMyDocs.Token, Encoding.UTF8.GetString(stored), StringComparison.Ordinal);
            Assert.DoesNotContain("mydocs.example", Encoding.UTF8.GetString(stored), StringComparison.Ordinal);
        }

        // Eine zweite Sitzung entschlüsselt die Verbindung mit dem Tresorschlüssel.
        var other = Bridge(await VaultBrowser.OpenAsync(_account), new FakeMyDocs());
        var settings = await other.LoadAsync();
        Assert.Equal(FakeMyDocs.Url, settings!.ServerUrl);
        Assert.Equal(FakeMyDocs.Token, settings.Token);

        using var anonymous = server.CreateClient();
        var response = await anonymous.GetAsync(new Uri("/api/info", UriKind.Relative), Ct);
        Assert.Contains("connect-src 'self' https://mydocs.example.test;", response.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
        Assert.Equal("https://mydocs.example.test/", (await response.Content.ReadFromJsonAsync<AppInfoDto>(Ct))!.MyDocsUrl);

        await other.DisconnectAsync();
        Assert.Null(await Bridge(await VaultBrowser.OpenAsync(_account), new FakeMyDocs()).LoadAsync());
    }

    [Fact]
    public async Task ANewStateIsComparedEncryptedAndConfirmed()
    {
        var browser = await VaultBrowser.OpenAsync(_account);
        var fake = new FakeMyDocs();
        var bridge = Bridge(browser, fake);
        await bridge.ConnectAsync(FakeMyDocs.Url, FakeMyDocs.Token);

        var marker = "Haftpflicht-" + Guid.NewGuid().ToString("N");
        var account = Guid.NewGuid();
        var shelf = Guid.NewGuid();
        var insurance = Guid.NewGuid();
        var policy = new MyDocsDocument(Guid.NewGuid(), new string('a', 64), "Police 2026", "police.pdf", "application/pdf", 8, new DateOnly(2026, 1, 15));
        fake.Contents[policy.Id] = Encoding.UTF8.GetBytes("%PDF-1.7");
        fake.Export = FakeMyDocs.Empty(
            FakeMyDocs.Object(account, "bankAccount", "Girokonto", new() { ["bank"] = "Sparkasse" }),
            FakeMyDocs.Object(shelf, "location", "Ordner Versicherungen", []),
            FakeMyDocs.Object(insurance, "insurance", marker, new() { ["insuranceKind"] = "liability", ["insurer"] = "HUK", ["paymentAccount"] = account.ToString() },
                overdue: new DateOnly(2026, 11, 1), documents: policy),
            FakeMyDocs.Object(Guid.NewGuid(), "unknownType", "Unbekannt", []));

        var plan = await bridge.CheckAsync(Ct);

        Assert.NotNull(plan);
        // Alle Tests der Klasse teilen sich einen Tresor: Verknüpfte Einträge anderer Tests erscheinen als „nicht mehr in myDocs“.
        Assert.Equal(3, plan.Changes.Count(c => c.Kind == MyDocsChangeKind.New));
        Assert.DoesNotContain(plan.Changes, c => c.Title == "Unbekannt");
        Assert.Single(plan.Changes.Single(c => c.Source?.Id == insurance).NewDocuments);

        await bridge.ApplyAsync(plan);

        Assert.Equal([fake.Export.Fingerprint], fake.Confirmed);
        var mine = browser.Vault.Current.Single(i => i.Payload.MyDocs?.Id == insurance);
        var myAccount = browser.Vault.Current.Single(i => i.Payload.MyDocs?.Id == account);
        Assert.Equal(marker, mine.Title);
        Assert.Equal(myAccount.Id.ToString(), mine.Payload.Get("paymentAccount"));
        Assert.Equal(new DateOnly(2026, 11, 1), mine.Payload.MyDocs!.OverdueSince);
        Assert.Equal(new DateOnly(2026, 10, 1), mine.Payload.ReviewedOn);
        var copy = Assert.Single(browser.Vault.FilesOf(mine.Id));
        Assert.Equal(policy.Id, copy.Metadata.MyDocsId);
        Assert.Equal("%PDF-1.7", Encoding.UTF8.GetString(await browser.Vault.DownloadAsync(copy)));

        // Der Server von myLife sieht nur Chiffrat.
        await using (var scope = server.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MyLifeDbContext>();
            var stored = await db.Objects.AsNoTracking().SingleAsync(o => o.Id == mine.Id, Ct);
            Assert.DoesNotContain(marker, Encoding.UTF8.GetString(stored.Payload), StringComparison.Ordinal);
            Assert.DoesNotContain("HUK", Encoding.UTF8.GetString(stored.Payload), StringComparison.Ordinal);
        }

        // Unverändert: nichts zu tun.
        Assert.True((await bridge.CheckAsync(Ct))!.IsEmpty);
    }

    [Fact]
    public async Task ChangesReplaceCopiesAndEntriesMissingInMyDocsAreUnlinkedOrTrashed()
    {
        var browser = await VaultBrowser.OpenAsync(_account);
        var fake = new FakeMyDocs();
        var bridge = Bridge(browser, fake);
        await bridge.ConnectAsync(FakeMyDocs.Url, FakeMyDocs.Token);
        var car = Guid.NewGuid();
        var mobile = Guid.NewGuid();
        var gym = Guid.NewGuid();
        var v1 = new MyDocsDocument(Guid.NewGuid(), new string('1', 64), "Police", "kfz.pdf", "application/pdf", 2, null);
        fake.Contents[v1.Id] = "v1"u8.ToArray();
        fake.Export = FakeMyDocs.Empty(
            FakeMyDocs.Object(car, "insurance", "KFZ", new() { ["insuranceKind"] = "car", ["insurer"] = "HUK" }, documents: v1),
            FakeMyDocs.Object(mobile, "contract", "Handy", new() { ["contractKind"] = "mobile", ["provider"] = "Telekom" }),
            FakeMyDocs.Object(gym, "contract", "Fitness", new() { ["contractKind"] = "other", ["provider"] = "Studio" }));
        await bridge.ApplyAsync((await bridge.CheckAsync(Ct))!);

        // Eigene Angaben in myLife bleiben beim Abgleich erhalten.
        var carItem = browser.Vault.Current.Single(i => i.Payload.MyDocs?.Id == car);
        var own = carItem.Payload.Clone();
        own.Wish = "cancel";
        own.Notes = "Notiz aus myLife";
        await browser.Vault.UpdateAsync(carItem, own, carItem.Status);

        var v2 = v1 with { Hash = new string('2', 64), Size = 2 };
        fake.Contents[v2.Id] = "v2"u8.ToArray();
        fake.Export = FakeMyDocs.Empty(
            FakeMyDocs.Object(car, "insurance", "KFZ", new() { ["insuranceKind"] = "car", ["insurer"] = "Allianz" }, ended: true, documents: v2));

        var plan = (await bridge.CheckAsync(Ct))!;
        var changed = plan.Changes.Single(c => c.Kind == MyDocsChangeKind.Changed);
        Assert.Contains("insurer", changed.Fields);
        Assert.Contains("status", changed.Fields);
        Assert.Single(changed.ChangedDocuments);
        Assert.Equal(2, plan.Changes.Count(c => c.Kind == MyDocsChangeKind.Removed));
        plan.Changes.Single(c => c.Item?.Payload.MyDocs?.Id == gym).Removal = MyDocsRemoval.Trash;

        await bridge.ApplyAsync(plan);

        carItem = browser.Vault.Find(carItem.Id)!;
        Assert.Equal("Allianz", carItem.Payload.Get("insurer"));
        Assert.Equal(ObjectStatus.Ended, carItem.Status);
        Assert.Equal("cancel", carItem.Payload.Wish);
        Assert.Equal("Notiz aus myLife", carItem.Payload.Notes);
        var copy = Assert.Single(browser.Vault.FilesOf(carItem.Id), f => !f.IsDeleted);
        Assert.Equal("v2", Encoding.UTF8.GetString(await browser.Vault.DownloadAsync(copy)));
        Assert.Contains(browser.Vault.FilesOf(carItem.Id), f => f.IsDeleted);
        var handy = browser.Vault.Items.Single(i => i.Title == "Handy");
        Assert.Null(handy.Payload.MyDocs);
        Assert.False(handy.IsDeleted);
        Assert.True(browser.Vault.Items.Single(i => i.Title == "Fitness").IsDeleted);
        Assert.Equal(2, fake.Confirmed.Count);
    }

    [Fact]
    public void FieldsFromMyDocsStayReadOnlyWhenSaving()
    {
        var original = new VaultPayload
        {
            Title = "KFZ", Fields = { ["insurer"] = "HUK" }, ExpiresOn = new DateOnly(2027, 1, 1), MyDocs = new MyDocsLink { Id = Guid.NewGuid() },
            Locations = { ["policyLocation"] = Guid.NewGuid() },
        };
        var draft = original.Clone();
        draft.Title = "geändert";
        draft.Fields["insurer"] = "anders";
        draft.ExpiresOn = null;
        draft.Notes = "eigene Notiz";
        var shelf = Guid.NewGuid();
        draft.Locations[VaultPayload.OriginalLocationKey] = shelf;

        MyDocsBridge.KeepManaged(original, draft);

        Assert.Equal("KFZ", draft.Title);
        Assert.Equal("HUK", draft.Get("insurer"));
        Assert.Equal(new DateOnly(2027, 1, 1), draft.ExpiresOn);
        Assert.Equal("eigene Notiz", draft.Notes);
        Assert.Equal(shelf, draft.Locations[VaultPayload.OriginalLocationKey]);
        Assert.Equal(original.Locations["policyLocation"], draft.Locations["policyLocation"]);
    }

    [Fact]
    public async Task ANewerFormatOrAWrongTokenIsReportedWithoutChangingAnything()
    {
        var browser = await VaultBrowser.OpenAsync(_account);
        var fake = new FakeMyDocs { Export = FakeMyDocs.Empty() with { Version = 2 } };
        var bridge = Bridge(browser, fake);
        await bridge.ConnectAsync(FakeMyDocs.Url, FakeMyDocs.Token);

        Assert.Null(await bridge.CheckAsync(Ct));
        Assert.Equal(MyDocsErrors.Format, bridge.Error);

        await bridge.ConnectAsync(FakeMyDocs.Url, "mdl_falsch");
        Assert.Null(await bridge.CheckAsync(Ct));
        Assert.Equal(MyDocsErrors.Token, bridge.Error);
        await Assert.ThrowsAsync<MyDocsException>(() => bridge.ConnectAsync("ftp://mydocs", FakeMyDocs.Token));
    }
}
