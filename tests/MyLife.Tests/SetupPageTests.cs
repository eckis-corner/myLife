using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using MyLife.Client.Pages;
using MyLife.Client.Services;
using MyLife.Crypto;
using MyLife.Shared.Setup;

namespace MyLife.Tests;

public sealed class SetupPageTests : BunitContext
{
    private readonly List<string> _requests = [];

    public SetupPageTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de");
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddLocalization();
        Services.AddSingleton<IMyLifeCrypto, ReferenceMyLifeCrypto>();
        Services.AddSingleton<VaultSetupService>();
        Services.AddSingleton(new FamilyClient(new HttpClient { BaseAddress = new Uri("https://localhost") }));
    }

    [Fact]
    public void ShowsHintWhenAlreadySetUp()
    {
        UseServer(setupRequired: false);

        var page = Render<Setup>();

        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-testid=setup-not-required]")));
    }

    [Fact]
    public void WrongTokenKeepsTheFirstStepAndShowsTheReason()
    {
        UseServer(setupRequired: true, tokenStatus: HttpStatusCode.Forbidden);
        var page = Render<Setup>();
        page.WaitForAssertion(() => Assert.Equal("Willkommen", page.Find("[data-testid=setup-step-title]").TextContent));

        page.Find("input[data-testid=setup-token]").Input("FALSCH");
        page.Find("[data-testid=setup-next]").Click();

        page.WaitForAssertion(() => Assert.Contains("Der Einrichtungscode stimmt nicht", page.Find("[data-testid=setup-error]").TextContent, StringComparison.Ordinal));
        Assert.Equal("Willkommen", page.Find("[data-testid=setup-step-title]").TextContent);
        Assert.Contains("/api/setup/token", _requests);
    }

    [Fact]
    public void CorrectTokenLeadsToTheAccountStep()
    {
        UseServer(setupRequired: true, tokenStatus: HttpStatusCode.NoContent);
        var page = Render<Setup>();
        page.WaitForAssertion(() => page.Find("input[data-testid=setup-token]"));

        page.Find("input[data-testid=setup-token]").Input("ABCD-EFGH-JKLM-NPQR-STUV");
        page.Find("[data-testid=setup-next]").Click();

        page.WaitForAssertion(() => Assert.Equal("Dein Konto", page.Find("[data-testid=setup-step-title]").TextContent));
    }

    private void UseServer(bool setupRequired, HttpStatusCode tokenStatus = HttpStatusCode.NoContent)
    {
        var http = new HttpClient(new StubHandler(this, setupRequired, tokenStatus)) { BaseAddress = new Uri("http://localhost/") };
        Services.AddSingleton(new SetupClient(http));
    }

    private sealed class StubHandler(SetupPageTests test, bool setupRequired, HttpStatusCode tokenStatus) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            test._requests.Add(path);
            var response = path switch
            {
                "/api/setup/status" => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new SetupStatusDto(setupRequired)) },
                "/api/setup/token" when tokenStatus == HttpStatusCode.Forbidden => new HttpResponseMessage(tokenStatus)
                {
                    Content = JsonContent.Create(new { title = "setup-invalid-token", status = 403 }),
                },
                "/api/setup/token" => new HttpResponseMessage(tokenStatus),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            };
            return Task.FromResult(response);
        }
    }
}
