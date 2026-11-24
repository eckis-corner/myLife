using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyLife.Client.Services;
using MyLife.Crypto;
using MyLife.Server.Data;
using MyLife.Server.Infrastructure;
using MyLife.Shared.Auth;
using static MyLife.Tests.Server.TestToken;

namespace MyLife.Tests.Server;

internal static class TestToken
{
    public static CancellationToken Ct => TestContext.Current.CancellationToken;
}

/// <summary>Ein eingerichtetes Konto und Helfer, die den Browser mit der .NET-Referenz nachbilden.</summary>
internal sealed class AuthTestSupport : IAsyncDisposable
{
    
    public const string Email = "daniel@example.org";
    private readonly ServerFixture _server;
    private readonly List<IAsyncDisposable> _disposables = [];

    private AuthTestSupport(ServerFixture server, byte[] recoveryKey, string totpSecret)
    {
        _server = server;
        RecoveryKey = recoveryKey;
        TotpSecret = totpSecret;
    }

    public byte[] RecoveryKey { get; }

    public string TotpSecret { get; }

    public SoftwareAuthenticator Authenticator { get; } = new("https://localhost");

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ServerFixture, Task<AuthTestSupport>> Accounts = [];

    /// <summary>Richtet je Server (also je Testklasse) einmal ein; alle Tests der Klasse teilen sich das Konto.</summary>
    public static Task<AuthTestSupport> ForAsync(ServerFixture server) =>
        Accounts.GetValue(server, s => SetUpAsync(s, CancellationToken.None));

    private static async Task<AuthTestSupport> SetUpAsync(ServerFixture server, CancellationToken cancellationToken)
    {
        using var client = server.CreateApiClient();
        await using var crypto = new ReferenceMyLifeCrypto();
        var (request, recoveryKey, totpSecret) = await SetupTestSupport.PrepareAsync(client, SetupTestSupport.Token(server), crypto, cancellationToken);
        await new SetupClient(client).CompleteAsync(request, cancellationToken);
        return new AuthTestSupport(server, recoveryKey, totpSecret);
    }

    /// <summary>Ein „Browser“: eigener HttpClient mit Cookies, eigenes Krypto-Register, eigene Sitzung.</summary>
    public (SessionService Session, AuthClient Api, HttpClient Http) NewBrowser()
    {
        var http = _server.CreateApiClient();
        var crypto = new ReferenceMyLifeCrypto();
        _disposables.Add(crypto);
        var api = new AuthClient(http);
        var webAuthn = new WebAuthnService(Authenticator.AsJsRuntime());
        return (new SessionService(api, crypto, webAuthn), api, http);
    }

    /// <summary>Stellt die Serveruhr auf den nächsten TOTP-Schritt und liefert dessen Code.</summary>
    public string NextCode()
    {
        _server.Clock.Advance(TimeSpan.FromSeconds(Totp.PeriodSeconds));
        return Totp.Compute(Base32.Decode(TotpSecret), Totp.StepAt(_server.Clock.GetUtcNow()));
    }

    public async Task<(SessionService Session, AuthClient Api, HttpClient Http)> LoginAsync(string password = SetupTestSupport.Password)
    {
        var browser = NewBrowser();
        var challenge = await browser.Session.BeginLoginAsync(Email, password);
        await browser.Session.CompleteLoginWithTotpAsync(challenge.Pending, NextCode());
        return browser;
    }

    public async ValueTask DisposeAsync()
    {
        Authenticator.Dispose();
        foreach (var disposable in _disposables)
        {
            await disposable.DisposeAsync();
        }
    }
}

public sealed class LoginTests(ServerFixture server) : IClassFixture<ServerFixture>, IAsyncLifetime
{
    private AuthTestSupport _account = null!;

    public async ValueTask InitializeAsync() => _account = await AuthTestSupport.ForAsync(server);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task PasswordAndTotpOpenTheVault()
    {
        var (session, api, _) = _account.NewBrowser();
        var challenge = await session.BeginLoginAsync(" Daniel@Example.org ", SetupTestSupport.Password);

        Assert.True(challenge.Totp);
        Assert.False(challenge.Passkey);
        Assert.Equal(SessionState.Unknown, session.State);

        var code = _account.NextCode();
        await session.CompleteLoginWithTotpAsync(challenge.Pending, code);

        Assert.Equal(SessionState.Unlocked, session.State);
        Assert.NotNull(session.VaultKey);
        Assert.NotNull(session.SigningKey);
        Assert.True(session.ToPrincipal().Identity!.IsAuthenticated);
        Assert.Equal(AuthTestSupport.Email, (await api.GetSessionAsync(Ct))!.Email);

        // Derselbe Code gilt nicht ein zweites Mal.
        var (other, _, _) = _account.NewBrowser();
        var second = await other.BeginLoginAsync(AuthTestSupport.Email, SetupTestSupport.Password);
        var replay = await Assert.ThrowsAsync<AuthException>(() => other.CompleteLoginWithTotpAsync(second.Pending, code));
        Assert.Equal(AuthErrors.InvalidCode, replay.Message);

        await session.LogoutAsync();
        Assert.Equal(SessionState.Anonymous, session.State);
        Assert.Null(await api.GetSessionAsync(Ct));
    }

    [Fact]
    public async Task WrongPasswordAndUnknownAddressLookTheSame()
    {
        var (session, api, _) = _account.NewBrowser();

        var wrong = await Assert.ThrowsAsync<AuthException>(() => session.BeginLoginAsync(AuthTestSupport.Email, "falsches Passwort!"));
        var unknown = await Assert.ThrowsAsync<AuthException>(() => session.BeginLoginAsync("niemand@example.org", SetupTestSupport.Password));

        Assert.Equal(AuthErrors.InvalidCredentials, wrong.Message);
        Assert.Equal(AuthErrors.InvalidCredentials, unknown.Message);

        var decoy = await api.PreloginAsync("niemand@example.org", Ct);
        Assert.Equal(16, decoy.KdfSalt.Length);
        Assert.Equal(decoy.KdfSalt, (await api.PreloginAsync("NIEMAND@example.org ", Ct)).KdfSalt);
        Assert.NotEqual(decoy.KdfSalt, (await api.PreloginAsync("jemand@example.org", Ct)).KdfSalt);
        Assert.Equal(Argon2Parameters.Default.MemoryKiB, decoy.KdfMemoryKiB);
    }

    [Fact]
    public async Task ReloadKeepsTheSessionButLocksTheVault()
    {
        var (_, _, http) = await _account.LoginAsync();

        // Neues Krypto-Register mit demselben Cookie: wie ein Neuladen der Seite.
        await using var crypto = new ReferenceMyLifeCrypto();
        var reloaded = new SessionService(new AuthClient(http), crypto, new WebAuthnService(_account.Authenticator.AsJsRuntime()));
        await reloaded.InitializeAsync();
        Assert.Equal(SessionState.Locked, reloaded.State);
        Assert.False(reloaded.ToPrincipal().Identity!.IsAuthenticated);

        var wrong = await Assert.ThrowsAsync<AuthException>(() => reloaded.UnlockAsync("falsches Passwort!"));
        Assert.Equal(ClientAuthErrors.WrongPassword, wrong.Message);
        Assert.Equal(SessionState.Locked, reloaded.State);

        await reloaded.UnlockAsync(SetupTestSupport.Password);
        Assert.Equal(SessionState.Unlocked, reloaded.State);
    }

    [Fact]
    public async Task ExpiredPendingTokenIsRejected()
    {
        var (session, _, _) = _account.NewBrowser();
        var challenge = await session.BeginLoginAsync(AuthTestSupport.Email, SetupTestSupport.Password);
        server.Clock.Advance(TimeSpan.FromMinutes(6));

        var error = await Assert.ThrowsAsync<AuthException>(() => session.CompleteLoginWithTotpAsync(challenge.Pending, _account.NextCode()));
        Assert.Equal(AuthErrors.Expired, error.Message);
    }

    [Fact]
    public async Task ProtectedEndpointsNeedASessionAndWritesNeedTheCsrfHeader()
    {
        using var anonymous = server.CreateApiClient();
        var cancellationToken = TestContext.Current.CancellationToken;

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(new Uri("/api/auth/session", UriKind.Relative), cancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(new Uri("/api/account/passkeys", UriKind.Relative), cancellationToken)).StatusCode);

        var (_, _, http) = await _account.LoginAsync();
        using var withoutHeader = server.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        var response = await withoutHeader.PostAsJsonAsync("/api/auth/prelogin", new PreloginRequest(AuthTestSupport.Email), cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync(new Uri("/api/auth/session", UriKind.Relative), cancellationToken)).StatusCode);
    }
}

public sealed class LockoutTests(ServerFixture server) : IClassFixture<ServerFixture>, IAsyncLifetime
{
    private AuthTestSupport _account = null!;

    public async ValueTask InitializeAsync() => _account = await AuthTestSupport.ForAsync(server);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task FiveFailuresLockTheAccountForFifteenMinutes()
    {
        var (session, _, _) = _account.NewBrowser();
        for (var i = 0; i < 5; i++)
        {
            await Assert.ThrowsAsync<AuthException>(() => session.BeginLoginAsync(AuthTestSupport.Email, $"falsch {i} Passwort"));
        }

        var locked = await Assert.ThrowsAsync<AuthException>(() => session.BeginLoginAsync(AuthTestSupport.Email, SetupTestSupport.Password));
        Assert.Equal(AuthErrors.Locked, locked.Message);

        server.Clock.Advance(TimeSpan.FromMinutes(16));
        var (unlocked, _, _) = await _account.LoginAsync();
        Assert.Equal(SessionState.Unlocked, unlocked.State);
    }
}

public sealed class PasswordChangeTests(ServerFixture server) : IClassFixture<ServerFixture>, IAsyncLifetime
{
    private const string NewPassword = "Ein ganz neues Master-Passwort 2026";
    private AuthTestSupport _account = null!;

    public async ValueTask InitializeAsync() => _account = await AuthTestSupport.ForAsync(server);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task NewPasswordWorksAndOtherSessionsEnd()
    {
        var (session, api, _) = await _account.LoginAsync();
        var (_, otherApi, _) = await _account.LoginAsync();

        var wrong = await Assert.ThrowsAsync<AuthException>(() => session.ChangePasswordAsync("nicht das Passwort", NewPassword));
        Assert.Equal(ClientAuthErrors.WrongPassword, wrong.Message);

        await session.ChangePasswordAsync(SetupTestSupport.Password, NewPassword);

        Assert.NotNull(await api.GetSessionAsync(Ct));
        Assert.Null(await otherApi.GetSessionAsync(Ct));
        var old = await Assert.ThrowsAsync<AuthException>(() => _account.LoginAsync());
        Assert.Equal(AuthErrors.InvalidCredentials, old.Message);
        var (fresh, _, _) = await _account.LoginAsync(NewPassword);
        Assert.Equal(SessionState.Unlocked, fresh.State);
    }
}

public sealed class RecoveryTests(ServerFixture server) : IClassFixture<ServerFixture>, IAsyncLifetime
{
    private const string NewPassword = "Nach der Wiederherstellung gesetzt!";
    private AuthTestSupport _account = null!;

    public async ValueTask InitializeAsync() => _account = await AuthTestSupport.ForAsync(server);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task RecoveryKeySetsANewPasswordAndKeepsTheVault()
    {
        using var http = server.CreateApiClient();
        await using var crypto = new ReferenceMyLifeCrypto();
        var recovery = new RecoveryService(new AuthClient(http), crypto);
        var words = string.Join(' ', Bip39.Encode(_account.RecoveryKey));

        var typo = await Assert.ThrowsAsync<AuthException>(() => recovery.RecoverAsync(AuthTestSupport.Email, words.Replace(words.Split(' ')[0], "zoo", StringComparison.Ordinal), NewPassword));
        Assert.Equal(ClientAuthErrors.InvalidRecoveryWords, typo.Message);
        var otherKey = string.Join(' ', Bip39.Encode(new byte[32]));
        var wrong = await Assert.ThrowsAsync<AuthException>(() => recovery.RecoverAsync(AuthTestSupport.Email, otherKey, NewPassword));
        Assert.Equal(AuthErrors.InvalidCredentials, wrong.Message);

        await recovery.RecoverAsync(AuthTestSupport.Email, words, NewPassword);

        await Assert.ThrowsAsync<AuthException>(() => _account.LoginAsync());
        var (session, _, _) = await _account.LoginAsync(NewPassword);
        Assert.Equal(SessionState.Unlocked, session.State);

        // Der Recovery-Key bleibt gültig: Er öffnet weiterhin denselben Tresorschlüssel.
        await using var scope = server.Services.CreateAsyncScope();
        var user = await scope.ServiceProvider.GetRequiredService<MyLifeDbContext>().Users.SingleAsync(TestContext.Current.CancellationToken);
        var recoveryWrap = await crypto.ImportRecoveryKeyAsync(_account.RecoveryKey);
        await crypto.UnwrapKeyAsync(recoveryWrap, user.WrappedVmkByRecovery, KeyContexts.Vmk);
    }
}

public sealed class PasskeyTests(ServerFixture server) : IClassFixture<ServerFixture>, IAsyncLifetime
{
    private AuthTestSupport _account = null!;

    public async ValueTask InitializeAsync() => _account = await AuthTestSupport.ForAsync(server);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task RegisteredPasskeyReplacesTheTotpCode()
    {
        var (session, api, _) = await _account.LoginAsync();
        var webAuthn = new WebAuthnService(_account.Authenticator.AsJsRuntime());
        var options = await api.PasskeyRegistrationOptionsAsync(Ct);
        var created = await api.RegisterPasskeyAsync("Laptop", options.OptionsToken, await webAuthn.CreateAsync(options.OptionsJson), Ct);
        Assert.Equal("Laptop", Assert.Single(await api.GetPasskeysAsync(Ct)).Name);

        // Eine Antwort, die nicht vom Authenticator stammt, wird abgelehnt.
        var invalid = await Assert.ThrowsAsync<AuthException>(() => api.RegisterPasskeyAsync("Kopie", options.OptionsToken, "{}", Ct));
        Assert.Equal(AuthErrors.PasskeyFailed, invalid.Message);
        await session.LogoutAsync();

        var (browser, browserApi, _) = _account.NewBrowser();
        var challenge = await browser.BeginLoginAsync(AuthTestSupport.Email, SetupTestSupport.Password);
        Assert.True(challenge.Passkey);
        await browser.CompleteLoginWithPasskeyAsync(challenge.Pending);
        Assert.Equal(SessionState.Unlocked, browser.State);
        Assert.NotNull(Assert.Single(await browserApi.GetPasskeysAsync(Ct)).LastUsedAt);

        await browserApi.DeletePasskeyAsync(created.Id, Ct);
        Assert.Empty(await browserApi.GetPasskeysAsync(Ct));
    }

    [Fact]
    public async Task ForgedPasskeySignatureIsRejected()
    {
        var (_, api, _) = await _account.LoginAsync();
        var webAuthn = new WebAuthnService(_account.Authenticator.AsJsRuntime());
        var options = await api.PasskeyRegistrationOptionsAsync(Ct);
        await api.RegisterPasskeyAsync("Schlüssel", options.OptionsToken, await webAuthn.CreateAsync(options.OptionsJson), Ct);

        var (browser, _, _) = _account.NewBrowser();
        var challenge = await browser.BeginLoginAsync(AuthTestSupport.Email, SetupTestSupport.Password);
        _account.Authenticator.ForgeNextSignature = true;

        var error = await Assert.ThrowsAsync<AuthException>(() => browser.CompleteLoginWithPasskeyAsync(challenge.Pending));
        Assert.Equal(AuthErrors.PasskeyFailed, error.Message);
        Assert.NotEqual(SessionState.Unlocked, browser.State);
    }
}
