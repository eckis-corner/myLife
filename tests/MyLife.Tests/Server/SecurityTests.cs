using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using static MyLife.Tests.Server.TestToken;

namespace MyLife.Tests.Server;

/// <summary>Sicherheitsprüfung (M3.6): Wer was ohne Anmeldung erreicht, Admin-Rechte, Header, mitgelieferte Bibliotheken.</summary>
public sealed class SecurityTests(ServerFixture server) : IClassFixture<ServerFixture>
{
    /// <summary>Bewusst ohne Anmeldung erreichbar; jeder Eintrag hier ist eine Entscheidung (Architektur 4.4).</summary>
    private static readonly HashSet<string> PublicEndpoints = new(StringComparer.Ordinal)
    {
        // Info für die Startseite, Datenschutzhinweis
        "GET /api/info", "GET /api/privacy",
        // Einrichtung (nur solange es keinen Nutzer gibt, mit Einrichtungscode, Entscheidung 51)
        "GET /api/setup/status", "POST /api/setup/token", "POST /api/setup/totp", "POST /api/setup/totp/verify", "POST /api/setup/complete",
        // Anmeldung, zweiter Faktor (mit geschütztem Zwischen-Token), Wiederherstellung, Abmelden
        "POST /api/auth/prelogin", "POST /api/auth/login", "POST /api/auth/login/totp", "POST /api/auth/login/totp/setup",
        "POST /api/auth/login/totp/enroll", "POST /api/auth/login/passkey/options", "POST /api/auth/login/passkey",
        "POST /api/auth/recovery", "POST /api/auth/recovery/complete", "POST /api/auth/logout",
        // Einladung annehmen (Token und Geheimnis aus dem Link)
        "POST /api/invite/info", "POST /api/invite/accept", "POST /api/invite/totp", "POST /api/invite/totp/verify",
        // Links aus Mails mit Einmal-Token (Entscheidungen 77, 95)
        "POST /api/account-deletion/info", "POST /api/account-deletion/confirm", "POST /api/emergency/link/lifesign", "POST /api/emergency/link/objection",
    };

    [Fact]
    public void EveryApiEndpointRequiresALoginUnlessItIsPublicByDesign()
    {
        var anonymous = ApiEndpoints()
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is not null || !e.Metadata.GetOrderedMetadata<IAuthorizeData>().Any())
            .Select(Describe).Order(StringComparer.Ordinal).ToList();
        var unexpected = anonymous.Except(PublicEndpoints).ToList();
        var missing = PublicEndpoints.Except(anonymous).ToList();
        Assert.True(unexpected.Count == 0 && missing.Count == 0,
            $"Ohne Anmeldung erreichbar, aber nicht vorgesehen:\n{string.Join("\n", unexpected)}\nVorgesehen, aber nicht gefunden:\n{string.Join("\n", missing)}");
    }

    [Fact]
    public void AdminEndpointsRequireTheAdminRole()
    {
        var admin = ApiEndpoints().Where(e => e.RoutePattern.RawText!.StartsWith("/api/admin", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(admin);
        Assert.All(admin, e => Assert.Contains(e.Metadata.GetOrderedMetadata<IAuthorizeData>(), a => a.Policy == MyLife.Server.Api.AdminEndpoints.Policy));
    }

    [Fact]
    public async Task ResponsesDoNotRevealTheServerSoftware()
    {
        using var client = server.CreateClient();
        using var response = await client.GetAsync(new Uri("/api/info", UriKind.Relative), Ct);
        Assert.False(response.Headers.Contains("Server"));
        Assert.False(response.Headers.Contains("X-Powered-By"));
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public void VendoredLibrariesMatchTheirRecordedChecksum()
    {
        var root = Path.Combine(ResourceParityTests.FindRepositoryRoot(), "src", "MyLife.Client", "wwwroot", "lib");
        foreach (var versionFile in Directory.GetFiles(root, "VERSION", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(versionFile);
            var match = System.Text.RegularExpressions.Regex.Match(text, @"dist/(\S+), SHA-256 ([0-9a-f]{64})");
            Assert.True(match.Success, versionFile);
            var file = Path.Combine(Path.GetDirectoryName(versionFile)!, match.Groups[1].Value);
            Assert.Equal(match.Groups[2].Value, Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file))));
        }
    }

    private IEnumerable<RouteEndpoint> ApiEndpoints() =>
        server.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api", StringComparison.Ordinal) == true);

    private static string Describe(RouteEndpoint endpoint) =>
        $"{string.Join(",", endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()?.HttpMethods ?? [])} {endpoint.RoutePattern.RawText}";
}
