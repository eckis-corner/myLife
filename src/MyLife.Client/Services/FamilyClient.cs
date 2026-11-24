using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MyLife.Shared.Family;
using MyLife.Shared.Setup;

namespace MyLife.Client.Services;

/// <summary>Typisierter Zugriff auf <c>/api/family</c> (angemeldet) und <c>/api/invite</c> (Annahme ohne Anmeldung).</summary>
public sealed class FamilyClient(HttpClient http)
{
    public async Task<IReadOnlyList<InvitationDto>> GetInvitationsAsync(CancellationToken cancellationToken = default) =>
        await GetAsync<List<InvitationDto>>("api/family/invitations", cancellationToken) ?? [];

    public async Task<InvitationCreatedDto> CreateInvitationAsync(CreateInvitationRequest request, CancellationToken cancellationToken = default) =>
        (await SendAsync<InvitationCreatedDto>(HttpMethod.Post, "api/family/invitations", request, cancellationToken))!;

    public async Task<InvitationCreatedDto> RenewInvitationAsync(Guid id, string? mailFragment, CancellationToken cancellationToken = default) =>
        (await SendAsync<InvitationCreatedDto>(HttpMethod.Post, $"api/family/invitations/{id}/renew", new RenewInvitationRequest(mailFragment), cancellationToken))!;

    public Task RevokeInvitationAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync<object>(HttpMethod.Delete, $"api/family/invitations/{id}", null, cancellationToken);

    public async Task<IReadOnlyList<PendingVerificationDto>> GetVerificationsAsync(CancellationToken cancellationToken = default) =>
        await GetAsync<List<PendingVerificationDto>>("api/family/verifications", cancellationToken) ?? [];

    public Task VerifyAsync(Guid invitationId, VerifyInvitationRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<object>(HttpMethod.Post, $"api/family/invitations/{invitationId}/verify", request, cancellationToken);

    public async Task<IReadOnlyList<PersonDto>> GetPeopleAsync(CancellationToken cancellationToken = default) =>
        await GetAsync<List<PersonDto>>("api/family/people", cancellationToken) ?? [];

    public async Task<IReadOnlyList<PinDto>> GetPinsAsync(CancellationToken cancellationToken = default) =>
        await GetAsync<List<PinDto>>("api/family/pins", cancellationToken) ?? [];

    public Task PinAsync(Guid personId, byte[] encryptedPin, CancellationToken cancellationToken = default) =>
        SendAsync<object>(HttpMethod.Put, $"api/family/people/{personId}/pin", new PinRequest(encryptedPin), cancellationToken);

    public Task EndorseAsync(Guid personId, byte[] signature, CancellationToken cancellationToken = default) =>
        SendAsync<object>(HttpMethod.Put, $"api/family/people/{personId}/endorsement", new EndorseRequest(signature), cancellationToken);

    public async Task<IReadOnlyList<InvitationDto>> GetOpenInvitationsAsAdminAsync(CancellationToken cancellationToken = default) =>
        await GetAsync<List<InvitationDto>>("api/admin/invitations", cancellationToken) ?? [];

    public Task RevokeInvitationAsAdminAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync<object>(HttpMethod.Delete, $"api/admin/invitations/{id}", null, cancellationToken);

    public async Task<InvitationInfoDto> GetInviteInfoAsync(string token, CancellationToken cancellationToken = default) =>
        (await SendAsync<InvitationInfoDto>(HttpMethod.Post, "api/invite/info", new InvitationTokenRequest(token), cancellationToken))!;

    public async Task<TotpEnrollmentDto> CreateInviteTotpAsync(string token, CancellationToken cancellationToken = default) =>
        (await SendAsync<TotpEnrollmentDto>(HttpMethod.Post, "api/invite/totp", new InvitationTokenRequest(token), cancellationToken))!;

    public async Task<string> VerifyInviteTotpAsync(string token, string enrollment, string code, CancellationToken cancellationToken = default) =>
        (await SendAsync<TotpConfirmationDto>(HttpMethod.Post, "api/invite/totp/verify", new InviteTotpVerifyRequest(token, enrollment, code), cancellationToken))!.Confirmation;

    public Task AcceptAsync(AcceptInvitationRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<object>(HttpMethod.Post, "api/invite/accept", request, cancellationToken);

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

        var code = response.StatusCode == HttpStatusCode.NotFound ? FamilyErrors.NotFound : FamilyErrors.Failed;
        try
        {
            using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (problem.RootElement.TryGetProperty("errors", out _))
            {
                code = FamilyErrors.Invalid;
            }
            else if (problem.RootElement.TryGetProperty("title", out var title) && title.GetString() is { } value
                     && (value.StartsWith("family-", StringComparison.Ordinal) || value.StartsWith("admin-", StringComparison.Ordinal)))
            {
                code = value;
            }
        }
        catch (JsonException)
        {
            // Kein Problem-JSON, etwa vom Rate-Limiter.
        }

        throw new FamilyException(code);
    }
}

/// <summary>Fehler bei Einladungen und Schlüsselprüfung. <see cref="Exception.Message"/> ist ein Code aus <see cref="FamilyErrors"/>.</summary>
public sealed class FamilyException : Exception
{
    public FamilyException(string code)
        : base(code)
    {
    }

    public FamilyException()
    {
    }

    public FamilyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
