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

        // Eine Anmeldung danach ist ein Lebenszeichen, hebt die Freigabe aber nicht auf; dafür gibt es die Rücknahme.
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

    [Fact]
    public async Task TheOwnerRevokesAReleaseAndAreaAndLetterGetNewKeys()
    {
        var family = await FamilyAsync("revoke");
        var letter = await family.Owner.Browser.Vault.Vault.CreateAsync("letter", new VaultPayload
        {
            Title = "Für dich", Fields = { ["recipients"] = "Kind", ["letterText"] = "Alles Gute." },
        });
        var people = await family.Owner.Browser.Keys.GetPeopleAsync();
        await family.Owner.Browser.Trust.SaveAsync(people.Single(p => p.Person.Email == family.Trustee.Email), new TrusteeProfile("Kind", null), true,
            new HashSet<(int, GrantMode)> { (4, GrantMode.Death), (4, GrantMode.Incapacity) }, new HashSet<(Guid, GrantMode)> { (letter.Id, GrantMode.Incapacity) }, null);
        await ReleaseAsync(family.Owner, ReleasedModes.Incapacity);
        var before = await GrantsAsync(family.Owner);

        // Die Vertrauensperson liest Bereich und Brief.
        var trustee = await family.Trustee.LoginAsync();
        var shared = (await trustee.Shared.GetOwnersAsync()).Single();
        Assert.Equal(2, shared.Grants.Count);
        Assert.Contains((await trustee.Shared.OpenAsync(shared.OwnerId, shared.Grants.Single(g => g.Dto.Kind == GrantKind.Letter).Dto)).Items,
            i => i.Payload.Get("letterText") == "Alles Gute.");

        // Der Eigentümer meldet sich zurück und nimmt die Freigabe zurück: Der Server liefert sofort nichts mehr aus.
        var owner = await family.Owner.LoginAsync();
        var pending = await owner.Emergency.RevokeAsync(Ct);
        Assert.Equal([4], pending.Areas);
        Assert.Equal([letter.Id], pending.Letters);
        Assert.Empty(await (await family.Trustee.LoginAsync()).Shared.GetOwnersAsync());
        var state = await SwitchAsync(family.Owner);
        Assert.Equal(ReleasedModes.None, state.ReleasedModes);
        Assert.Equal(EmergencyState.Active, state.State);
        Assert.Equal(EmergencyErrors.NotReleased, (await Assert.ThrowsAsync<EmergencyException>(() => owner.Emergency.RevokeAsync(Ct))).Message);
        await TickAsync(TimeSpan.FromMinutes(1));
        Assert.Contains(server.Mail.To(family.Trustee.Email), m => m.Subject == $"myLife: {family.Owner.Name} hat den Notfallzugang zurückgenommen");

        // Bis der Browser fertig ist, merkt sich der Server die ausstehenden Schlüsselwechsel.
        Assert.False((await owner.Emergency.GetStatusAsync(Ct)).Rekey.IsEmpty);
        await owner.Trust.CompleteRekeyAsync(pending);
        Assert.True((await owner.Emergency.GetStatusAsync(Ct)).Rekey.IsEmpty);

        // Alle Umschläge sind neu versiegelt; der Eigentümer liest weiter alles.
        var after = await GrantsAsync(family.Owner);
        Assert.Equal(before.Keys.Order(), after.Keys.Order());
        Assert.All(before, g => Assert.False(g.Value.AsSpan().SequenceEqual(after[g.Key])));
        var reopened = await family.Owner.LoginAsync();
        await reopened.Vault.Vault.EnsureLoadedAsync();
        Assert.Equal("Alles Gute.", reopened.Vault.Vault.Find(letter.Id)!.Payload.Get("letterText"));
        Assert.Contains(reopened.Vault.Vault.Items, i => i.Title == "Lebensversicherung");

        // Wird später wieder freigegeben, öffnen die neuen Umschläge die neuen Schlüssel.
        await ReleaseAsync(family.Owner, ReleasedModes.Incapacity);
        trustee = await family.Trustee.LoginAsync();
        shared = (await trustee.Shared.GetOwnersAsync()).Single();
        Assert.All(shared.Grants, g => Assert.True(g.Verified));
        Assert.Contains((await trustee.Shared.OpenAsync(shared.OwnerId, shared.Grants.Single(g => g.Dto.Kind == GrantKind.Area).Dto)).Items,
            i => i.Title == "Lebensversicherung");
        Assert.Contains((await trustee.Shared.OpenAsync(shared.OwnerId, shared.Grants.Single(g => g.Dto.Kind == GrantKind.Letter).Dto)).Items,
            i => i.Payload.Get("letterText") == "Alles Gute.");
    }

    [Fact]
    public async Task AfterDeathAllTrusteesMustAgreeAndTheOwnerCanStillStopIt()
    {
        var family = await FamilyAsync("deletion", withSecondTrustee: true);
        var trustee = await family.Trustee.LoginAsync();
        var second = await family.SecondTrustee!.LoginAsync();
        var ownerId = family.Owner.Id;

        // Vor der Freigabe „verstorben“ gibt es nichts zu löschen; wer keine Vertrauensperson ist, sieht nichts.
        Assert.Null((await trustee.Emergency.GetTrustedAsync(Ct)).Single().Deletion);
        Assert.Equal(EmergencyErrors.NotReleased, (await Assert.ThrowsAsync<EmergencyException>(() => trustee.Emergency.AgreeDeletionAsync(ownerId, Ct))).Message);
        await ReleaseAsync(family.Owner, ReleasedModes.Death);
        var (_, _, adminHttp) = await _account.LoginAsync();
        Assert.Equal(EmergencyErrors.NotFound, (await Assert.ThrowsAsync<EmergencyException>(() => new EmergencyClient(adminHttp).AgreeDeletionAsync(ownerId, Ct))).Message);

        // Die erste Zustimmung schlägt das Löschen den anderen vor.
        var deletion = await trustee.Emergency.AgreeDeletionAsync(ownerId, Ct);
        Assert.Equal([family.Trustee.Name], deletion.Agreed);
        Assert.Equal([family.SecondTrustee.Name], deletion.Missing);
        Assert.Null(deletion.ScheduledAt);
        await TickAsync(TimeSpan.FromMinutes(1));
        Assert.Contains(server.Mail.To(family.SecondTrustee.Email), m => m.Subject == $"myLife: Tresor von {family.Owner.Name} löschen?");

        // Mit der letzten Zustimmung ist das Löschen in sieben Tagen geplant; der Eigentümer bekommt Bescheid.
        deletion = await second.Emergency.AgreeDeletionAsync(ownerId, Ct);
        Assert.True(deletion.AgreedByMe);
        Assert.NotNull(deletion.ScheduledAt);
        await TickAsync(TimeSpan.FromMinutes(1));
        Assert.Contains(server.Mail.To(family.Owner.Email), m => (m.Subject ?? string.Empty).StartsWith("myLife: Dein Tresor wird am", StringComparison.Ordinal));
        Assert.NotNull((await (await family.Owner.LoginAsync()).Emergency.GetStatusAsync(Ct)).Deletion?.ScheduledAt);

        // Wer zurückzieht, stoppt es; die anderen erfahren es.
        deletion = await second.Emergency.WithdrawDeletionAsync(ownerId, Ct);
        Assert.Null(deletion.ScheduledAt);
        await TickAsync(TimeSpan.FromMinutes(1));
        Assert.Contains(server.Mail.To(family.Trustee.Email), m => (m.Subject ?? string.Empty).StartsWith("myLife: Löschen des Tresors", StringComparison.Ordinal));

        // Lebt der Eigentümer doch, stoppt seine Rücknahme alles und verwirft die Zustimmungen.
        Assert.NotNull((await second.Emergency.AgreeDeletionAsync(ownerId, Ct)).ScheduledAt);
        var owner = await family.Owner.LoginAsync();
        await owner.Trust.CompleteRekeyAsync(await owner.Emergency.RevokeAsync(Ct));
        Assert.Null((await SwitchAsync(family.Owner)).DeletionScheduledAt);
        await TickAsync(TimeSpan.FromDays(8));
        Assert.True(await UserExistsAsync(ownerId));

        // Nach einer neuen Freigabe stimmen beide zu; nach sieben Tagen ist alles gelöscht.
        await ReleaseAsync(family.Owner, ReleasedModes.Death);
        trustee = await family.Trustee.LoginAsync();
        second = await family.SecondTrustee.LoginAsync();
        Assert.Empty((await trustee.Emergency.GetTrustedAsync(Ct)).Single().Deletion!.Agreed);
        await trustee.Emergency.AgreeDeletionAsync(ownerId, Ct);
        await second.Emergency.AgreeDeletionAsync(ownerId, Ct);
        await TickAsync(TimeSpan.FromDays(6));
        Assert.True(await UserExistsAsync(ownerId));
        await TickAsync(TimeSpan.FromDays(1.1));
        Assert.False(await UserExistsAsync(ownerId));
        Assert.Contains(server.Mail.To(family.Trustee.Email), m => m.Subject == $"myLife: Tresor von {family.Owner.Name} gelöscht");
        Assert.Empty(await (await family.Trustee.LoginAsync()).Emergency.GetTrustedAsync(Ct));
        await using var scope = server.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MyLifeDbContext>();
        Assert.True(await db.AuditLog.AnyAsync(a => a.Action == AuditActions.VaultDeletedByTrustees && a.TargetId == ownerId, Ct));
        Assert.False(await db.Objects.AnyAsync(o => o.OwnerId == ownerId, Ct));
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

    /// <summary>Freigabe direkt setzen; der Weg dorthin ist oben geprüft.</summary>
    private async Task ReleaseAsync(OwnerHandle owner, ReleasedModes modes)
    {
        await using var scope = server.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MyLifeDbContext>();
        var state = await db.EmergencySwitches.SingleAsync(s => s.OwnerId == owner.Id, Ct);
        state.ReleasedModes = modes;
        state.ReleasedAt = server.Clock.GetUtcNow();
        await db.SaveChangesAsync(Ct);
    }

    private async Task<Dictionary<Guid, byte[]>> GrantsAsync(OwnerHandle owner)
    {
        await using var scope = server.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<MyLifeDbContext>().Grants.AsNoTracking()
            .Where(g => g.OwnerId == owner.Id).ToDictionaryAsync(g => g.Id, g => g.Envelope, Ct);
    }

    private async Task<bool> UserExistsAsync(Guid id)
    {
        await using var scope = server.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<MyLifeDbContext>().Users.AnyAsync(u => u.Id == id, Ct);
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
