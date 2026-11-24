using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace MyLife.Tests;

/// <summary>Startet PostgreSQL in einem Container und den Server dagegen.</summary>
public sealed class ServerFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private readonly string _dataDirectory = Path.Combine(Path.GetTempPath(), "mylife-tests", Guid.NewGuid().ToString("N"));

    public async ValueTask InitializeAsync() => await _database.StartAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseSetting("ConnectionStrings:MyLife", _database.GetConnectionString());
        builder.UseSetting("MyLife:DataDirectory", _dataDirectory);
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
