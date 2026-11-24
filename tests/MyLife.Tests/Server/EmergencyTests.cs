using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyLife.Client.Services;
using MyLife.Server.Data;
using MyLife.Server.Infrastructure.Emergency;
using MyLife.Server.Infrastructure.Mail;
using MyLife.Shared.Admin;
using MyLife.Shared.Emergency;
using MyLife.Shared.Trust;
using MyLife.Shared.Vault;
using static MyLife.Tests.Server.TestToken;

namespace MyLife.Tests.Server;

/// <summary>Notfallzugang (M2.4, Architektur 5.1): Lebenszeichen, Erinnerungen, Freigabe, Antrag, Widerspruch, Mailausfall.</summary>
public sealed partial class EmergencyTests(ServerFixture server) : IClassFixture<ServerFixture>, IAsyncLifetime
{
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
    public async Task WithoutALifesignRemindersFollowAndBothModesAreReleased()
    {
        var family = await FamilyAsync("timeout");
        await family.Owner.Emergency.SaveSettingsAsync(new EmergencySettingsDto(7, 7, 7, null, null), Ct);

        // Vor Ablauf des Intervalls passiert nichts.
        await TickAsync(TimeSpan.FromDays(6));
        Assert.Equal(EmergencyState.Active, (await SwitchAsync(family.Owner)).State);

        await TickAsync(TimeSpan.FromDays(1.1));
        var state = await SwitchAsync(family.Owner);
        Assert.Equal(EmergencyState.Overdue, state.State);
        Assert.NotNull(state.StageSentAt);
        var due = server.Mail.To(family.Owner.Email).Last();
        Assert.Equal("myLife: Bitte gib ein Lebenszeichen", due.Subject);
        Assert.Matches(LifesignLink(), due.TextBody ?? string.Empty);

        // Erinnerungen nach 3, 7 und 14 Tagen (Standard), jeweils ab dem Versand der vorigen Mail gerechnet.
        await TickAsync(TimeSpan.FromDays(3.1));
        await TickAsync(TimeSpan.FromDays(4.1));
        await TickAsync(TimeSpan.FromDays(7.1));
        Assert.Equal(3, server.Mail.To(family.Owner.Email).Count(m => (m.Subject ?? string.Empty).StartsWith("myLife: Erinnerung", StringComparison.Ordinal)));
        Assert.Equal(EmergencyState.Waiting, (await SwitchAsync(family.Owner)).State);
        Assert.Empty(await (await family.Trustee.LoginAsync()).Shared.GetOwnersAsync());

        // Nach der Wartezeit sind beide Modi frei; die Vertrauensperson liest, was für sie hinterlegt ist.
        await TickAsync(TimeSpan.FromDays(7.1));
        state = await SwitchAsync(family.Owner);
        Assert.Equal(EmergencyState.Released, state.State);
        Assert.Equal(ReleasedModes.Death | ReleasedModes.Incapacity, state.ReleasedModes);
        var trustee = await family.Trustee.LoginAsync();
        var shared = (await trustee.Shared.GetOwnersAsync()).Single();
        Assert.Contains(shared.Grants, g => g.Dto.Mode == GrantMode.Death && g.Verified);
        var content = await trustee.Shared.OpenAsync(shared.OwnerId, shared.Grants.First(g => g.Dto.Mode == GrantMode.Death).Dto);
        Assert.Contains(content.Items, i => i.Title == "Lebensversicherung");
        Assert.Contains(server.Mail.To(family.Trustee.Email), m => m.Subject == $"myLife: Zugang zu {family.Owner.Name} freigegeben");
        Assert.Contains(server.Mail.To(family.Owner.Email), m => m.Subject == "myLife: Dein Notfallzugang wurde freigegeben");

        // Eine Anmeldung danach ist ein Lebenszeichen, hebt die Freigabe aber nicht auf (Rücknahme mit M2.6).
        var status = await (await family.Owner.LoginAsync()).Emergency.GetStatusAsync(Ct);
        Assert.Equal(ReleasedModes.Death | ReleasedModes.Incapacity, status.ReleasedModes);
    }

    [Fact]
    public async Task TheLinkInTheMailIsALifesignAndWorksOnce()
    {
        var family = await FamilyAsync("link");
        await family.Owner.Emergency.SaveSettingsAsync(new EmergencySettingsDto(7, 7, 7, "zweite.link@example.org", null), Ct);
        await TickAsync(TimeSpan.FromDays(7.1));
        Assert.Equal(EmergencyState.Overdue, (await SwitchAsync(family.Owner)).State);
        Assert.Contains(server.Mail.To("zweite.link@example.org"), m => m.Subject == "myLife: Bitte gib ein Lebenszeichen");

        var token = LifesignLink().Match(server.Mail.To(family.Owner.Email).Last().TextBody ?? string.Empty).Groups[1].Value;
        using var anonymous = server.CreateApiClient();
        var links = new EmergencyClient(anonymous);
        Assert.Equal(family.Owner.Name, (await links.LifesignLinkAsync(token, Ct)).OwnerName);
        Assert.Equal(EmergencyState.Active, (await SwitchAsync(family.Owner)).State);
        Assert.Equal(EmergencyErrors.TokenInvalid, (await Assert.ThrowsAsync<EmergencyException>(() => links.LifesignLinkAsync(token, Ct))).Message);

        // Die neue Runde beginnt mit dem Lebenszeichen von eben.
        await TickAsync(TimeSpan.FromDays(6));
        Assert.Equal(EmergencyState.Active, (await SwitchAsync(family.Owner)).State);
    }

    [Fact]
    public async Task AMailOutageNeverLeadsToARelease()
    {
        var family = await FamilyAsync("outage");
        await family.Owner.Emergency.SaveSettingsAsync(new EmergencySettingsDto(7, 7, 7, null, null), Ct);
        server.Mail.Fail = true;
        try
        {
            for (var day = 0; day < 90; day += 3)
            {
                await TickAsync(TimeSpan.FromDays(3));
            }

            var state = await SwitchAsync(family.Owner);
            Assert.Equal(EmergencyState.Overdue, state.State);
            Assert.Equal(1, state.Stage);
            Assert.Null(state.StageSentAt);
            Assert.Equal(ReleasedModes.None, state.ReleasedModes);
            await using var scope = server.Services.CreateAsyncScope();
            Assert.True(await scope.ServiceProvider.GetRequiredService<EmergencyEngine>().IsMailDelayedAsync(state, Ct));
        }
        finally
        {
            server.Mail.Fail = false;
        }

        // Sobald Mails wieder gehen, läuft die Kette normal weiter, ab dem tatsächlichen Versand.
        await TickAsync(TimeSpan.FromMinutes(5));
        Assert.NotNull((await SwitchAsync(family.Owner)).StageSentAt);
        Assert.Equal(ReleasedModes.None, (await SwitchAsync(family.Owner)).ReleasedModes);
    }

    [Fact]
    public async Task ARequestCanBePausedByLinkContinuedAndThenReleasesOnlyItsMode()
    {
        var family = await FamilyAsync("request", withSecondTrustee: true);
        var trustee = await family.Trustee.LoginAsync();
        var owner = (await trustee.Emergency.GetTrustedAsync(Ct)).Single();
        Assert.True(owner.CanRequestAccess);
        Assert.True(owner.HasIncapacityGrants);

        var request = await trustee.Emergency.RequestAsync(owner.OwnerId, GrantMode.Incapacity, Ct);
        Assert.Equal(RequestState.Pending, request.State);
        Assert.Equal(EmergencyErrors.Pending, (await Assert.ThrowsAsync<EmergencyException>(() => trustee.Emergency.RequestAsync(owner.OwnerId, GrantMode.Incapacity, Ct))).Message);

        await TickAsync(TimeSpan.FromMinutes(1));
        var stored = await RequestAsync(request.Id);
        Assert.NotNull(stored.DeadlineAt);
        var mail = server.Mail.To(family.Owner.Email).Last(m => (m.Subject ?? string.Empty).Contains("beantragt", StringComparison.Ordinal));
        Assert.Contains(server.Mail.To(family.SecondTrustee!.Email), m => (m.Subject ?? string.Empty).StartsWith("myLife: Antrag auf Zugang", StringComparison.Ordinal));

        // Der Link hält sofort an, ohne Anmeldung.
        var token = ObjectionLink().Match(mail.TextBody ?? string.Empty).Groups[1].Value;
        using var anonymous = server.CreateApiClient();
        var link = await new EmergencyClient(anonymous).ObjectionLinkAsync(token, Ct);
        Assert.Equal(GrantMode.Incapacity, link.Mode);
        Assert.Equal(RequestState.Paused, (await RequestAsync(request.Id)).State);

        await TickAsync(TimeSpan.FromDays(10));
        Assert.Equal(ReleasedModes.None, (await SwitchAsync(family.Owner)).ReleasedModes);

        // Nach der Anmeldung lässt der Eigentümer den Antrag weiterlaufen; die Frist verlängert sich um die Pause.
        var ownerBrowser = await family.Owner.LoginAsync();
        Assert.Equal(RequestState.Paused, (await ownerBrowser.Emergency.GetStatusAsync(Ct)).Requests.Single().State);
        await ownerBrowser.Emergency.ContinueAsync(request.Id, Ct);
        await TickAsync(TimeSpan.FromDays(6));
        Assert.Equal(ReleasedModes.None, (await SwitchAsync(family.Owner)).ReleasedModes);
        await TickAsync(TimeSpan.FromDays(1.5));

        var state = await SwitchAsync(family.Owner);
        Assert.Equal(ReleasedModes.Incapacity, state.ReleasedModes);
        Assert.Equal(RequestState.Released, (await RequestAsync(request.Id)).State);
        var grants = (await (await family.Trustee.LoginAsync()).Shared.GetOwnersAsync()).Single().Grants;
        Assert.All(grants, g => Assert.Equal(GrantMode.Incapacity, g.Dto.Mode));
        Assert.NotEmpty(grants);
    }

    [Fact]
    public async Task AnObjectionStopsTheRequestAndOnlyAllowedTrusteesMayRequest()
    {
        var family = await FamilyAsync("objection", withSecondTrustee: true);
        var trustee = await family.Trustee.LoginAsync();
        var ownerId = (await trustee.Emergency.GetTrustedAsync(Ct)).Single().OwnerId;
        var request = await trustee.Emergency.RequestAsync(ownerId, GrantMode.Death, Ct);
        await TickAsync(TimeSpan.FromMinutes(1));

        await (await family.Owner.LoginAsync()).Emergency.ObjectAsync(request.Id, Ct);
        Assert.Equal(RequestState.Objected, (await RequestAsync(request.Id)).State);
        await TickAsync(TimeSpan.FromDays(30));
        Assert.Equal(ReleasedModes.None, (await SwitchAsync(family.Owner)).ReleasedModes);
        Assert.Contains(server.Mail.To(family.Trustee.Email), m => (m.Subject ?? string.Empty).Contains("widersprochen", StringComparison.Ordinal));

        // Wer kein Antragsrecht hat, darf nicht; wer keine Vertrauensperson ist, sieht den Eigentümer nicht.
        var second = await family.SecondTrustee!.LoginAsync();
        Assert.Equal(EmergencyErrors.NotAllowed, (await Assert.ThrowsAsync<EmergencyException>(() => second.Emergency.RequestAsync(ownerId, GrantMode.Death, Ct))).Message);
        var (_, _, adminHttp) = await _account.LoginAsync();
        Assert.Equal(EmergencyErrors.NotFound, (await Assert.ThrowsAsync<EmergencyException>(() => new EmergencyClient(adminHttp).RequestAsync(ownerId, GrantMode.Death, Ct))).Message);

        // Ein neuer Antrag ist danach wieder möglich und lässt sich zurückziehen.
        var again = await (await family.Trustee.LoginAsync()).Emergency.RequestAsync(ownerId, GrantMode.Death, Ct);
        await (await family.Trustee.LoginAsync()).Emergency.WithdrawAsync(again.Id, Ct);
        Assert.Equal(RequestState.Withdrawn, (await RequestAsync(again.Id)).State);
    }

    [Fact]
    public async Task PeopleWithoutTrusteesGetNoReminders()
    {
        var secret = await VaultIsolationTests.CreateUserAsync(server, "allein@example.org");
        await VaultBrowser.OpenAsync(_account, "allein@example.org", () => FamilyTests.Code(server, secret));
        for (var i = 0; i < 5; i++)
        {
            await TickAsync(TimeSpan.FromDays(40));
        }

        Assert.Empty(server.Mail.To("allein@example.org"));
    }

    // ---------- Hilfen ----------

    private sealed record Person(ServerFixture Server, AuthTestSupport Account, string Email, string Name, string Secret)
    {
        public async Task<PersonBrowser> LoginAsync()
        {
            var vault = await VaultBrowser.OpenAsync(Account, Email, () => FamilyTests.Code(Server, Secret));
            var family = new FamilyClient(vault.Http);
            var keys = new KeyTrustService(family, vault.Crypto, vault.Session);
            var trust = new TrustClient(vault.Http);
            return new PersonBrowser(vault, keys, new TrustService(trust, family, keys, vault.Vault, vault.Crypto, vault.Session),
                new SharedService(trust, keys, vault.Vault, vault.Crypto, vault.Session), new EmergencyClient(vault.Http));
        }
    }

    private sealed record PersonBrowser(VaultBrowser Vault, KeyTrustService Keys, TrustService Trust, SharedService Shared, EmergencyClient Emergency);

    private sealed record Family(PersonBrowser Owner0, Person OwnerPerson, Person Trustee, Person? SecondTrustee)
    {
        public OwnerHandle Owner => new(OwnerPerson, Owner0);
    }

    private sealed record OwnerHandle(Person Person, PersonBrowser Browser)
    {
        public string Email => Person.Email;

        public string Name => Person.Name;

        public EmergencyClient Emergency => Browser.Emergency;

        public Guid Id => Browser.Vault.Session.Session!.UserId;

        public Task<PersonBrowser> LoginAsync() => Person.LoginAsync();
    }

    /// <summary>
    /// Eigentümer (eingeladen vom Admin), eine Vertrauensperson mit Antragsrecht und Freigaben für beide Modi auf
    /// Bereich 4, optional eine zweite ohne Antragsrecht. Alle Schlüssel sind gegenseitig bestätigt.
    /// </summary>
    private async Task<Family> FamilyAsync(string name, bool withSecondTrustee = false)
    {
        var adminBrowser = await VaultBrowser.OpenAsync(_account);
        var admin = new KeyTrustService(new FamilyClient(adminBrowser.Http), adminBrowser.Crypto, adminBrowser.Session);
        var ownerPerson = await InviteAsync(admin, $"{name}.eigentuemer@example.org", $"Eigentümer {name}");
        var owner = await ownerPerson.LoginAsync();
        var trustee = await InviteAsync(owner.Keys, $"{name}.vertrauen@example.org", $"Vertrauen {name}");
        var second = withSecondTrustee ? await InviteAsync(owner.Keys, $"{name}.zweite@example.org", $"Zweite {name}") : null;
        await owner.Keys.SyncAsync();

        await owner.Vault.Vault.CreateAsync("insurance", new VaultPayload { Title = "Lebensversicherung", Fields = { ["insuranceKind"] = "life", ["insurer"] = "Allianz" } });
        var people = await owner.Keys.GetPeopleAsync();
        await owner.Trust.SaveAsync(people.Single(p => p.Person.Email == trustee.Email), new TrusteeProfile("Kind", null), true,
            new HashSet<(int, GrantMode)> { (4, GrantMode.Death), (4, GrantMode.Incapacity) }, new HashSet<(Guid, GrantMode)>(), null);
        if (second is not null)
        {
            await owner.Trust.SaveAsync(people.Single(p => p.Person.Email == second.Email), new TrusteeProfile("Freund", null), false,
                new HashSet<(int, GrantMode)> { (4, GrantMode.Death) }, new HashSet<(Guid, GrantMode)>(), null);
        }

        // Die Vertrauenspersonen bestätigen den Eigentümer (gepinnt aus dem Link) mit der ersten Anmeldung.
        await (await trustee.LoginAsync()).Keys.SyncAsync();
        return new Family(owner, ownerPerson, trustee, second);
    }

    private async Task<Person> InviteAsync(KeyTrustService inviter, string email, string name)
    {
        var link = await inviter.InviteAsync(email, "de", sendMail: false, "https://localhost/");
        var secret = await FamilyTests.AcceptInvitationAsync(server, link.Link, name);
        await inviter.SyncAsync();
        return new Person(server, _account, email, name, secret);
    }

    /// <summary>Uhr vorstellen, dann Notfalldienst, Mailversand und noch einmal den Notfalldienst laufen lassen.</summary>
    private async Task TickAsync(TimeSpan advance)
    {
        server.Clock.Advance(advance);
        var emergency = server.Services.GetRequiredService<EmergencySwitchService>();
        var mail = server.Services.GetRequiredService<MailOutboxService>();
        await emergency.ProcessOnceAsync(Ct);
        await mail.ProcessOnceAsync(Ct);
        await emergency.ProcessOnceAsync(Ct);
        await mail.ProcessOnceAsync(Ct);
    }

    private async Task<EmergencySwitch> SwitchAsync(OwnerHandle owner)
    {
        await using var scope = server.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<MyLifeDbContext>().EmergencySwitches.AsNoTracking().SingleAsync(s => s.OwnerId == owner.Id, Ct);
    }

    private async Task<AccessRequest> RequestAsync(Guid id)
    {
        await using var scope = server.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<MyLifeDbContext>().AccessRequests.AsNoTracking().SingleAsync(r => r.Id == id, Ct);
    }

    [GeneratedRegex(@"/lifesign#([A-Za-z0-9_\-]+)")]
    private static partial Regex LifesignLink();

    [GeneratedRegex(@"/objection#([A-Za-z0-9_\-]+)")]
    private static partial Regex ObjectionLink();
}
