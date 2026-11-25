using System.Globalization;
using Bunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using MyLife.Client.Pages.Reading;
using MyLife.Client.Services;
using MyLife.Server.Data;
using MyLife.Shared.Emergency;
using MyLife.Shared.Trust;
using MyLife.Shared.Vault;
using MyLife.Tests.Server;
using static MyLife.Tests.Server.TestToken;

namespace MyLife.Tests;

/// <summary>
/// Lesemodus (M2.5): Eine Person hat dem Admin-Konto Bereiche und einen Brief für den Todesfall freigegeben; die
/// Freigabe selbst ist in den Tests zum Notfallzugang geprüft und wird hier direkt gesetzt.
/// </summary>
public sealed class ReadingTests : BunitContext, IClassFixture<ServerFixture>, IAsyncLifetime
{
    private readonly ServerFixture _server;
    private VaultBrowser _reader = null!;
    private ReadingService _reading = null!;
    private Guid _ownerId;

    public ReadingTests(ServerFixture server)
    {
        _server = server;
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de");
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddLocalization();
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ServerFixture, Task<Guid>> Owners = [];

    public async ValueTask InitializeAsync()
    {
        var account = await AuthTestSupport.ForAsync(_server);
        _ownerId = await Owners.GetValue(_server, s => SetUpOwnerAsync(s, account));
        _reader = await VaultBrowser.OpenAsync(account);
        var readerKeys = new KeyTrustService(new FamilyClient(_reader.Http), _reader.Crypto, _reader.Session);
        var shared = new SharedService(new TrustClient(_reader.Http), readerKeys, _reader.Vault, _reader.Crypto, _reader.Session);
        _reading = new ReadingService(shared);
        Services.AddSingleton(_reading);
        Services.AddSingleton(new ChecklistClient(_reader.Http));
        Services.AddSingleton<SchemaText>();
        Services.AddSingleton<BrowserFiles>();
        Render<MudBlazor.MudPopoverProvider>();
        Render<MudBlazor.MudDialogProvider>();
    }

    /// <summary>Einmal je Server: Die Person wird vom Admin eingeladen, gibt frei und ist danach „verstorben“ freigegeben.</summary>
    private static async Task<Guid> SetUpOwnerAsync(ServerFixture server, AuthTestSupport account)
    {
        var admin = await VaultBrowser.OpenAsync(account);
        var readerKeys = new KeyTrustService(new FamilyClient(admin.Http), admin.Crypto, admin.Session);

        // Die Person wird vom Admin eingeladen; beide Schlüssel sind danach gegenseitig gepinnt.
        var link = await readerKeys.InviteAsync("vera@example.org", "de", false, "https://localhost/");
        var secret = await FamilyTests.AcceptInvitationAsync(server, link.Link, "Vera");
        await readerKeys.SyncAsync();
        var owner = await VaultBrowser.OpenAsync(account, "vera@example.org", () => FamilyTests.Code(server, secret));
        var ownerId = owner.Session.Session!.UserId;
        var ownerFamily = new FamilyClient(owner.Http);
        var ownerKeys = new KeyTrustService(ownerFamily, owner.Crypto, owner.Session);
        var ownerTrust = new TrustService(new TrustClient(owner.Http), ownerFamily, ownerKeys, owner.Vault, owner.Crypto, owner.Session);

        await owner.Vault.CreateAsync("funeral", new VaultPayload { Title = "Bestattung", Fields = { ["place"] = "Friedwald" }, Notes = "Im Wald, bitte." });
        var letter = await owner.Vault.CreateAsync("letter", new VaultPayload { Title = "Für dich", Fields = { ["recipients"] = "Daniel", ["letterText"] = "Danke für alles." } });
        await owner.Vault.CreateAsync("insurance", new VaultPayload { Title = "Risikoleben", Fields = { ["insuranceKind"] = "termLife", ["insurer"] = "HUK" } });
        await owner.Vault.CreateAsync("bankAccount", new VaultPayload { Title = "Geheimes Konto", Fields = { ["bank"] = "X", ["iban"] = "DE89370400440532013000" } });
        await owner.Vault.CreateAsync("task", new VaultPayload
        {
            Title = "Versicherung melden",
            Fields = { ["checklistPhase"] = "first24h", ["taskText"] = "Sofort anrufen.", ["linkedArea"] = "insurance" },
        });

        var reader = (await ownerKeys.GetPeopleAsync()).Single(p => p.Person.Email == AuthTestSupport.Email);
        await ownerTrust.SaveAsync(reader, new TrusteeProfile("Bruder", null), false,
            new HashSet<(int, GrantMode)> { (12, GrantMode.Death), (11, GrantMode.Death), (4, GrantMode.Death), (1, GrantMode.Incapacity) },
            new HashSet<(Guid, GrantMode)> { (letter.Id, GrantMode.Death) }, null);

        await using (var scope = server.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MyLifeDbContext>();
            var state = await db.EmergencySwitches.SingleAsync(s => s.OwnerId == ownerId);
            state.ReleasedModes = ReleasedModes.Death;
            state.ReleasedAt = server.Clock.GetUtcNow();
            await db.SaveChangesAsync();
        }

        return ownerId;
    }

    [Fact]
    public async Task OnlyReleasedContentIsGuidedLettersFirstThenWishes()
    {
        var owner = Assert.Single(await _reading.GetOwnersAsync());
        Assert.Equal("Vera", owner.Name);

        var book = (await _reading.OpenAsync(_ownerId))!;
        Assert.True(book.Death);
        Assert.False(book.Incapacity);
        Assert.Equal(["letters", "area-12", "area-11", "area-4"], book.Steps.Select(s => s.Key));

        await _reading.LoadAsync(book, book.Steps[0]);
        Assert.Equal("Danke für alles.", Assert.Single(book.Steps[0].Items).Payload.Get("letterText"));

        // Der Bereich 11 enthält den Brief nicht noch einmal; das Konto (nur handlungsunfähig) ist nicht dabei.
        await _reading.LoadAsync(book, book.Steps[2]);
        Assert.Equal("Bestattung", Assert.Single(book.Steps[2].Items).Title);
        Assert.DoesNotContain(book.Steps.SelectMany(s => s.Items), i => i.Title == "Geheimes Konto");
        await _reading.CloseAsync(book);
    }

    [Fact]
    public void ThePageWalksThroughTheStepsWithLargeText()
    {
        var page = Render<ReadingPage>(p => p.Add(x => x.OwnerId, _ownerId));
        page.WaitForAssertion(() => Assert.Equal("Vera", page.Find("[data-testid=reading-owner-name]").TextContent));
        page.WaitForAssertion(() => Assert.Contains("Danke für alles.", page.Markup, StringComparison.Ordinal));
        Assert.Equal("Briefe an dich", page.Find("[data-testid=reading-step-title]").TextContent);

        // Die Checkliste führt: abhaken, und der verknüpfte Bereich lässt sich direkt aufschlagen.
        page.Find("[data-testid=reading-next]").Click();
        page.WaitForAssertion(() => Assert.Equal("Checkliste", page.Find("[data-testid=reading-step-title]").TextContent));
        page.WaitForAssertion(() => Assert.Contains("Versicherung melden", page.Find("[data-testid=checklist-task]").TextContent, StringComparison.Ordinal));
        Assert.Contains("Erste 24 Stunden", page.Markup, StringComparison.Ordinal);
        page.Find("input[data-testid=checklist-tick]").Change(true);
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-testid=checklist-done-by]")));
        page.Find("input[data-testid=checklist-tick]").Change(false);
        page.WaitForAssertion(() => Assert.Empty(page.FindAll("[data-testid=checklist-done-by]")));
        page.Find("[data-testid=checklist-open-area]").Click();
        page.WaitForAssertion(() => Assert.Equal("Versicherungen", page.Find("[data-testid=reading-step-title]").TextContent));
        page.Find("[data-testid=reading-previous]").Click();
        page.WaitForAssertion(() => Assert.Equal("Persönliche Wünsche", page.Find("[data-testid=reading-step-title]").TextContent));
        page.WaitForAssertion(() => Assert.Contains("Im Wald, bitte.", page.Markup, StringComparison.Ordinal));

        page.Find("[data-testid=reading-next]").Click();
        page.WaitForAssertion(() => Assert.Contains("Risikoleben", page.Markup, StringComparison.Ordinal));
        Assert.Contains("Fertig", page.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnlyTheOwnerAndTrusteesWithTheChecklistMayTickOff()
    {
        var api = new ChecklistClient(_reader.Http);
        var book = (await _reading.OpenAsync(_ownerId))!;
        await _reading.LoadAsync(book, book.Steps[1]);
        var task = Assert.Single(book.Steps[1].Items);
        await _reading.CloseAsync(book);

        var done = await api.DoneAsync(_ownerId, task.Id, Ct);
        Assert.Equal(task.Id, Assert.Single(await api.GetAsync(_ownerId, Ct)).TaskId);
        Assert.Equal(done.DoneAt, (await api.DoneAsync(_ownerId, task.Id, Ct)).DoneAt);

        // Nur Aufgaben der Checkliste lassen sich abhaken; Fremde sehen nichts.
        await Assert.ThrowsAsync<HttpRequestException>(() => api.DoneAsync(_ownerId, Guid.NewGuid(), Ct));
        var stranger = $"fremd{Guid.NewGuid():N}@example.org";
        var secret = await Server.VaultIsolationTests.CreateUserAsync(_server, stranger);
        var other = await VaultBrowser.OpenAsync(await AuthTestSupport.ForAsync(_server), stranger, () => FamilyTests.Code(_server, secret));
        Assert.Empty(await new ChecklistClient(other.Http).GetAsync(_ownerId, Ct));
        await Assert.ThrowsAsync<HttpRequestException>(() => new ChecklistClient(other.Http).DoneAsync(_ownerId, task.Id, Ct));

        await api.UndoneAsync(_ownerId, task.Id, Ct);
        Assert.Empty(await api.GetAsync(_ownerId, Ct));
    }

    [Fact]
    public void AnUnknownPersonShowsAQuietNotice()
    {
        var page = Render<ReadingPage>(p => p.Add(x => x.OwnerId, Guid.NewGuid()));
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-testid=reading-missing]")));
    }
}
