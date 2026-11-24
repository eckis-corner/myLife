using System.Net;
using System.Net.Http.Json;
using MyLife.Shared;

namespace MyLife.Tests;

public sealed class ServerTests(ServerFixture server) : IClassFixture<ServerFixture>
{
    [Fact]
    public async Task ReadinessReportsHealthyDatabase()
    {
        using var client = server.CreateClient();

        var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task InfoReturnsVersionAndCryptoFormat()
    {
        using var client = server.CreateClient();

        var info = await client.GetFromJsonAsync<AppInfoDto>("/api/info", TestContext.Current.CancellationToken);

        Assert.NotNull(info);
        Assert.False(string.IsNullOrWhiteSpace(info.Version));
        Assert.Equal(1, info.CryptoFormatVersion);
    }

    [Fact]
    public async Task PagesCarryStrictSecurityHeaders()
    {
        using var client = server.CreateClient();

        var response = await client.GetAsync(new Uri("/", UriKind.Relative), TestContext.Current.CancellationToken);

        var csp = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("script-src 'self' 'wasm-unsafe-eval'", csp, StringComparison.Ordinal);
        Assert.DoesNotContain("'unsafe-inline'; img", csp.Replace("style-src 'self' 'unsafe-inline'", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
    }

    [Fact]
    public async Task PageIsNotPrerenderedOnTheServer()
    {
        using var client = server.CreateClient();

        var html = await client.GetStringAsync(new Uri("/", UriKind.Relative), TestContext.Current.CancellationToken);

        // Zero-Knowledge: Der Server liefert nur die Hülle, die Oberfläche entsteht im Browser.
        Assert.DoesNotContain("Willkommen bei myLife", html, StringComparison.Ordinal);
        Assert.Contains("_framework/blazor.web", html, StringComparison.Ordinal);
    }
}
