using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using MyLife.Shared.Admin;

namespace MyLife.Server.Infrastructure.Admin;

/// <summary>
/// Push-Nachrichten über ntfy (Entscheidung 13). Veröffentlicht als JSON an die Wurzel des Servers, damit Titel
/// und Text Umlaute enthalten dürfen. Die Themen je Nutzer kommen mit M2.4; jetzt gibt es nur den Test.
/// </summary>
internal sealed partial class NtfyClient(IHttpClientFactory clients, SettingsStore settings)
{
    public const string HttpClientName = "ntfy";
    private static readonly string[] Tags = ["file_folder"];

    public static bool IsValidTopic(string? topic) => topic is not null && TopicPattern().IsMatch(topic);

    public async Task SendAsync(AdminSettingsDto stored, string topic, string title, string message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stored);
        if (stored.Ntfy.ServerUrl is not { } server)
        {
            throw new InvalidOperationException("ntfy ist nicht eingerichtet.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, server + "/")
        {
            Content = JsonContent.Create(new { topic, title, message, tags = Tags }),
        };
        if (settings.NtfyToken(stored) is { } token)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var client = clients.CreateClient(HttpClientName);
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"ntfy antwortet mit {(int)response.StatusCode} {response.ReasonPhrase}", null, response.StatusCode);
        }
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex TopicPattern();
}
