using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace MyLife.Tests;

/// <summary>Startet PostgreSQL in einem Container und den Server dagegen.</summary>
public sealed class ServerFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private readonly string _dataDirectory = Path.Combine(Path.GetTempPath(), "mylife-tests", Guid.NewGuid().ToString("N"));

    /// <summary>Uhr des Servers; Tests stellen sie vor, etwa für den nächsten TOTP-Schritt oder das Ende einer Sperre.</summary>
    public TestClock Clock { get; } = new();

    public async ValueTask InitializeAsync() => await _database.StartAsync();

    /// <summary>Client wie der Browser: HTTPS (Secure-Cookie), Cookies und der CSRF-Header für schreibende Aufrufe.</summary>
    public HttpClient CreateApiClient()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });
        client.DefaultRequestHeaders.Add(MyLife.Shared.Auth.ApiHeaders.Csrf, MyLife.Shared.Auth.ApiHeaders.CsrfValue);
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting("ConnectionStrings:MyLife", _database.GetConnectionString());
        builder.UseSetting("MyLife:DataDirectory", _dataDirectory);
        builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(Clock));
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
        if (Directory.Exists(_dataDirectory))
        {
            Directory.Delete(_dataDirectory, recursive: true);
        }
    }
}

/// <summary>Einstellbare Uhr für Tests. Startet bei der echten Zeit.</summary>
public sealed class TestClock : TimeProvider
{
    private DateTimeOffset _now = DateTimeOffset.UtcNow;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now = _now.Add(by);
}
