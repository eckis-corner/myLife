using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using MyLife.Client.Pages;
using MyLife.Client.Services;
using MyLife.Crypto;
using MyLife.Shared.Auth;
using MyLife.Shared.Setup;

namespace MyLife.Tests;

public sealed class LoginPageTests : BunitContext
{
    public LoginPageTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de");
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddLocalization();
        Services.AddSingleton<IMyLifeCrypto, ReferenceMyLifeCrypto>();
        Services.AddSingleton<WebAuthnService>();
        Services.AddSingleton<SessionService>();
    }

    [Fact]
    public void RedirectsToSetupWhenInstanceIsNotSetUp()
    {
        UseServer(setupRequired: true, session: null);

        Render<Login>();

        Assert.EndsWith("/setup", Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);
    }

    [Fact]
    public void ShowsEmailAndPasswordWhenNobodyIsSignedIn()
    {
        UseServer(setupRequired: false, session: null);

        var page = Render<Login>();

        page.WaitForAssertion(() => Assert.Equal("Anmelden", page.Find("[data-testid=login-title]").TextContent));
        Assert.NotNull(page.Find("input[data-testid=login-email]"));
        Assert.NotNull(page.Find("input[data-testid=login-password]"));
    }

    [Fact]
    public void OffersUnlockWhenTheSessionSurvivedAReload()
    {
        UseServer(setupRequired: false, session: new SessionDto
        {
            UserId = Guid.NewGuid(), Email = "daniel@example.org", DisplayName = "Daniel", IsAdmin = true, Language = "de",
            IdleTimeoutMinutes = 15, KdfSalt = new byte[16], KdfMemoryKiB = 65536, KdfIterations = 3, KdfParallelism = 1,
            WrappedVmk = [], PublicEncryptionKey = [], WrappedEncryptionPrivateKey = [], PublicSigningKey = [], WrappedSigningPrivateKey = [],
        });

        var page = Render<Login>();

        page.WaitForAssertion(() => Assert.Equal("Tresor entsperren", page.Find("[data-testid=login-title]").TextContent));
        Assert.Contains("Hallo Daniel", page.Markup, StringComparison.Ordinal);
        Assert.Empty(page.FindAll("input[data-testid=login-email]"));
    }

    [Fact]
    public void RecoverPageRendersItsFields()
    {
        UseServer(setupRequired: false, session: null);
        Services.AddSingleton<RecoveryService>();

        var page = Render<Recover>();

        Assert.NotNull(page.Find("textarea[data-testid=recover-words]"));
        Assert.NotNull(page.Find("input[data-testid=new-password]"));
        Assert.NotNull(page.Find("input[data-testid=new-password-repeat]"));
    }

    [Fact]
    public void SecurityPageRendersPasswordFormAndPasskeys()
    {
        UseServer(setupRequired: false, session: null);
        Render<MudBlazor.MudPopoverProvider>();

        var page = Render<Security>();

        page.WaitForAssertion(() => Assert.Contains("Noch kein Passkey eingerichtet.", page.Markup, StringComparison.Ordinal));
        Assert.NotNull(page.Find("input[data-testid=current-password]"));
        Assert.NotNull(page.Find("input[data-testid=new-password]"));
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("kurz", 1)]
    [InlineData("zwölf Zeichen", 2)]
    [InlineData("Lang.genug 2026!", 3)]
    [InlineData("ein langer satz aus wörtern", 3)]
    public void PasswordStrengthIsRated(string password, int expected)
    {
        Assert.Equal(expected, MyLife.Client.Components.NewPasswordFields.Rate(password));
    }

    private void UseServer(bool setupRequired, SessionDto? session)
    {
        var http = new HttpClient(new StubHandler(setupRequired, session)) { BaseAddress = new Uri("http://localhost/") };
        Services.AddSingleton(new SetupClient(http));
        Services.AddSingleton(new AuthClient(http));
    }

    private sealed class StubHandler(bool setupRequired, SessionDto? session) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(request.RequestUri!.AbsolutePath switch
            {
                "/api/setup/status" => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new SetupStatusDto(setupRequired)) },
                "/api/auth/session" when session is not null => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(session) },
                "/api/auth/session" => new HttpResponseMessage(HttpStatusCode.Unauthorized),
                "/api/account/passkeys" => new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Array.Empty<PasskeyDto>()) },
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            });
    }
}
