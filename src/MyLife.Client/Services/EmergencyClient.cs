using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MyLife.Shared.Emergency;
using MyLife.Shared.Trust;

namespace MyLife.Client.Services;

/// <summary>Typisierter Zugriff auf <c>/api/emergency</c>: Notfallzugang, Lebenszeichen, Anträge und Links aus Mails.</summary>
public sealed class EmergencyClient(HttpClient http)
{
    public async Task<EmergencyStatusDto> GetStatusAsync(CancellationToken cancellationToken = default) =>
        (await GetAsync<EmergencyStatusDto>("api/emergency", cancellationToken))!;

    public Task LifesignAsync(CancellationToken cancellationToken = default) =>
        SendAsync<object>(HttpMethod.Post, "api/emergency/lifesign", new { }, cancellationToken);

    public Task SaveSettingsAsync(EmergencySettingsDto settings, CancellationToken cancellationToken = default) =>
        SendAsync<object>(HttpMethod.Put, "api/emergency/settings", settings, cancellationToken);

    public Task ObjectAsync(Guid requestId, CancellationToken cancellationToken = default) =>
        SendAsync<object>(HttpMethod.Post, $"api/emergency/requests/{requestId}/object", new { }, cancellationToken);

    public Task ContinueAsync(Guid requestId, CancellationToken cancellationToken = default) =>
        SendAsync<object>(HttpMethod.Post, $"api/emergency/requests/{requestId}/continue", new { }, cancellationToken);

    public async Task<IReadOnlyList<TrustedOwnerDto>> GetTrustedAsync(CancellationToken cancellationToken = default) =>
        await GetAsync<List<TrustedOwnerDto>>("api/emergency/trusted", cancellationToken) ?? [];

    public async Task<AccessRequestDto> RequestAsync(Guid ownerId, GrantMode mode, CancellationToken cancellationToken = default) =>
        (await SendAsync<AccessRequestDto>(HttpMethod.Post, $"api/emergency/trusted/{ownerId}/requests", new CreateAccessRequest(mode), cancellationToken))!;

    public Task WithdrawAsync(Guid requestId, CancellationToken cancellationToken = default) =>
        SendAsync<object>(HttpMethod.Delete, $"api/emergency/requests/{requestId}", null, cancellationToken);

    public async Task<EmergencyLinkResultDto> LifesignLinkAsync(string token, CancellationToken cancellationToken = default) =>
        (await SendAsync<EmergencyLinkResultDto>(HttpMethod.Post, "api/emergency/link/lifesign", new EmergencyTokenRequest(token), cancellationToken))!;

    public async Task<EmergencyLinkResultDto> ObjectionLinkAsync(string token, CancellationToken cancellationToken = default) =>
        (await SendAsync<EmergencyLinkResultDto>(HttpMethod.Post, "api/emergency/link/objection", new EmergencyTokenRequest(token), cancellationToken))!;

    private async Task<T?> GetAsync<T>(string uri, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(uri, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
    }

    private async Task<T?> SendAsync<T>(HttpMethod method, string uri, object? body, CancellationToken cancellationToken)
        where T : class
    {
        using var request = new HttpRequestMessage(method, uri);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        using var response = await http.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return response.StatusCode == HttpStatusCode.NoContent || typeof(T) == typeof(object)
            ? null
            : await response.Content.ReadFromJsonAsync<T>(cancellationToken);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var code = response.StatusCode == HttpStatusCode.NotFound ? EmergencyErrors.NotFound : EmergencyErrors.Failed;
        try
        {
            using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (problem.RootElement.TryGetProperty("title", out var title) && title.GetString() is { } value && value.StartsWith("emergency-", StringComparison.Ordinal))
            {
                code = value;
            }
        }
        catch (JsonException)
        {
            // Kein Problem-JSON, etwa vom Rate-Limiter.
        }

        throw new EmergencyException(code);
    }
}

/// <summary>Fehler beim Notfallzugang. <see cref="Exception.Message"/> ist ein Code aus <see cref="EmergencyErrors"/>.</summary>
public sealed class EmergencyException : Exception
{
    public EmergencyException(string code)
        : base(code)
    {
    }

    public EmergencyException()
    {
    }

    public EmergencyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
