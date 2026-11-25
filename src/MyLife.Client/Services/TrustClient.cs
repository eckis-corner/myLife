using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MyLife.Shared.Trust;

namespace MyLife.Client.Services;

/// <summary>Typisierter Zugriff auf <c>/api/trust</c> (Eigentümer) und <c>/api/shared</c> (Vertrauensperson).</summary>
public sealed class TrustClient(HttpClient http)
{
    public async Task<IReadOnlyList<TrusteeDto>> GetTrusteesAsync(CancellationToken cancellationToken = default) =>
        await GetAsync<List<TrusteeDto>>("api/trust/trustees", cancellationToken) ?? [];

    public async Task<TrusteeDto> SaveTrusteeAsync(Guid trusteeId, SaveTrusteeRequest request, CancellationToken cancellationToken = default) =>
        (await SendAsync<TrusteeDto>(HttpMethod.Put, $"api/trust/trustees/{trusteeId}", request, cancellationToken))!;

    public Task RemoveTrusteeAsync(Guid trusteeId, CancellationToken cancellationToken = default) =>
        SendAsync<object>(HttpMethod.Delete, $"api/trust/trustees/{trusteeId}", null, cancellationToken);

    public async Task<IReadOnlyList<AreaGrantDto>> GetAreaGrantsAsync(int area, CancellationToken cancellationToken = default) =>
        await GetAsync<List<AreaGrantDto>>(string.Create(CultureInfo.InvariantCulture, $"api/trust/areas/{area}/grants"), cancellationToken) ?? [];

    public async Task<IReadOnlyList<AreaGrantDto>> GetLetterGrantsAsync(Guid objectId, CancellationToken cancellationToken = default) =>
        await GetAsync<List<AreaGrantDto>>($"api/trust/letters/{objectId}/grants", cancellationToken) ?? [];

    public async Task<IReadOnlyList<SharedOwnerDto>> GetSharedAsync(CancellationToken cancellationToken = default) =>
        await GetAsync<List<SharedOwnerDto>>("api/shared", cancellationToken) ?? [];

    public async Task<SharedContentDto> GetSharedAreaAsync(Guid ownerId, int area, CancellationToken cancellationToken = default) =>
        (await GetAsync<SharedContentDto>(string.Create(CultureInfo.InvariantCulture, $"api/shared/{ownerId}/areas/{area}"), cancellationToken))!;

    public async Task<SharedContentDto> GetSharedLetterAsync(Guid ownerId, Guid objectId, CancellationToken cancellationToken = default) =>
        (await GetAsync<SharedContentDto>($"api/shared/{ownerId}/letters/{objectId}", cancellationToken))!;

    public async Task<byte[]> DownloadSharedDocumentAsync(Guid ownerId, Guid documentId, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync($"api/shared/{ownerId}/documents/{documentId}/content", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

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

        var code = response.StatusCode == HttpStatusCode.NotFound ? TrustErrors.NotFound : TrustErrors.Failed;
        try
        {
            using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (problem.RootElement.TryGetProperty("title", out var title) && title.GetString() is { } value && value.StartsWith("trust-", StringComparison.Ordinal))
            {
                code = value;
            }
        }
        catch (JsonException)
        {
            // Kein Problem-JSON, etwa vom Rate-Limiter.
        }

        throw new TrustException(code);
    }
}

/// <summary>Fehler bei Vertrauenspersonen und Freigaben. <see cref="Exception.Message"/> ist ein Code aus <see cref="TrustErrors"/>.</summary>
public sealed class TrustException : Exception
{
    public TrustException(string code)
        : base(code)
    {
    }

    public TrustException()
    {
    }

    public TrustException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
