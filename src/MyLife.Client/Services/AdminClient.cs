using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MyLife.Shared.Admin;
using MyLife.Shared.Vault;

namespace MyLife.Client.Services;

/// <summary>Typisierter Zugriff auf <c>/api/admin</c>, den Datenschutzhinweis und die Kontolöschung.</summary>
public sealed class AdminClient(HttpClient http)
{
    public async Task<AdminSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default) =>
        (await GetAsync<AdminSettingsDto>("api/admin/settings", cancellationToken))!;

    public async Task<AdminSettingsDto> SaveSettingsAsync(AdminSettingsDto settings, CancellationToken cancellationToken = default) =>
        (await SendAsync<AdminSettingsDto>(HttpMethod.Put, "api/admin/settings", settings, cancellationToken))!;

    public async Task<SendTestResultDto> TestMailAsync(CancellationToken cancellationToken = default) =>
        (await SendAsync<SendTestResultDto>(HttpMethod.Post, "api/admin/settings/test-mail", new { }, cancellationToken))!;

    public async Task<SendTestResultDto> TestNtfyAsync(string topic, CancellationToken cancellationToken = default) =>
        (await SendAsync<SendTestResultDto>(HttpMethod.Post, "api/admin/settings/test-ntfy", new NtfyTestRequest(topic), cancellationToken))!;

    public async Task<MailQueueDto> GetMailQueueAsync(CancellationToken cancellationToken = default) =>
        (await GetAsync<MailQueueDto>("api/admin/mail", cancellationToken))!;

    public async Task<BackupStatusDto> GetBackupsAsync(CancellationToken cancellationToken = default) =>
        (await GetAsync<BackupStatusDto>("api/admin/backups", cancellationToken))!;

    public async Task<BackupItemDto> BackupNowAsync(CancellationToken cancellationToken = default) =>
        (await SendAsync<BackupItemDto>(HttpMethod.Post, "api/admin/backups", new { }, cancellationToken))!;

    public Task RetryMailAsync(CancellationToken cancellationToken = default) =>
        SendAsync<object>(HttpMethod.Post, "api/admin/mail/retry", new { }, cancellationToken);

    public async Task<IReadOnlyList<AdminUserDto>> GetUsersAsync(CancellationToken cancellationToken = default) =>
        await GetAsync<List<AdminUserDto>>("api/admin/users", cancellationToken) ?? [];

    public async Task<AdminUserDto> LockAsync(Guid id, CancellationToken cancellationToken = default) =>
        (await SendAsync<AdminUserDto>(HttpMethod.Post, $"api/admin/users/{id}/lock", new { }, cancellationToken))!;

    public async Task<AdminUserDto> UnlockAsync(Guid id, CancellationToken cancellationToken = default) =>
        (await SendAsync<AdminUserDto>(HttpMethod.Post, $"api/admin/users/{id}/unlock", new { }, cancellationToken))!;

    public async Task<AdminUserDto> ResetTwoFactorAsync(Guid id, CancellationToken cancellationToken = default) =>
        (await SendAsync<AdminUserDto>(HttpMethod.Post, $"api/admin/users/{id}/reset-2fa", new { }, cancellationToken))!;

    public async Task<AdminUserDto> SetAdminAsync(Guid id, bool isAdmin, CancellationToken cancellationToken = default) =>
        (await SendAsync<AdminUserDto>(HttpMethod.Put, $"api/admin/users/{id}/admin", new SetAdminRequest(isAdmin), cancellationToken))!;

    public async Task<AdminUserDto> RequestDeletionAsync(Guid id, CancellationToken cancellationToken = default) =>
        (await SendAsync<AdminUserDto>(HttpMethod.Post, $"api/admin/users/{id}/deletion", new { }, cancellationToken))!;

    public async Task<AdminUserDto> CancelDeletionAsync(Guid id, CancellationToken cancellationToken = default) =>
        (await SendAsync<AdminUserDto>(HttpMethod.Delete, $"api/admin/users/{id}/deletion", null, cancellationToken))!;

    public async Task<AuditPageDto> GetAuditAsync(int page, int size, CancellationToken cancellationToken = default) =>
        await GetAsync<AuditPageDto>(string.Create(CultureInfo.InvariantCulture, $"api/admin/audit?page={page}&size={size}"), cancellationToken)
        ?? new AuditPageDto([], 0);

    public async Task<PrivacyNoticeDto> GetPrivacyAsync(CancellationToken cancellationToken = default) =>
        (await GetAsync<PrivacyNoticeDto>("api/privacy", cancellationToken))!;

    public async Task<AccountDeletionDto> GetDeletionAsync(string token, CancellationToken cancellationToken = default) =>
        (await SendAsync<AccountDeletionDto>(HttpMethod.Post, "api/account-deletion/info", new AccountDeletionRequest(token), cancellationToken))!;

    public Task ConfirmDeletionAsync(string token, CancellationToken cancellationToken = default) =>
        SendAsync<object>(HttpMethod.Post, "api/account-deletion/confirm", new AccountDeletionRequest(token), cancellationToken);

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

        var code = response.StatusCode == HttpStatusCode.NotFound ? AdminErrors.NotFound : AdminErrors.Failed;
        Dictionary<string, string>? fields = null;
        try
        {
            using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (problem.RootElement.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                code = AdminErrors.Invalid;
                fields = errors.EnumerateObject().ToDictionary(e => e.Name, e => e.Value.EnumerateArray().FirstOrDefault().GetString() ?? SettingsErrors.Range, StringComparer.Ordinal);
            }
            else if (problem.RootElement.TryGetProperty("title", out var title) && title.GetString() is { } value && value.StartsWith("admin-", StringComparison.Ordinal))
            {
                code = value;
            }
        }
        catch (JsonException)
        {
            // Kein Problem-JSON, etwa vom Rate-Limiter.
        }

        throw new AdminException(code, fields);
    }
}

/// <summary>Fehler der Verwaltung. <see cref="Exception.Message"/> ist ein Code aus <see cref="AdminErrors"/>.</summary>
public sealed class AdminException : Exception
{
    public AdminException(string code, IReadOnlyDictionary<string, string>? fields)
        : base(code)
    {
        Fields = fields ?? new Dictionary<string, string>();
    }

    public AdminException()
    {
        Fields = new Dictionary<string, string>();
    }

    public AdminException(string message, Exception innerException)
        : base(message, innerException)
    {
        Fields = new Dictionary<string, string>();
    }

    /// <summary>Feld → Fehlercode bei ungültigen Einstellungen.</summary>
    public IReadOnlyDictionary<string, string> Fields { get; }
}
