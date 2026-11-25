using Microsoft.Extensions.DependencyInjection;
using MyLife.Client.Services;
using MyLife.Server.Infrastructure.Mail;
using MyLife.Server.Infrastructure.Reminders;
using MyLife.Shared.Admin;
using MyLife.Shared.Reminders;
using MyLife.Shared.Vault;
using static MyLife.Tests.Server.TestToken;

namespace MyLife.Tests.Server;

/// <summary>Erinnerungen (M3.1): nächster Termin ohne Zusammenhang, Prüfintervall der Bereiche, Mail an oder aus.</summary>
public sealed class ReminderTests(ServerFixture server) : IClassFixture<ServerFixture>, IAsyncLifetime
{
    private const string Subject = "myLife: Zeit für einen Blick in deinen Notfallordner";
    private AuthTestSupport _account = null!;

    public async ValueTask InitializeAsync()
    {
        _account = await AuthTestSupport.ForAsync(server);
        var (_, _, http) = await _account.LoginAsync();
        var admin = new AdminClient(http);
        var settings = await admin.GetSettingsAsync(Ct);
        await admin.SaveSettingsAsync(settings with
        {
            PublicUrl = "https://mylife.example.org",
            Smtp = new SmtpSettingsDto { Host = "smtp.example.org", FromAddress = "familie@example.org" },
        }, Ct);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task TheBrowserReportsOnlyTheNextDateAndTheMailComesOnceWithoutDetails()
    {
        var (email, browser) = await UserAsync("termin");
        var today = DateOnly.FromDateTime(server.Clock.GetLocalNow().DateTime);
        await browser.Vault.CreateAsync("idDocument", new VaultPayload { Title = "Personalausweis", ExpiresOn = today.AddDays(70) });
        await browser.Vault.CreateAsync("contract", new VaultPayload { Title = "Handyvertrag", FollowUpOn = today.AddDays(30) });
        var reminders = new ReminderSync(new ReminderClient(browser.Http), browser.Vault, server.Clock);

        // Vorlauf 60 Tage: Der Ausweis zählt ab Tag 10, die Wiedervorlage ab Tag 30; der Server erfährt nur Tag 10.
        await reminders.SyncAsync();
        var status = await new ReminderClient(browser.Http).GetAsync(Ct);
        Assert.Equal(today.AddDays(10), status.NextDueOn);
        Assert.Equal(ReminderSettingsDto.Default, status.Settings);

        await TickAsync(TimeSpan.FromDays(8));
        Assert.DoesNotContain(server.Mail.To(email), m => m.Subject == Subject);
        await TickAsync(TimeSpan.FromDays(3));
        var mail = Assert.Single(server.Mail.To(email), m => m.Subject == Subject);
        Assert.Contains("Ablaufdatum oder eine Wiedervorlage", mail.TextBody, StringComparison.Ordinal);
        Assert.Contains("https://mylife.example.org", mail.TextBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Personalausweis", mail.TextBody, StringComparison.Ordinal);

        // Derselbe Termin löst keine zweite Mail aus.
        await TickAsync(TimeSpan.FromDays(2));
        Assert.Single(server.Mail.To(email), m => m.Subject == Subject);
    }

    [Fact]
    public async Task AreasDueForReviewAreRemindedMonthlyUntilMarkedReviewed()
    {
        var (email, browser) = await UserAsync("pruefen");
        await browser.Vault.CreateAsync("bankAccount", VaultSamples.BankAccount("Girokonto", "Prüfen"));

        await TickAsync(TimeSpan.FromDays(150));
        Assert.DoesNotContain(server.Mail.To(email), m => m.Subject == Subject);
        await TickAsync(TimeSpan.FromDays(40));
        var mail = Assert.Single(server.Mail.To(email), m => m.Subject == Subject);
        Assert.Contains("1 Bereiche wurden seit mehr als 6 Monaten nicht geprüft", mail.TextBody, StringComparison.Ordinal);

        // Höchstens alle 30 Tage, solange nichts geprüft ist.
        await TickAsync(TimeSpan.FromDays(10));
        Assert.Single(server.Mail.To(email), m => m.Subject == Subject);
        await TickAsync(TimeSpan.FromDays(21));
        Assert.Equal(2, server.Mail.To(email).Count(m => m.Subject == Subject));

        // Als geprüft markiert, ist Ruhe bis zum nächsten Intervall.
        var again = await VaultBrowser.OpenAsync(_account, email, () => FamilyTests.Code(server, _secrets[email]));
        await again.Vault.MarkAreaReviewedAsync(1);
        await TickAsync(TimeSpan.FromDays(60));
        Assert.Equal(2, server.Mail.To(email).Count(m => m.Subject == Subject));
    }

    [Fact]
    public async Task WithoutMailOrWithInvalidSettingsNothingIsSent()
    {
        var (email, browser) = await UserAsync("ruhe");
        var api = new ReminderClient(browser.Http);
        await Assert.ThrowsAsync<HttpRequestException>(() => api.SaveSettingsAsync(new ReminderSettingsDto(30, 60, true), Ct));
        await Assert.ThrowsAsync<HttpRequestException>(() => api.SaveSettingsAsync(new ReminderSettingsDto(6, 2, true), Ct));
        var saved = await api.SaveSettingsAsync(new ReminderSettingsDto(0, 30, false), Ct);
        Assert.False(saved.Settings.MailEnabled);
        await api.SetNextDueAsync(DateOnly.FromDateTime(server.Clock.GetUtcNow().UtcDateTime), Ct);
        await browser.Vault.CreateAsync("bankAccount", VaultSamples.BankAccount("Sparbuch", "Ruhe"));

        await TickAsync(TimeSpan.FromDays(400));
        Assert.DoesNotContain(server.Mail.To(email), m => m.Subject == Subject);
    }

    [Fact]
    public void TheNextDateSkipsWhatIsAlreadyWithinTheLeadTimeAndEndedEntries()
    {
        var today = new DateOnly(2026, 11, 25);
        VaultItem Item(string type, DateOnly? expires = null, DateOnly? followUp = null, ObjectStatus status = ObjectStatus.Active) => new()
        {
            Id = Guid.NewGuid(), Area = 1, Type = ObjectCatalog.Find(type)!, Status = status,
            Payload = new VaultPayload { Title = type, ExpiresOn = expires, FollowUpOn = followUp },
        };

        Assert.Null(ReminderSync.NextDue([], 60, today));
        Assert.Equal(today.AddDays(5), ReminderSync.NextDue(
        [
            Item("idDocument", expires: today.AddDays(30)),                       // schon im Vorlauf: steht auf der Übersicht
            Item("contract", expires: today.AddDays(65)),                     // Tag 5
            Item("contract", followUp: today.AddDays(7)),                     // Tag 7
            Item("card", expires: today.AddDays(61), status: ObjectStatus.Ended),
        ], 60, today));
        Assert.Equal(today, ReminderSync.NextDue([Item("contract", followUp: today)], 60, today));
    }

    private readonly Dictionary<string, string> _secrets = [];

    private async Task<(string Email, VaultBrowser Browser)> UserAsync(string name)
    {
        var email = $"{name}.erinnerung@example.org";
        var secret = await VaultIsolationTests.CreateUserAsync(server, email);
        _secrets[email] = secret;
        return (email, await VaultBrowser.OpenAsync(_account, email, () => FamilyTests.Code(server, secret)));
    }

    private async Task TickAsync(TimeSpan advance)
    {
        server.Clock.Advance(advance);
        await server.Services.GetRequiredService<ReminderService>().ProcessOnceAsync(Ct);
        await server.Services.GetRequiredService<MailOutboxService>().ProcessOnceAsync(Ct);
    }
}
