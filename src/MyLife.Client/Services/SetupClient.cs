using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MyLife.Shared.Setup;

namespace MyLife.Client.Services;

/// <summary>Typisierter Zugriff auf <c>/api/setup</c>. Fehler kommen als <see cref="SetupException"/> mit einem festen Code.</summary>
public sealed class SetupClient(HttpClient http)
{
    public async Task<bool> IsRequiredAsync(CancellationToken cancellationToken = default)
    {
        var status = await http.GetFromJsonAsync<SetupStatusDto>("api/setup/status", cancellationToken);
        return status?.Required ?? false;
    }

    public Task VerifyTokenAsync(string token, CancellationToken cancellationToken = default) =>
        PostAsync<object>("api/setup/token", new SetupTokenRequest(token), cancellationToken);

    public async Task<TotpEnrollmentDto> CreateTotpAsync(string token, CancellationToken cancellationToken = default) =>
        (await PostAsync<TotpEnrollmentDto>("api/setup/totp", new SetupTokenRequest(token), cancellationToken))!;

    public async Task<string> VerifyTotpAsync(string token, string enrollment, string code, CancellationToken cancellationToken = default) =>
        (await PostAsync<TotpConfirmationDto>("api/setup/totp/verify", new TotpVerifyRequest(token, enrollment, code), cancellationToken))!.Confirmation;

    public Task CompleteAsync(SetupCompleteRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<object>("api/setup/complete", request, cancellationToken);

    private async Task<T?> PostAsync<T>(string uri, object body, CancellationToken cancellationToken)
        where T : class
    {
        using var response = await http.PostAsJsonAsync(uri, body, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.Created || typeof(T) == typeof(object)
                ? null
                : await response.Content.ReadFromJsonAsync<T>(cancellationToken);
        }

        throw new SetupException(await ReadCodeAsync(response, cancellationToken));
    }

    private static async Task<string> ReadCodeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return "setup-rate-limited";
        }

        try
        {
            using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (problem.RootElement.TryGetProperty("errors", out _))
            {
                return "setup-invalid-input";
            }

            return problem.RootElement.TryGetProperty("title", out var title) && title.GetString() is { } code && code.StartsWith("setup-", StringComparison.Ordinal)
                ? code
                : "setup-failed";
        }
        catch (JsonException)
        {
            return "setup-failed";
        }
    }
}

/// <summary>Fehler im Einrichtungsassistenten. <see cref="Exception.Message"/> ist ein Ressourcenschlüssel-Suffix wie <c>setup-invalid-token</c>.</summary>
public sealed class SetupException : Exception
{
    public SetupException(string code)
        : base(code)
    {
    }

    public SetupException()
    {
    }

    public SetupException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
