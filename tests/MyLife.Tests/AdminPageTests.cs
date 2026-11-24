using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using MyLife.Client.Pages;
using MyLife.Client.Pages.Admin;
using MyLife.Client.Services;
using MyLife.Tests.Server;

namespace MyLife.Tests;

/// <summary>Verwaltungsseiten, Datenschutz und Kontolöschung mit echtem Server (angemeldet als Administrator).</summary>
public sealed class AdminPageTests : BunitContext, IClassFixture<ServerFixture>, IAsyncLifetime
{
    private readonly ServerFixture _server;

    public AdminPageTests(ServerFixture server)
    {
        _server = server;
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de");
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddLocalization();
    }

    public async ValueTask InitializeAsync()
    {
        var account = await AuthTestSupport.ForAsync(_server);
        var (session, _, http) = await account.LoginAsync();
        Services.AddSingleton(session);
        Services.AddSingleton(new AdminClient(http));
        Render<MudBlazor.MudPopoverProvider>();
    }

    [Fact]
    public void SettingsPageValidatesAndSaves()
    {
        var page = Render<AdminSettings>();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-testid=settings-save]")));
        Assert.Contains("Allgemein", page.Markup, StringComparison.Ordinal);
        Assert.Contains("Fristen", page.Markup, StringComparison.Ordinal);

        page.Find("input[data-testid=setting-public-url]").Change("keine adresse");
        page.Find("[data-testid=settings-save]").Click();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-testid=settings-errors]")));
        Assert.Contains("Keine gültige Adresse", page.Markup, StringComparison.Ordinal);

        page.Find("input[data-testid=setting-public-url]").Change("https://mylife.example.org");
        page.Find("[data-testid=settings-save]").Click();
        page.WaitForAssertion(() => Assert.Empty(page.FindAll("[data-testid=settings-errors]")));
    }

    [Fact]
    public void UsersPageListsAccountsWithoutActionsForOneself()
    {
        var page = Render<AdminUsers>();
        page.WaitForAssertion(() => Assert.Contains(AuthTestSupport.Email, page.Find("[data-testid=users-table]").TextContent, StringComparison.Ordinal));
        Assert.Contains("(du)", page.Markup, StringComparison.Ordinal);
        Assert.Contains("Administrator", page.Markup, StringComparison.Ordinal);
        Assert.Empty(page.FindAll($"[data-testid='user-actions-{AuthTestSupport.Email}']"));
    }

    [Fact]
    public void PrivacyPageShowsTheDefaultText()
    {
        var page = Render<Privacy>();
        page.WaitForAssertion(() => Assert.Contains("Was gespeichert wird", page.Find("[data-testid=privacy-text]").TextContent, StringComparison.Ordinal));
    }

    [Fact]
    public void DeletionPageRejectsUnknownLinks()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("account/delete#unbekannt");
        var page = Render<ConfirmDeletion>();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-testid=deletion-invalid]")));
    }
}
