using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using MyLife.Client.Pages;
using MyLife.Client.Services;
using MyLife.Tests.Server;

namespace MyLife.Tests;

/// <summary>Seiten „Vertrauenspersonen“ und eine Vertrauensperson bearbeiten, mit echtem Server und eingeladener Person.</summary>
public sealed class TrustPageTests : BunitContext, IClassFixture<ServerFixture>, IAsyncLifetime
{
    private readonly ServerFixture _server;
    private KeyTrustService _keys = null!;
    private TrustService _trust = null!;

    public TrustPageTests(ServerFixture server)
    {
        _server = server;
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de");
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddLocalization();
    }

    public async ValueTask InitializeAsync()
    {
        var browser = await VaultBrowser.OpenAsync(await AuthTestSupport.ForAsync(_server));
        var family = new FamilyClient(browser.Http);
        _keys = new KeyTrustService(family, browser.Crypto, browser.Session);
        var link = await _keys.InviteAsync("ulla@example.org", "de", false, "https://localhost/");
        await FamilyTests.AcceptInvitationAsync(_server, link.Link, "Ulla");
        await _keys.SyncAsync();
        var trustApi = new TrustClient(browser.Http);
        _trust = new TrustService(trustApi, family, _keys, browser.Vault, browser.Crypto, browser.Session);
        Services.AddSingleton(browser.Session);
        Services.AddSingleton(browser.Vault);
        Services.AddSingleton<SchemaText>();
        Services.AddSingleton(_keys);
        Services.AddSingleton(_trust);
        Render<MudBlazor.MudPopoverProvider>();
        Render<MudBlazor.MudDialogProvider>();
    }

    [Fact]
    public async Task ATrusteeIsSavedFromTheMatrixAndListed()
    {
        var ulla = (await _keys.GetPeopleAsync()).Single(p => p.Person.Email == "ulla@example.org");
        var page = Render<TrusteePage>(p => p.Add(x => x.PersonId, ulla.Person.Id));
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-testid=grant-matrix]")));
        Assert.Contains("Verstorben", page.Find("[data-testid=grant-matrix]").TextContent, StringComparison.Ordinal);

        page.Find("input[data-testid=trustee-relationship]").Change("Schwester");
        page.Find("[data-testid=grant-10-Live] input, input[data-testid=grant-10-Live]").Change(true);
        page.Find("[data-testid=grant-4-Death] input, input[data-testid=grant-4-Death]").Change(true);
        page.Find("[data-testid=trustee-save]").Click();

        var navigation = Services.GetRequiredService<NavigationManager>();
        page.WaitForAssertion(() => Assert.EndsWith("/trust", navigation.Uri, StringComparison.Ordinal), TimeSpan.FromSeconds(10));
        var saved = (await _trust.GetTrusteesAsync()).Single();
        Assert.Equal("Schwester", saved.Profile.Relationship);
        Assert.Equal(2, saved.Grants.Count);

        var list = Render<TrustPage>();
        list.WaitForAssertion(() => Assert.Contains("Schwester", list.Find("[data-testid=trustees]").TextContent, StringComparison.Ordinal));
        Assert.Contains("Sofort teilen: 1", list.Markup, StringComparison.Ordinal);
    }
}
