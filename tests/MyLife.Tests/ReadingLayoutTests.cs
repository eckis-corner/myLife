using System.Globalization;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using MyLife.Client.Layout;
using MyLife.Client.Services;
using MyLife.Crypto;

namespace MyLife.Tests;

/// <summary>Rahmen des Lesemodus: Er muss sich jederzeit wieder verlassen lassen.</summary>
public sealed class ReadingLayoutTests : BunitContext
{
    public ReadingLayoutTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de");
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddLocalization();
        Services.AddSingleton(new SessionService(new AuthClient(new HttpClient()), new ReferenceMyLifeCrypto(), new WebAuthnService(JSInterop.JSRuntime)));
    }

    /// <summary>„Lesemodus verlassen“ ist ein echter Link zur Startseite (ein leeres Href ergibt bei MudButton einen wirkungslosen Knopf).</summary>
    [Fact]
    public void LeavingLinksBackToTheNormalView()
    {
        var layout = Render<ReadingLayout>();
        var leave = layout.Find("[data-testid=reading-leave]");
        Assert.Equal("A", leave.TagName, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("./", leave.GetAttribute("href"));
    }
}
