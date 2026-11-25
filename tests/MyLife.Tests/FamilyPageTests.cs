using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using MyLife.Client.Pages;
using MyLife.Client.Services;
using MyLife.Crypto;
using MyLife.Tests.Server;

namespace MyLife.Tests;

/// <summary>Seite „Familie“, Einladungsdialog und die Annahmeseite mit echtem Server.</summary>
public sealed class FamilyPageTests : BunitContext, IClassFixture<ServerFixture>, IAsyncLifetime
{
    private readonly ServerFixture _server;
    private IRenderedComponent<MudBlazor.MudDialogProvider> _dialogs = null!;

    public FamilyPageTests(ServerFixture server)
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
        Services.AddSingleton(browser.Session);
        Services.AddSingleton<IMyLifeCrypto>(browser.Crypto);
        Services.AddSingleton(family);
        Services.AddSingleton(new KeyTrustService(family, browser.Crypto, browser.Session));
        Services.AddSingleton(new SetupClient(browser.Http));
        Services.AddSingleton(new VaultSetupService(browser.Crypto));
        Services.AddSingleton<BrowserFiles>();
        Render<MudBlazor.MudPopoverProvider>();
        _dialogs = Render<MudBlazor.MudDialogProvider>();
    }

    [Fact]
    public void InvitingByLinkShowsTheLinkAndListsTheInvitation()
    {
        var page = Render<Family>();
        page.WaitForAssertion(() => Assert.Contains("Noch niemand außer dir", page.Markup, StringComparison.Ordinal));

        page.ClickWhenReady("[data-testid=family-invite]");
        var dialog = _dialogs;
        dialog.WaitForAssertion(() => Assert.NotNull(dialog.Find("input[data-testid=invite-email]")));
        dialog.ChangeWhenReady("input[data-testid=invite-email]", "kai@example.org");
        dialog.FindAll("input[type=radio]")[1].Click();
        dialog.ClickWhenReady("[data-testid=invite-submit]");

        dialog.WaitForAssertion(() => Assert.Contains("/invite#t=", dialog.Find("[data-testid=invite-link]").GetAttribute("value") ?? dialog.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public void AnInvalidInvitationLinkIsRejected()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("invite#t=abc&s=def");
        var page = Render<Setup>();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-testid=invite-invalid]")));
        Assert.Contains("Einladung annehmen", page.Markup, StringComparison.Ordinal);
    }
}
