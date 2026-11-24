using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MyLife.Client.Services;
using MyLife.Crypto;
using MyLife.Server.Data;
using MyLife.Server.Infrastructure;
using MyLife.Server.Infrastructure.Admin;
using MyLife.Server.Infrastructure.Mail;
using MyLife.Shared.Admin;
using MyLife.Shared.Auth;
using MyLife.Shared.Vault;
using static MyLife.Tests.Server.TestToken;

namespace MyLife.Tests.Server;

/// <summary>Verwaltung (M2.1): Einstellungen, Mailversand, Benutzer, System-Protokoll, Datenschutz.</summary>
public sealed partial class AdminTests(ServerFixture server) : IClassFixture<ServerFixture>, IAsyncLifetime
{
    private AuthTestSupport _account = null!;

    public async ValueTask InitializeAsync() => _account = await AuthTestSupport.ForAsync(server);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task SettingsRoundTripWithoutRevealingSecrets()
    {
        var admin = await AdminAsync();
        var saved = await admin.SaveSettingsAsync(MailSettings() with { Ntfy = new NtfySettingsDto { ServerUrl = "https://ntfy.example.org/", AccessToken = "tk_geheim" } }, Ct);

        Assert.True(saved.Smtp.HasPassword);
        Assert.Null(saved.Smtp.Password);
        Assert.True(saved.Ntfy.HasAccessToken);
        Assert.Null(saved.Ntfy.AccessToken);
        Assert.Equal("https://ntfy.example.org", saved.Ntfy.ServerUrl);
        Assert.DoesNotContain("smtp-geheim", await StoredSettingsAsync(), StringComparison.Ordinal);
        Assert.DoesNotContain("tk_geheim", await StoredSettingsAsync(), StringComparison.Ordinal);

        // Ohne neues Passwort bleibt das alte, eine leere Zeichenkette entfernt es.
        Assert.True((await admin.SaveSettingsAsync(saved with { Smtp = saved.Smtp with { Port = 2525 } }, Ct)).Smtp.HasPassword);
        Assert.False((await admin.SaveSettingsAsync(saved with { Smtp = saved.Smtp with { Password = string.Empty } }, Ct)).Smtp.HasPassword);

        var invalid = saved with { Deadlines = saved.Deadlines with { LifesignIntervalDays = new DayRange(30, 40, 60), ReminderDays = [7, 3] } };
        var error = await Assert.ThrowsAsync<AdminException>(() => admin.SaveSettingsAsync(invalid, Ct));
        Assert.Equal(AdminErrors.Invalid, error.Message);
        Assert.Equal(SettingsErrors.Range, error.Fields["deadlines.lifesign"]);
        Assert.Equal(SettingsErrors.Order, error.Fields["deadlines.reminders"]);
    }

    [Fact]
    public async Task IdleTimeoutFromTheSettingsAppliesToSessionsAndCookie()
    {
        var admin = await AdminAsync();
        var settings = await admin.GetSettingsAsync(Ct);
        await admin.SaveSettingsAsync(settings with { IdleTimeoutMinutes = 7 }, Ct);
        try
        {
            var (session, _, _) = await _account.LoginAsync();
            Assert.Equal(7, session.Session!.IdleTimeoutMinutes);
            var cookies = server.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>();
            Assert.Equal(TimeSpan.FromMinutes(7), cookies.Get(CookieAuthenticationDefaults.AuthenticationScheme).ExpireTimeSpan);
        }
        finally
        {
            await admin.SaveSettingsAsync(settings, Ct);
        }
    }

    [Fact]
    public async Task OnlyAdministratorsReachTheAdministration()
    {
        var other = await OtherUserAsync("nutzer@example.org");
        using var response = await other.Http.GetAsync("api/admin/settings", Ct);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(AdminErrors.Failed, (await Assert.ThrowsAsync<AdminException>(() => new AdminClient(other.Http).GetUsersAsync(Ct))).Message);

        using var anonymous = server.CreateApiClient();
        using var unauthorized = await anonymous.GetAsync("api/admin/users", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
    }

    [Fact]
    public async Task TestMailGoesToTheAdminAndReportsErrors()
    {
        var admin = await AdminAsync();
        await admin.SaveSettingsAsync(MailSettings(), Ct);

        var result = await admin.TestMailAsync(Ct);
        Assert.True(result.Success, result.Error);
        var mail = server.Mail.To(AuthTestSupport.Email).Last();
        Assert.Equal("myLife: Testnachricht", mail.Subject);
        Assert.Contains("Mailversand von myLife funktioniert", mail.TextBody, StringComparison.Ordinal);
        Assert.Contains("https://mylife.example.org", mail.TextBody, StringComparison.Ordinal);
        Assert.Equal("familie@example.org", mail.From.Mailboxes.Single().Address);

        server.Mail.Fail = true;
        try
        {
            var failed = await admin.TestMailAsync(Ct);
            Assert.False(failed.Success);
            Assert.Contains("Verbindung abgelehnt", failed.Error, StringComparison.Ordinal);
        }
        finally
        {
            server.Mail.Fail = false;
        }

        Assert.Contains((await admin.GetAuditAsync(0, 100, Ct)).Items, e => e.Action == AuditActions.AdminMailTested);
    }

    [Fact]
    public async Task NtfyTestPublishesJsonWithToken()
    {
        var admin = await AdminAsync();
        var settings = await admin.GetSettingsAsync(Ct);
        await admin.SaveSettingsAsync(settings with { Ntfy = new NtfySettingsDto { ServerUrl = "https://ntfy.example.org", AccessToken = "tk_test" } }, Ct);

        Assert.True((await admin.TestNtfyAsync("familie-test", Ct)).Success);
        var (request, body) = server.Ntfy.Requests[^1];
        Assert.Equal("https://ntfy.example.org/", request.RequestUri!.ToString());
        Assert.Equal("Bearer tk_test", request.Headers.Authorization!.ToString());
        Assert.Contains("\"topic\":\"familie-test\"", body, StringComparison.Ordinal);

        Assert.Equal(AdminErrors.Invalid, (await Assert.ThrowsAsync<AdminException>(() => admin.TestNtfyAsync("kein thema!", Ct))).Message);
        server.Ntfy.Status = HttpStatusCode.Forbidden;
        try
        {
            Assert.False((await admin.TestNtfyAsync("familie-test", Ct)).Success);
        }
        finally
        {
            server.Ntfy.Status = HttpStatusCode.OK;
        }
    }

    [Fact]
    public async Task OutboxRetriesWithBackoffAndGivesUpAfterTwoDays()
    {
        var admin = await AdminAsync();
        var users = await admin.GetUsersAsync(Ct);
        await admin.SaveSettingsAsync(MailSettings(), Ct);
        var outbox = server.Services.GetRequiredService<MailOutboxService>();
        await outbox.ProcessOnceAsync(Ct);

        var target = await OtherUserIdAsync("outbox@example.org");
        server.Mail.Fail = true;
        try
        {
            await admin.LockAsync(target, Ct);
            await outbox.ProcessOnceAsync(Ct);
            var entry = await OutboxEntryAsync("outbox@example.org");
            Assert.Equal(1, entry.Attempts);
            Assert.Null(entry.SentAt);
            Assert.Equal(server.Clock.GetUtcNow().AddMinutes(1), entry.NextAttemptAt);
            Assert.NotNull(entry.Parameters);

            for (var attempt = 1; attempt < MailOutboxService.MaxAttempts; attempt++)
            {
                server.Clock.Advance(TimeSpan.FromDays(1));
                await outbox.ProcessOnceAsync(Ct);
            }

            entry = await OutboxEntryAsync("outbox@example.org");
            Assert.NotNull(entry.FailedAt);
            Assert.Null(entry.Parameters);

            // Die Uhr ist Tage weiter: Die Sitzung ist abgelaufen.
            admin = await AdminAsync();
            var queue = await admin.GetMailQueueAsync(Ct);
            Assert.True(queue.Failed >= 1);
            Assert.Contains("Verbindung abgelehnt", queue.LastError, StringComparison.Ordinal);
            Assert.Contains((await admin.GetAuditAsync(0, 100, Ct)).Items, e => e.Action == AuditActions.MailFailed && e.Details == MailTemplate.AccountLocked);
        }
        finally
        {
            server.Mail.Fail = false;
        }

        Assert.NotEmpty(users);
    }

    [Fact]
    public async Task LockedAccountsAreSignedOutAndCannotSignIn()
    {
        var admin = await AdminAsync();
        await admin.SaveSettingsAsync(MailSettings(), Ct);
        const string email = "gesperrt@example.org";
        var secret = await VaultIsolationTests.CreateUserAsync(server, email);
        var other = await VaultBrowser.OpenAsync(_account, email, () => Code(secret));
        var id = other.Session.Session!.UserId;

        var locked = await admin.LockAsync(id, Ct);
        Assert.Equal(AccountStatus.Locked, locked.Status);
        Assert.Null(await new AuthClient(other.Http).GetSessionAsync(Ct));

        var (browser, _, _) = _account.NewBrowser();
        var error = await Assert.ThrowsAsync<AuthException>(() => browser.BeginLoginAsync(email, SetupTestSupport.Password));
        Assert.Equal(AuthErrors.Disabled, error.Message);
        // Mit falschem Passwort verrät die Antwort die Sperre nicht.
        var wrong = await Assert.ThrowsAsync<AuthException>(() => browser.BeginLoginAsync(email, "falsches Passwort 123"));
        Assert.Equal(AuthErrors.InvalidCredentials, wrong.Message);

        await server.Services.GetRequiredService<MailOutboxService>().ProcessOnceAsync(Ct);
        Assert.Contains(server.Mail.To(email), m => m.Subject == "Dein myLife-Konto wurde gesperrt" && (m.TextBody ?? string.Empty).Contains("Hallo Zweite", StringComparison.Ordinal));

        Assert.Equal(AccountStatus.Active, (await admin.UnlockAsync(id, Ct)).Status);
        var again = await VaultBrowser.OpenAsync(_account, email, () => Code(secret));
        Assert.Equal(SessionState.Unlocked, again.Session.State);

        var audit = (await admin.GetAuditAsync(0, 100, Ct)).Items;
        Assert.Contains(audit, e => e.Action == AuditActions.AdminUserLocked && e.Details == "Zweite" && e.ActorName is not null);
        Assert.Contains(audit, e => e.Action == AuditActions.AdminUserUnlocked);
        // Die Person sieht die Aktion auch in ihrem eigenen Protokoll.
        Assert.Contains((await again.Api.GetAuditAsync(0, 100, Ct)).Items, e => e.Action == AuditActions.AdminUserLocked);
    }

    [Fact]
    public async Task AdministratorsCannotLockThemselvesOrRemoveTheLastAdmin()
    {
        var admin = await AdminAsync();
        var me = (await admin.GetUsersAsync(Ct)).Single(u => u.IsMe);

        Assert.Equal(AdminErrors.Self, (await Assert.ThrowsAsync<AdminException>(() => admin.LockAsync(me.Id, Ct))).Message);
        Assert.Equal(AdminErrors.Self, (await Assert.ThrowsAsync<AdminException>(() => admin.SetAdminAsync(me.Id, false, Ct))).Message);
        Assert.Equal(AdminErrors.NotFound, (await Assert.ThrowsAsync<AdminException>(() => admin.LockAsync(Guid.NewGuid(), Ct))).Message);

        // Ein zweiter Admin bekommt das Recht; seine Sitzung endet, damit die Rolle sofort gilt.
        const string email = "admin2@example.org";
        var secret = await VaultIsolationTests.CreateUserAsync(server, email);
        var other = await VaultBrowser.OpenAsync(_account, email, () => Code(secret));
        Assert.True((await admin.SetAdminAsync(other.Session.Session!.UserId, true, Ct)).IsAdmin);
        Assert.Null(await new AuthClient(other.Http).GetSessionAsync(Ct));
        var asAdmin = await VaultBrowser.OpenAsync(_account, email, () => Code(secret));
        Assert.True(asAdmin.Session.Session!.IsAdmin);
        Assert.NotEmpty(await new AdminClient(asAdmin.Http).GetUsersAsync(Ct));
        Assert.False((await admin.SetAdminAsync(other.Session.Session!.UserId, false, Ct)).IsAdmin);
    }

    [Fact]
    public async Task ResetTwoFactorRequiresANewAuthenticatorAtTheNextSignIn()
    {
        var admin = await AdminAsync();
        const string email = "reset@example.org";
        var secret = await VaultIsolationTests.CreateUserAsync(server, email);
        var id = (await VaultBrowser.OpenAsync(_account, email, () => Code(secret))).Session.Session!.UserId;

        var reset = await admin.ResetTwoFactorAsync(id, Ct);
        Assert.False(reset.HasTotp);
        Assert.Equal(0, reset.PasskeyCount);

        var (session, _, _) = _account.NewBrowser();
        var challenge = await session.BeginLoginAsync(email, SetupTestSupport.Password);
        Assert.True(challenge.Enroll);
        var enrollment = await session.BeginTotpEnrollmentAsync(challenge.Pending);
        Assert.NotEqual(secret, enrollment.Secret);

        var wrong = await Assert.ThrowsAsync<AuthException>(() => session.CompleteLoginWithEnrollmentAsync(challenge.Pending, enrollment.Enrollment, Code(secret)));
        Assert.Equal(AuthErrors.InvalidCode, wrong.Message);
        await session.CompleteLoginWithEnrollmentAsync(challenge.Pending, enrollment.Enrollment, Code(enrollment.Secret));
        Assert.Equal(SessionState.Unlocked, session.State);

        // Danach gilt die neue App wie gewohnt, die Einrichtung ist nicht noch einmal möglich.
        var next = await VaultBrowser.OpenAsync(_account, email, () => Code(enrollment.Secret));
        Assert.Equal(SessionState.Unlocked, next.Session.State);
        Assert.Contains((await next.Api.GetAuditAsync(0, 50, Ct)).Items, e => e.Action == AuditActions.TotpEnrolled);
    }

    [Fact]
    public async Task DeletionNeedsTheUsersConfirmationAndRemovesEverything()
    {
        var admin = await AdminAsync();
        const string email = "loeschen@example.org";
        var secret = await VaultIsolationTests.CreateUserAsync(server, email);
        var other = await VaultBrowser.OpenAsync(_account, email, () => Code(secret));
        var id = other.Session.Session!.UserId;
        var item = await other.Vault.CreateAsync("pet", new VaultPayload { Title = "Bello", Fields = { ["petName"] = "Bello" } });
        var file = await other.Vault.UploadAsync(item.Id, new MemoryStream([1, 2, 3]), new DocumentMetadata { FileName = "foto.jpg", Size = 3 });

        var settings = await admin.GetSettingsAsync(Ct);
        await admin.SaveSettingsAsync(settings with { Smtp = new SmtpSettingsDto() }, Ct);
        Assert.Equal(AdminErrors.MailNotConfigured, (await Assert.ThrowsAsync<AdminException>(() => admin.RequestDeletionAsync(id, Ct))).Message);

        await admin.SaveSettingsAsync(MailSettings(), Ct);
        Assert.True((await admin.RequestDeletionAsync(id, Ct)).DeletionPending);
        await server.Services.GetRequiredService<MailOutboxService>().ProcessOnceAsync(Ct);
        var mail = server.Mail.To(email).Last(m => (m.Subject ?? string.Empty).Contains("löschen", StringComparison.Ordinal));
        var token = DeletionLink().Match(mail.TextBody ?? string.Empty).Groups[1].Value;
        Assert.Contains($"href=\"https://mylife.example.org/account/delete#{token}\"", mail.HtmlBody, StringComparison.Ordinal);

        using var anonymous = server.CreateApiClient();
        var anon = new AdminClient(anonymous);
        Assert.Equal(AdminErrors.TokenInvalid, (await Assert.ThrowsAsync<AdminException>(() => anon.GetDeletionAsync("x" + token[1..], Ct))).Message);
        var info = await anon.GetDeletionAsync(token, Ct);
        Assert.Equal(email, info.Email);

        // Der Token in der Datenbank ist nur ein Hash.
        string storageKey;
        await using (var scope = server.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MyLifeDbContext>();
            Assert.Equal(32, (await db.OneTimeTokens.SingleAsync(t => t.UserId == id, Ct)).TokenHash.Length);
            storageKey = (await db.Documents.SingleAsync(d => d.Id == file.Id, Ct)).StorageKey;
        }

        Assert.Single(StoredFiles(storageKey));

        await anon.ConfirmDeletionAsync(token, Ct);
        Assert.Equal(AdminErrors.TokenInvalid, (await Assert.ThrowsAsync<AdminException>(() => anon.ConfirmDeletionAsync(token, Ct))).Message);

        await using (var scope = server.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MyLifeDbContext>();
            Assert.False(await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Id == id, Ct));
            Assert.False(await db.Objects.AnyAsync(o => o.OwnerId == id, Ct));
            Assert.False(await db.Documents.AnyAsync(d => d.OwnerId == id, Ct));
            Assert.False(await db.AuditLog.AnyAsync(a => a.OwnerId == id, Ct));
        }

        Assert.Empty(StoredFiles(storageKey));
        Assert.Null(await new AuthClient(other.Http).GetSessionAsync(Ct));
        Assert.Contains((await admin.GetAuditAsync(0, 100, Ct)).Items, e => e.Action == AuditActions.AccountDeleted && e.TargetId == id);
        await server.Services.GetRequiredService<MailOutboxService>().ProcessOnceAsync(Ct);
        Assert.Contains(server.Mail.To(email), m => m.Subject == "Dein myLife-Konto wurde gelöscht");
    }

    [Fact]
    public async Task PrivacyNoticeIsPublic()
    {
        var admin = await AdminAsync();
        using var anonymous = server.CreateApiClient();
        var settings = await admin.GetSettingsAsync(Ct);
        await admin.SaveSettingsAsync(settings with { PrivacyNotice = null }, Ct);
        Assert.True((await new AdminClient(anonymous).GetPrivacyAsync(Ct)).IsDefault);

        await admin.SaveSettingsAsync(settings with { PrivacyNotice = "  Verantwortlich: Familie Beispiel.  " }, Ct);
        var notice = await new AdminClient(anonymous).GetPrivacyAsync(Ct);
        Assert.False(notice.IsDefault);
        Assert.Equal("Verantwortlich: Familie Beispiel.", notice.Text);
    }

    [Fact]
    public async Task OldLogEntriesAreRemovedAfterTheRetentionPeriod()
    {
        var admin = await AdminAsync();
        var settings = await admin.GetSettingsAsync(Ct);
        await admin.SaveSettingsAsync(settings with { AuditRetentionDays = 30 }, Ct);
        await using (var scope = server.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MyLifeDbContext>();
            db.AuditLog.Add(new AuditEntry { At = server.Clock.GetUtcNow().AddDays(-31), Action = "admin-settings-changed", Details = "alt" });
            db.AuditLog.Add(new AuditEntry { At = server.Clock.GetUtcNow().AddDays(-29), Action = "admin-settings-changed", Details = "neu" });
            await db.SaveChangesAsync(Ct);
        }

        await server.Services.GetRequiredService<TrashPurgeService>().PurgeOnceAsync(Ct);

        await using (var scope = server.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MyLifeDbContext>();
            Assert.False(await db.AuditLog.AnyAsync(a => a.Details == "alt", Ct));
            Assert.True(await db.AuditLog.AnyAsync(a => a.Details == "neu", Ct));
        }

        await admin.SaveSettingsAsync(settings, Ct);
    }

    private static AdminSettingsDto MailSettings() => new()
    {
        PublicUrl = "https://mylife.example.org/",
        Smtp = new SmtpSettingsDto
        {
            Host = "smtp.example.org", Port = 587, Username = "familie", Password = "smtp-geheim", FromAddress = "familie@example.org", FromName = "myLife Familie",
        },
    };

    private async Task<AdminClient> AdminAsync()
    {
        var (_, _, http) = await _account.LoginAsync();
        return new AdminClient(http);
    }

    private async Task<VaultBrowser> OtherUserAsync(string email)
    {
        var secret = await VaultIsolationTests.CreateUserAsync(server, email);
        return await VaultBrowser.OpenAsync(_account, email, () => Code(secret));
    }

    private async Task<Guid> OtherUserIdAsync(string email) => (await OtherUserAsync(email)).Session.Session!.UserId;

    private async Task<MailOutboxEntry> OutboxEntryAsync(string email)
    {
        await using var scope = server.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<MyLifeDbContext>().MailOutbox.AsNoTracking()
            .Where(m => m.Recipient == email).OrderByDescending(m => m.CreatedAt).FirstAsync(Ct);
    }

    private async Task<string> StoredSettingsAsync()
    {
        await using var scope = server.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<MyLifeDbContext>().SystemSettings.AsNoTracking()
            .SingleAsync(s => s.Key == SettingsStore.SettingKey, Ct)).Value!;
    }

    private IEnumerable<string> StoredFiles(string storageKey) =>
        Directory.EnumerateFiles(Path.GetFullPath(server.Services.GetRequiredService<IOptions<MyLifeOptions>>().Value.DataDirectory), "*", SearchOption.AllDirectories)
            .Where(f => f.Contains(storageKey, StringComparison.Ordinal));

    private string Code(string secret)
    {
        server.Clock.Advance(TimeSpan.FromSeconds(Totp.PeriodSeconds));
        return Totp.Compute(Base32.Decode(secret), Totp.StepAt(server.Clock.GetUtcNow()));
    }

    [GeneratedRegex(@"/account/delete#([A-Za-z0-9_\-]+)")]
    private static partial Regex DeletionLink();
}
