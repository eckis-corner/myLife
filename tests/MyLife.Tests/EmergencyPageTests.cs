using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using MyLife.Client.Pages;
using MyLife.Client.Services;
using MyLife.Tests.Server;

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

    public async ValueTask InitializeAsync()
    {
        var browser = await VaultBrowser.OpenAsync(await AuthTestSupport.ForAsync(_server));
        Services.AddSingleton(new EmergencyClient(browser.Http));
        Render<MudBlazor.MudPopoverProvider>();
        Render<MudBlazor.MudDialogProvider>();
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
    public void AnUnknownLifesignLinkIsRejected()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("lifesign#unbekannt");
        var page = Render<EmergencyLink>();
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-testid=emergency-link-invalid]")));
    }
}
