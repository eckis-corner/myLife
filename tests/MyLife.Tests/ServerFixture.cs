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

    /// <summary>Ersetzt den SMTP-Versand: Tests lesen hier die verschickten Mails.</summary>
    public FakeMailTransport Mail { get; } = new();

    /// <summary>Ersetzt den ntfy-Server.</summary>
    public FakeHttpHandler Ntfy { get; } = new();

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
        // Die Tests melden sich viel öfter an als ein Mensch; das Limit gegen Durchprobieren würde sonst greifen.
        builder.UseSetting("MyLife:AuthRequestsPerMinute", "10000");
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<TimeProvider>(Clock);
            services.AddSingleton<MyLife.Server.Infrastructure.Mail.IMailTransport>(Mail);
            services.AddHttpClient(MyLife.Server.Infrastructure.Admin.NtfyClient.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => Ntfy).SetHandlerLifetime(Timeout.InfiniteTimeSpan);
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
        Ntfy.Dispose();
        if (Directory.Exists(_dataDirectory))
        {
            Directory.Delete(_dataDirectory, recursive: true);
        }
    }
}

/// <summary>
/// Einstellbare Uhr für Tests. Startet bei der echten Zeit, gekappt auf ganze Mikrosekunden:
/// PostgreSQL speichert nicht genauer, unter Linux liefert die Systemuhr aber 100-ns-Ticks.
/// Sonst unterscheiden sich gespeicherte und erwartete Zeitpunkte um den Rest.
/// </summary>
public sealed class TestClock : TimeProvider
{
    private DateTimeOffset _now = TruncateToMicroseconds(DateTimeOffset.UtcNow);

    private static DateTimeOffset TruncateToMicroseconds(DateTimeOffset value) =>
        value.AddTicks(-(value.Ticks % TimeSpan.TicksPerMicrosecond));

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now = _now.Add(by);
}

/// <summary>Mailversand für Tests: schreibt mit statt zu senden; mit <see cref="Fail"/> scheitert jeder Versand.</summary>
public sealed class FakeMailTransport : MyLife.Server.Infrastructure.Mail.IMailTransport
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<MimeKit.MimeMessage> _sent = new();

    public bool Fail { get; set; }

    public IReadOnlyList<MimeKit.MimeMessage> Sent => [.. _sent];

    public IEnumerable<MimeKit.MimeMessage> To(string email) =>
        _sent.Where(m => m.To.Mailboxes.Any(a => string.Equals(a.Address, email, StringComparison.OrdinalIgnoreCase)));

    Task MyLife.Server.Infrastructure.Mail.IMailTransport.SendAsync(
        MyLife.Server.Infrastructure.Mail.SmtpConfig config, MimeKit.MimeMessage message, CancellationToken cancellationToken)
    {
        if (Fail)
        {
            throw new IOException("Verbindung abgelehnt (Test)");
        }

        _sent.Enqueue(message);
        return Task.CompletedTask;
    }
}

/// <summary>HTTP-Gegenstelle für Tests (ntfy): schreibt Anfragen mit und antwortet mit <see cref="Status"/>.</summary>
public sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<(HttpRequestMessage Request, string Body)> _requests = new();

    public System.Net.HttpStatusCode Status { get; set; } = System.Net.HttpStatusCode.OK;

    public IReadOnlyList<(HttpRequestMessage Request, string Body)> Requests => [.. _requests];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        _requests.Enqueue((request, body));
        return new HttpResponseMessage(Status);
    }
}
