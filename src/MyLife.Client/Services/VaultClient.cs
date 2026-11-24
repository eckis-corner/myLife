using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MyLife.Shared.Vault;

namespace MyLife.Client.Services;

/// <summary>Typisierter Zugriff auf <c>/api/vault</c>. Fehler kommen als <see cref="VaultException"/> mit Code aus <see cref="VaultErrors"/>.</summary>
public sealed class VaultClient(HttpClient http)
{
    public async Task<IReadOnlyList<AreaDto>> GetAreasAsync(CancellationToken cancellationToken = default) =>
        await GetAsync<List<AreaDto>>("api/vault/areas", cancellationToken) ?? [];

    public async Task<AreaDto> CreateAreaKeyAsync(int area, byte[] wrappedKey, CancellationToken cancellationToken = default) =>
        (await SendAsync<AreaDto>(HttpMethod.Put, $"api/vault/areas/{area.ToString(CultureInfo.InvariantCulture)}/key", new AreaKeyRequest(wrappedKey), cancellationToken))!;

    public async Task<AreaDto> MarkAreaReviewedAsync(int area, CancellationToken cancellationToken = default) =>
        (await SendAsync<AreaDto>(HttpMethod.Post, $"api/vault/areas/{area.ToString(CultureInfo.InvariantCulture)}/reviewed", new { }, cancellationToken))!;

    public async Task<IReadOnlyList<VaultObjectDto>> GetObjectsAsync(CancellationToken cancellationToken = default) =>
        await GetAsync<List<VaultObjectDto>>("api/vault/objects", cancellationToken) ?? [];

    public async Task<VaultObjectDto> CreateObjectAsync(CreateObjectRequest request, CancellationToken cancellationToken = default) =>
        (await SendAsync<VaultObjectDto>(HttpMethod.Post, "api/vault/objects", request, cancellationToken))!;

    public async Task<VaultObjectDto> UpdateObjectAsync(Guid id, UpdateObjectRequest request, CancellationToken cancellationToken = default) =>
        (await SendAsync<VaultObjectDto>(HttpMethod.Put, $"api/vault/objects/{id}", request, cancellationToken))!;

    public async Task<VaultObjectDto> DeleteObjectAsync(Guid id, CancellationToken cancellationToken = default) =>
        (await SendAsync<VaultObjectDto>(HttpMethod.Delete, $"api/vault/objects/{id}", null, cancellationToken))!;

    public async Task<VaultObjectDto> RestoreObjectAsync(Guid id, CancellationToken cancellationToken = default) =>
        (await SendAsync<VaultObjectDto>(HttpMethod.Post, $"api/vault/objects/{id}/restore", new { }, cancellationToken))!;

    public Task PurgeObjectAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync<object>(HttpMethod.Delete, $"api/vault/objects/{id}/purge", null, cancellationToken);

    public async Task<IReadOnlyList<DocumentDto>> GetDocumentsAsync(CancellationToken cancellationToken = default) =>
        await GetAsync<List<DocumentDto>>("api/vault/documents", cancellationToken) ?? [];

    public async Task<DocumentDto> CreateDocumentAsync(CreateDocumentRequest request, CancellationToken cancellationToken = default) =>
        (await SendAsync<DocumentDto>(HttpMethod.Post, "api/vault/documents", request, cancellationToken))!;

    public async Task<DocumentDto> UploadContentAsync(Guid id, byte[] cipher, CancellationToken cancellationToken = default)
    {
        using var content = new ByteArrayContent(cipher);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var request = new HttpRequestMessage(HttpMethod.Put, $"api/vault/documents/{id}/content") { Content = content };
        using var response = await http.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<DocumentDto>(cancellationToken))!;
    }

    public async Task<byte[]> DownloadContentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync($"api/vault/documents/{id}/content", cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    public async Task<DocumentDto> DeleteDocumentAsync(Guid id, CancellationToken cancellationToken = default) =>
        (await SendAsync<DocumentDto>(HttpMethod.Delete, $"api/vault/documents/{id}", null, cancellationToken))!;

    public async Task<DocumentDto> RestoreDocumentAsync(Guid id, CancellationToken cancellationToken = default) =>
        (await SendAsync<DocumentDto>(HttpMethod.Post, $"api/vault/documents/{id}/restore", new { }, cancellationToken))!;

    public Task PurgeDocumentAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync<object>(HttpMethod.Delete, $"api/vault/documents/{id}/purge", null, cancellationToken);

    public async Task<AuditPageDto> GetAuditAsync(int page, int size, CancellationToken cancellationToken = default) =>
        await GetAsync<AuditPageDto>(
            string.Create(CultureInfo.InvariantCulture, $"api/vault/audit?page={page}&size={size}"), cancellationToken) ?? new AuditPageDto([], 0);

    public Task ReportAsync(string action, string? details = null, CancellationToken cancellationToken = default) =>
        SendAsync<object>(HttpMethod.Post, "api/vault/audit", new AuditEventRequest(action, details), cancellationToken);

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

        var code = response.StatusCode switch
        {
            HttpStatusCode.Conflict => VaultErrors.Conflict,
            HttpStatusCode.NotFound => VaultErrors.NotFound,
            HttpStatusCode.RequestEntityTooLarge => VaultErrors.TooLarge,
            _ => VaultErrors.Failed,
        };
        try
        {
            using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (problem.RootElement.TryGetProperty("title", out var title) && title.GetString() is { } value && value.StartsWith("vault-", StringComparison.Ordinal))
            {
                code = value;
            }
        }
        catch (JsonException)
        {
            // Kein Problem-JSON.
        }

        throw new VaultException(code, response.StatusCode);
    }
}

/// <summary>Fehler im Tresor. <see cref="Exception.Message"/> ist ein Code aus <see cref="VaultErrors"/> oder <see cref="ClientVaultErrors"/>.</summary>
public sealed class VaultException : Exception
{
    public VaultException(string code, HttpStatusCode? status = null)
        : base(code) => Status = status;

    public VaultException(string code, Exception innerException)
        : base(code, innerException)
    {
    }

    public VaultException()
    {
    }

    public VaultException(string message, Exception? innerException, HttpStatusCode? status)
        : base(message, innerException) => Status = status;

    public HttpStatusCode? Status { get; }
}

/// <summary>Fehlercodes, die nur im Browser entstehen.</summary>
public static class ClientVaultErrors
{
    public const string Locked = "vault-locked";
    public const string Decrypt = "vault-decrypt";
    public const string FileTooLarge = "vault-file-too-large";
    public const string ImportInvalid = "vault-import-invalid";
    public const string ImportWrongSecret = "vault-import-wrong-secret";
}
