using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using MyLife.Client.Pages;
using MyLife.Client.Services;
using MyLife.Server.Data;
using MyLife.Shared.Emergency;
using MyLife.Shared.Trust;
using MyLife.Shared.Vault;
using MyLife.Tests.Server;
using static MyLife.Tests.Server.TestToken;

namespace MyLife.Tests;

/// <summary>Seite „Notfallzugang“ und die Links aus den Mails mit echtem Server.</summary>
public sealed class EmergencyPageTests : BunitContext, IClassFixture<ServerFixture>, IAsyncLifetime
{
    private readonly ServerFixture _server;

    public EmergencyPageTests(ServerFixture server)
    {
        _server = server;
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de");
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddLocalization();
    }

    private AuthTestSupport _account = null!;
    private IRenderedComponent<MudBlazor.MudDialogProvider> _dialogs = null!;

    public async ValueTask InitializeAsync()
    {
        _account = await AuthTestSupport.ForAsync(_server);
        var browser = await VaultBrowser.OpenAsync(_account);
        Services.AddSingleton(new EmergencyClient(browser.Http));
        var family = new FamilyClient(browser.Http);
        var keys = new KeyTrustService(family, browser.Crypto, browser.Session);
        Services.AddSingleton(new TrustService(new TrustClient(browser.Http), family, keys, browser.Vault, browser.Crypto, browser.Session));
        Render<MudBlazor.MudPopoverProvider>();
        _dialogs = Render<MudBlazor.MudDialogProvider>();
    }

    [Fact]
    public void WithoutTrusteesTheAccessIsNotActiveButALifesignCanBeGiven()
    {
        var page = Render<EmergencyPage>();
        page.WaitForAssertion(() => Assert.Contains("Nicht aktiv", page.Find("[data-testid=emergency-state]").TextContent, StringComparison.Ordinal));
        Assert.Contains("mindestens eine Vertrauensperson", page.Markup, StringComparison.Ordinal);

        page.Find("[data-testid=emergency-lifesign]").Click();
        page.WaitForAssertion(() => Assert.Contains(Services.GetRequiredService<MudBlazor.ISnackbar>().ShownSnackbars,
            bar => bar.Message?.ToString()?.Contains("Lebenszeichen ist gespeichert", StringComparison.Ordinal) == true));
    }

    [Fact]
    public async Task AfterDeathTheTrusteeCanAgreeToDeleteTheVault()
    {
        // Ruth (eingeladen vom Admin) gibt dem Admin Bereich 4 für den Todesfall frei; „verstorben“ ist freigegeben.
        var admin = await VaultBrowser.OpenAsync(_account);
        var adminKeys = new KeyTrustService(new FamilyClient(admin.Http), admin.Crypto, admin.Session);
        var link = await adminKeys.InviteAsync("ruth@example.org", "de", false, "https://localhost/");
        var secret = await FamilyTests.AcceptInvitationAsync(_server, link.Link, "Ruth");
        await adminKeys.SyncAsync();
        var ruth = await VaultBrowser.OpenAsync(_account, "ruth@example.org", () => FamilyTests.Code(_server, secret));
        var ruthFamily = new FamilyClient(ruth.Http);
        var ruthKeys = new KeyTrustService(ruthFamily, ruth.Crypto, ruth.Session);
        await ruth.Vault.CreateAsync("insurance", new VaultPayload { Title = "Hausrat", Fields = { ["insuranceKind"] = "household", ["insurer"] = "HUK" } });
        var reader = (await ruthKeys.GetPeopleAsync()).Single(p => p.Person.Email == AuthTestSupport.Email);
        await new TrustService(new TrustClient(ruth.Http), ruthFamily, ruthKeys, ruth.Vault, ruth.Crypto, ruth.Session).SaveAsync(
            reader, new TrusteeProfile("Bruder", null), false, new HashSet<(int, GrantMode)> { (4, GrantMode.Death) }, new HashSet<(Guid, GrantMode)>(), null);
        await using (var scope = _server.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MyLifeDbContext>();
            var state = await db.EmergencySwitches.SingleAsync(s => s.OwnerId == ruth.Session.Session!.UserId, Ct);
            state.ReleasedModes = ReleasedModes.Death;
            state.ReleasedAt = _server.Clock.GetUtcNow();
            await db.SaveChangesAsync(Ct);
        }

        var page = Render<EmergencyPage>();
        page.WaitForAssertion(() => Assert.Contains("Zugestimmt: niemand. Es fehlt noch:", page.Find("[data-testid=emergency-deletion-state]").TextContent, StringComparison.Ordinal));
        page.Find("[data-testid=emergency-deletion-agree]").Click();
        _dialogs.WaitForAssertion(() => Assert.Contains("lässt sich nicht rückgängig machen", _dialogs.Markup, StringComparison.Ordinal));
        _dialogs.FindAll("button").First(b => b.TextContent.Trim() == "Dem Löschen zustimmen").Click();
        page.WaitForAssertion(() => Assert.Contains("Alle haben zugestimmt", page.Find("[data-testid=emergency-deletion-state]").TextContent, StringComparison.Ordinal));
        Assert.NotNull(page.Find("[data-testid=emergency-deletion-withdraw]"));
    }

    [Fact]
    public void AnUnknownLifesignLinkIsRejected()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("lifesign#unbekannt");
        var page = Render<EmergencyLink>();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-testid=emergency-link-invalid]")));
    }
}
