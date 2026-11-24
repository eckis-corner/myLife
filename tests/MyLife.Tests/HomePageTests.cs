using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using MyLife.Client.Pages;
using MyLife.Client.Services;
using MyLife.Shared;
using MyLife.Shared.Setup;

namespace MyLife.Tests;

public sealed class HomePageTests : BunitContext
{
    public HomePageTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de");
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddLocalization();
    }

    [Fact]
    public void ShowsVersionWhenServerIsReachable()
    {
        UseServer(HttpStatusCode.OK, new AppInfoDto("1.2.3", 1));

        var page = Render<Home>();

        page.WaitForAssertion(() => Assert.Equal("1.2.3", page.Find("[data-testid=app-version]").TextContent));
        Assert.Contains("Willkommen bei myLife", page.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void ShowsWarningWhenServerIsUnreachable()
    {
        UseServer(HttpStatusCode.ServiceUnavailable, null);

        var page = Render<Home>();

        page.WaitForAssertion(() =>
            Assert.Contains("Der Server ist nicht erreichbar", page.Markup, StringComparison.Ordinal));
    }

    private void UseServer(HttpStatusCode status, AppInfoDto? body, bool setupRequired = false)
    {
        var http = new HttpClient(new StubHandler(status, body, setupRequired)) { BaseAddress = new Uri("http://localhost/") };
        Services.AddSingleton(new AppInfoClient(http));
        Services.AddSingleton(new SetupClient(http));
    }

    private sealed class StubHandler(HttpStatusCode status, AppInfoDto? body, bool setupRequired) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath == "/api/setup/status")
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new SetupStatusDto(setupRequired)) });
            }

            var response = new HttpResponseMessage(status);
            if (body is not null)
            {
                response.Content = JsonContent.Create(body);
            }

            return Task.FromResult(response);
        }
    }
}
