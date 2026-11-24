using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using MyLife.Server.Infrastructure.Mail;
using MyLife.Shared.Admin;
using static MyLife.Tests.Server.TestToken;

namespace MyLife.Tests.Server;

/// <summary>Der echte SMTP-Versand (MailKit) gegen Mailpit im Container: Umlaute, Text- und HTML-Teil, Link.</summary>
public sealed class SmtpTransportTests : IAsyncLifetime
{
    private readonly IContainer _mailpit = new ContainerBuilder("axllent/mailpit:v1.27")
        .WithPortBinding(1025, true)
        .WithPortBinding(8025, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(8025).ForPath("/livez")))
        .Build();

    public async ValueTask InitializeAsync() => await _mailpit.StartAsync();

    public async ValueTask DisposeAsync() => await _mailpit.DisposeAsync();

    [Fact]
    public async Task SendsTheRenderedMailOverSmtp()
    {
        var config = new SmtpConfig(_mailpit.Hostname, _mailpit.GetMappedPublicPort(1025), SmtpSecurity.None, null, null, "familie@example.org", "myLife Familie");
        var mail = MailTemplates.Render(MailTemplate.DeletionRequest, "de", new Dictionary<string, string>
        {
            ["name"] = "Jörg Müller", ["link"] = "https://mylife.example.org/account/delete#abc_DEF-123", ["days"] = "7", ["instance"] = "https://mylife.example.org",
        });

        await new SmtpMailTransport().SendAsync(config, MailComposer.Build(config, "joerg@example.org", mail), Ct);

        using var http = new HttpClient { BaseAddress = new Uri($"http://{_mailpit.Hostname}:{_mailpit.GetMappedPublicPort(8025)}") };
        using var list = JsonDocument.Parse(await http.GetStringAsync("/api/v1/messages", Ct));
        var summary = list.RootElement.GetProperty("messages")[0];
        Assert.Equal("Bitte bestätige: myLife-Konto löschen", summary.GetProperty("Subject").GetString());
        Assert.Equal("myLife Familie", summary.GetProperty("From").GetProperty("Name").GetString());

        using var message = JsonDocument.Parse(await http.GetStringAsync($"/api/v1/message/{summary.GetProperty("ID").GetString()}", Ct));
        var text = message.RootElement.GetProperty("Text").GetString()!;
        var html = message.RootElement.GetProperty("HTML").GetString()!;
        Assert.Contains("Hallo Jörg Müller", text, StringComparison.Ordinal);
        Assert.Contains("https://mylife.example.org/account/delete#abc_DEF-123", text, StringComparison.Ordinal);
        Assert.Contains("<a href=\"https://mylife.example.org/account/delete#abc_DEF-123\">", html, StringComparison.Ordinal);
        Assert.Contains("Der Link gilt 7 Tage", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnglishTemplatesAndConnectionErrors()
    {
        var mail = MailTemplates.Render(MailTemplate.AccountLocked, "en", new Dictionary<string, string> { ["name"] = "Anna", ["instance"] = "myLife" });
        Assert.Equal("Your myLife account has been locked", mail.Subject);
        Assert.Contains("This message was sent automatically by myLife (myLife).", mail.Text, StringComparison.Ordinal);
        Assert.All(MailTemplate.All, template => Assert.NotEmpty(MailTemplates.Render(template, "en", new Dictionary<string, string>()).Subject));

        var closed = new SmtpConfig(_mailpit.Hostname, 1, SmtpSecurity.None, null, null, "familie@example.org", "myLife");
        await Assert.ThrowsAnyAsync<Exception>(() => new SmtpMailTransport().SendAsync(closed, MailComposer.Build(closed, "a@example.org", mail), Ct));
    }
}
