using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MyLife.Shared.Auth;

namespace MyLife.Client.Services;

/// <summary>Typisierter Zugriff auf <c>/api/auth</c> und <c>/api/account</c>. Fehler kommen als <see cref="AuthException"/>.</summary>
public sealed class AuthClient(HttpClient http)
{
    public async Task<PreloginDto> PreloginAsync(string email, CancellationToken cancellationToken = default) =>
        (await PostAsync<PreloginDto>("api/auth/prelogin", new PreloginRequest(email), cancellationToken))!;

    public async Task<LoginChallengeDto> LoginAsync(string email, byte[] authHash, CancellationToken cancellationToken = default) =>
        (await PostAsync<LoginChallengeDto>("api/auth/login", new LoginRequest(email, authHash), cancellationToken))!;

    public async Task<SessionDto> LoginTotpAsync(string pending, string code, CancellationToken cancellationToken = default) =>
        (await PostAsync<SessionDto>("api/auth/login/totp", new TotpLoginRequest(pending, code), cancellationToken))!;

    public async Task<PasskeyOptionsDto> PasskeyLoginOptionsAsync(string pending, CancellationToken cancellationToken = default) =>
        (await PostAsync<PasskeyOptionsDto>("api/auth/login/passkey/options", new PasskeyLoginOptionsRequest(pending), cancellationToken))!;

    public async Task<SessionDto> LoginPasskeyAsync(string pending, string optionsToken, string assertionJson, CancellationToken cancellationToken = default) =>
        (await PostAsync<SessionDto>("api/auth/login/passkey", new PasskeyLoginRequest(pending, optionsToken, assertionJson), cancellationToken))!;

    /// <summary>Aktuelle Sitzung oder null, wenn niemand angemeldet ist.</summary>
    public async Task<SessionDto?> GetSessionAsync(CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync("api/auth/session", cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SessionDto>(cancellationToken);
    }

    public Task LogoutAsync(CancellationToken cancellationToken = default) =>
        PostAsync<object>("api/auth/logout", new { }, cancellationToken);

    public async Task<RecoveryChallengeDto> StartRecoveryAsync(string email, byte[] recoveryAuthHash, CancellationToken cancellationToken = default) =>
        (await PostAsync<RecoveryChallengeDto>("api/auth/recovery", new RecoveryRequest(email, recoveryAuthHash), cancellationToken))!;

    public Task CompleteRecoveryAsync(string pending, PasswordMaterial material, CancellationToken cancellationToken = default) =>
        PostAsync<object>("api/auth/recovery/complete", new RecoveryCompleteRequest(pending, material), cancellationToken);

    public Task ChangePasswordAsync(byte[] currentAuthHash, PasswordMaterial material, CancellationToken cancellationToken = default) =>
        PostAsync<object>("api/account/password", new ChangePasswordRequest(currentAuthHash, material), cancellationToken);

    public async Task<IReadOnlyList<PasskeyDto>> GetPasskeysAsync(CancellationToken cancellationToken = default) =>
        await http.GetFromJsonAsync<List<PasskeyDto>>("api/account/passkeys", cancellationToken) ?? [];

    public async Task<PasskeyOptionsDto> PasskeyRegistrationOptionsAsync(CancellationToken cancellationToken = default) =>
        (await PostAsync<PasskeyOptionsDto>("api/account/passkeys/options", new { }, cancellationToken))!;

    public async Task<PasskeyDto> RegisterPasskeyAsync(string name, string optionsToken, string attestationJson, CancellationToken cancellationToken = default) =>
        (await PostAsync<PasskeyDto>("api/account/passkeys", new PasskeyRegisterRequest(name, optionsToken, attestationJson), cancellationToken))!;

    public async Task DeletePasskeyAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await http.DeleteAsync($"api/account/passkeys/{id}", cancellationToken);
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
        {
            throw new AuthException(await ReadCodeAsync(response, cancellationToken));
        }
    }

    private async Task<T?> PostAsync<T>(string uri, object body, CancellationToken cancellationToken)
        where T : class
    {
        using var response = await http.PostAsJsonAsync(uri, body, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return response.StatusCode is HttpStatusCode.NoContent || typeof(T) == typeof(object)
                ? null
                : await response.Content.ReadFromJsonAsync<T>(cancellationToken);
        }

        throw new AuthException(await ReadCodeAsync(response, cancellationToken));
    }

    private static async Task<string> ReadCodeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (problem.RootElement.TryGetProperty("errors", out _))
            {
                return AuthErrors.InvalidInput;
            }

            if (problem.RootElement.TryGetProperty("title", out var title) && title.GetString() is { } code && code.StartsWith("auth-", StringComparison.Ordinal))
            {
                return code;
            }
        }
        catch (JsonException)
        {
            // Kein Problem-JSON, etwa vom Rate-Limiter.
        }

        return response.StatusCode == HttpStatusCode.TooManyRequests ? AuthErrors.RateLimited : AuthErrors.Failed;
    }
}

/// <summary>Fehler bei Anmeldung oder Kontoverwaltung. <see cref="Exception.Message"/> ist ein Code aus <see cref="AuthErrors"/> oder <see cref="ClientAuthErrors"/>.</summary>
public sealed class AuthException : Exception
{
    public AuthException(string code)
        : base(code)
    {
    }

    public AuthException()
    {
    }

    public AuthException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Fehlercodes, die nur im Browser entstehen.</summary>
public static class ClientAuthErrors
{
    public const string WrongPassword = "auth-wrong-password";
    public const string InvalidRecoveryWords = "auth-invalid-recovery-words";
    public const string PasskeyCancelled = "auth-passkey-cancelled";
    public const string PasskeyUnsupported = "auth-passkey-unsupported";
    public const string CryptoFailed = "auth-crypto-failed";
}
