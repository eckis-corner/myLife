using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyLife.Client.Services;
using MyLife.Crypto;
using MyLife.Server.Data;
using MyLife.Server.Infrastructure;
using MyLife.Server.Infrastructure.Mail;
using MyLife.Shared.Admin;
using MyLife.Shared.Family;
using MyLife.Shared.Vault;
using static MyLife.Tests.Server.TestToken;

namespace MyLife.Tests.Server;

/// <summary>Einladen und Schlüsselprüfung (M2.2, Architektur 4.3), mit der Browser-Logik gegen den echten Server.</summary>
public sealed partial class FamilyTests(ServerFixture server) : IClassFixture<ServerFixture>, IAsyncLifetime
{
    private AuthTestSupport _account = null!;

    public async ValueTask InitializeAsync() => _account = await AuthTestSupport.ForAsync(server);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task InvitedPersonCreatesAnAccountAndBothSidesConfirmEachOther()
    {
        var inviter = await BrowserAsync();
        var link = await inviter.Trust.InviteAsync("anna@example.org", "de", sendMail: false, "https://localhost/");
        Assert.False(link.MailQueued);
        Assert.Matches(@"^https://localhost/invite#t=[A-Za-z0-9_-]{43}&s=[A-Za-z0-9_-]{43}&f=[0-9a-f]{64}$", link.Link);
        Assert.Equal(InvitationStatus.Pending, (await inviter.Family.GetInvitationsAsync(Ct)).Single(i => i.Id == link.Id).Status);

        var anna = await AcceptAsync(link.Link, "Anna");
        var invitations = await inviter.Family.GetInvitationsAsync(Ct);
        Assert.Equal(InvitationStatus.Accepted, invitations.Single(i => i.Id == link.Id).Status);
        Assert.Equal("Anna", invitations.Single(i => i.Id == link.Id).InviteeName);

        // Nach der Anmeldung prüft der Browser der einladenden Person die Bindung, gegenzeichnet und pinnt.
        Assert.True(await inviter.Trust.SyncAsync() >= 1);
        Assert.Equal(InvitationStatus.Verified, (await inviter.Family.GetInvitationsAsync(Ct)).Single(i => i.Id == link.Id).Status);
        var seenByInviter = (await inviter.Trust.GetPeopleAsync()).Single(p => p.Person.Email == "anna@example.org");
        Assert.Equal(TrustState.Pinned, seenByInviter.State);

        // Anna hat den Fingerprint der einladenden Person aus dem Link gepinnt und gegenzeichnet sie beim ersten Mal.
        var annaBrowser = await LoginAsync("anna@example.org", anna);
        Assert.Equal(TrustState.Pinned, (await annaBrowser.Trust.GetPeopleAsync()).Single(p => p.Person.Email == AuthTestSupport.Email).State);
        Assert.Equal(1, await annaBrowser.Trust.SyncAsync());
        Assert.Equal(0, await annaBrowser.Trust.SyncAsync());
        var annaId = annaBrowser.Session.Session!.UserId;
        Assert.Contains((await annaBrowser.Family.GetPeopleAsync(Ct)).Single(p => p.Email == AuthTestSupport.Email).Endorsements, e => e.SignerId == annaId);
        Assert.Single((await inviter.Family.GetPeopleAsync(Ct)).Single(p => p.Id == annaId).Endorsements);

        var audit = (await annaBrowser.Vault.Api.GetAuditAsync(0, 50, Ct)).Items;
        Assert.Contains(audit, e => e.Action == AuditActions.KeyEndorsed);
        Assert.Contains((await inviter.Vault.Api.GetAuditAsync(0, 50, Ct)).Items, e => e.Action == AuditActions.InvitationAccepted);
        await server.Services.GetRequiredService<MailOutboxService>().ProcessOnceAsync(Ct);
    }

    [Fact]
    public async Task InvitationMailCarriesTheSecretOnlyInTheFragment()
    {
        var inviter = await BrowserAsync();
        var admin = new AdminClient(inviter.Http);
        var settings = await admin.GetSettingsAsync(Ct);
        await admin.SaveSettingsAsync(settings with { Smtp = new SmtpSettingsDto() }, Ct);
        var error = await Assert.ThrowsAsync<FamilyException>(() => inviter.Trust.InviteAsync("bernd@example.org", "en", sendMail: true, "https://localhost/"));
        Assert.Equal(FamilyErrors.MailNotConfigured, error.Message);

        await admin.SaveSettingsAsync(settings with
        {
            PublicUrl = "https://mylife.example.org",
            Smtp = new SmtpSettingsDto { Host = "smtp.example.org", FromAddress = "familie@example.org" },
        }, Ct);
        var link = await inviter.Trust.InviteAsync("bernd@example.org", "en", sendMail: true, "https://localhost/");
        Assert.True(link.MailQueued);
        await server.Services.GetRequiredService<MailOutboxService>().ProcessOnceAsync(Ct);

        var mail = server.Mail.To("bernd@example.org").Last();
        Assert.Equal("Daniel invites you to myLife", mail.Subject);
        var mailed = MailLink().Match(mail.TextBody ?? string.Empty).Value;
        Assert.StartsWith("https://mylife.example.org/invite#t=", mailed, StringComparison.Ordinal);
        // Geheimnis und Fingerprint entsprechen dem Link, den der Browser gebaut hat; nur das Token ist eigen.
        Assert.Equal(link.Link[link.Link.IndexOf("&s=", StringComparison.Ordinal)..], mailed[mailed.IndexOf("&s=", StringComparison.Ordinal)..]);

        // Die Parameter (mit dem Geheimnis) sind nach dem Versand aus der Outbox verschwunden.
        await using (var scope = server.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MyLifeDbContext>();
            Assert.True(await db.MailOutbox.Where(m => m.Recipient == "bernd@example.org").AllAsync(m => m.Parameters == null, Ct));
            var stored = await db.Invitations.SingleAsync(i => i.Id == link.Id, Ct);
            var secret = KeyTrustService.FromBase64Url(Fragment(mailed)["s"])!;
            Assert.True(stored.EncryptedSecret.AsSpan().IndexOf(secret) < 0);
        }

        await AcceptAsync(mailed, "Bernd");
        Assert.True(await inviter.Trust.SyncAsync() >= 1);
        Assert.Equal(InvitationStatus.Verified, (await inviter.Family.GetInvitationsAsync(Ct)).Single(i => i.Id == link.Id).Status);
    }

    [Fact]
    public async Task ABindingThatDoesNotMatchIsReportedAndNotConfirmed()
    {
        var inviter = await BrowserAsync();
        var link = await inviter.Trust.InviteAsync("carla@example.org", "de", sendMail: false, "https://localhost/");
        await AcceptAsync(link.Link, "Carla");

        // Ein manipulierter Server tauscht die Bindung (oder den Schlüssel) aus.
        await using (var scope = server.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MyLifeDbContext>();
            var invitation = await db.Invitations.SingleAsync(i => i.Id == link.Id, Ct);
            invitation.Binding = new byte[32];
            await db.SaveChangesAsync(Ct);
        }

        Assert.True(await inviter.Trust.SyncAsync() >= 1);
        Assert.Equal(InvitationStatus.Mismatch, (await inviter.Family.GetInvitationsAsync(Ct)).Single(i => i.Id == link.Id).Status);
        Assert.Equal(TrustState.Unconfirmed, (await inviter.Trust.GetPeopleAsync()).Single(p => p.Person.Email == "carla@example.org").State);
        Assert.Contains((await new AdminClient(inviter.Http).GetAuditAsync(0, 100, Ct)).Items, e => e.Action == AuditActions.KeyMismatch);
    }

    [Fact]
    public async Task AKeySwappedByTheServerShowsAsChangedAndForeignEndorsementsAreRejected()
    {
        var inviter = await BrowserAsync();
        var link = await inviter.Trust.InviteAsync("dora@example.org", "de", sendMail: false, "https://localhost/");
        await AcceptAsync(link.Link, "Dora");
        await inviter.Trust.SyncAsync();
        var dora = (await inviter.Family.GetPeopleAsync(Ct)).Single(p => p.Email == "dora@example.org");

        // Eine Gegenzeichnung über einen falschen Fingerprint nimmt der Server nicht an.
        var signature = await inviter.Crypto.SignAsync(inviter.Session.SigningKey!.Value, new byte[32], KeyContexts.Endorsement(dora.Id));
        Assert.Equal(FamilyErrors.Fingerprint, (await Assert.ThrowsAsync<FamilyException>(() => inviter.Family.EndorseAsync(dora.Id, signature, Ct))).Message);

        await using (var scope = server.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MyLifeDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == dora.Id, Ct);
            user.PublicEncryptionKey = (await db.Users.SingleAsync(u => u.Email == AuthTestSupport.Email, Ct)).PublicEncryptionKey;
            await db.SaveChangesAsync(Ct);
        }

        Assert.Equal(TrustState.Changed, (await inviter.Trust.GetPeopleAsync()).Single(p => p.Person.Id == dora.Id).State);

        // Einen geänderten Schlüssel gegenzeichnet der Browser nicht.
        await inviter.Trust.SyncAsync();
        var swapped = (await inviter.Family.GetPeopleAsync(Ct)).Single(p => p.Id == dora.Id);
        var swappedFingerprint = System.Security.Cryptography.SHA256.HashData(KeyBinding.FingerprintData(swapped.PublicEncryptionKey, swapped.PublicSigningKey));
        Assert.DoesNotContain(swapped.Endorsements, e => e.Fingerprint.AsSpan().SequenceEqual(swappedFingerprint));
    }

    [Fact]
    public async Task OthersAreConfirmedThroughEndorsementsOfPeopleYouTrust()
    {
        var inviter = await BrowserAsync();
        var emil = await AcceptAsync((await inviter.Trust.InviteAsync("emil@example.org", "de", false, "https://localhost/")).Link, "Emil");
        await AcceptAsync((await inviter.Trust.InviteAsync("frieda@example.org", "de", false, "https://localhost/")).Link, "Frieda");
        Assert.True(await inviter.Trust.SyncAsync() >= 2);

        // Emil hat nur die einladende Person gepinnt; Frieda kennt er über deren Gegenzeichnung.
        var emilBrowser = await LoginAsync("emil@example.org", emil);
        var seen = await emilBrowser.Trust.GetPeopleAsync();
        var frieda = seen.Single(p => p.Person.Email == "frieda@example.org");
        Assert.Equal(TrustState.Endorsed, frieda.State);
        Assert.Equal("Daniel", frieda.ConfirmedBy);
    }

    [Fact]
    public async Task LinksWorkOnlyOnceAndOnlyForTheInvitedAddress()
    {
        var inviter = await BrowserAsync();
        var link = await inviter.Trust.InviteAsync("gert@example.org", "de", false, "https://localhost/");
        using var anonymous = server.CreateApiClient();
        var family = new FamilyClient(anonymous);
        var token = Fragment(link.Link)["t"];

        Assert.Equal("gert@example.org", (await family.GetInviteInfoAsync(token, Ct)).Email);
        Assert.Equal(FamilyErrors.TokenInvalid, (await Assert.ThrowsAsync<FamilyException>(() => family.GetInviteInfoAsync("x" + token[1..], Ct))).Message);

        // Eine andere Adresse lässt sich mit diesem Link nicht anlegen.
        await Assert.ThrowsAsync<FamilyException>(() => AcceptAsync(link.Link, "Gert", email: "fremd@example.org"));
        await AcceptAsync(link.Link, "Gert");
        Assert.Equal(FamilyErrors.TokenInvalid, (await Assert.ThrowsAsync<FamilyException>(() => family.GetInviteInfoAsync(token, Ct))).Message);

        Assert.Equal(FamilyErrors.AccountExists,
            (await Assert.ThrowsAsync<FamilyException>(() => inviter.Trust.InviteAsync("gert@example.org", "de", false, "https://localhost/"))).Message);

        // Ein neuer Link macht den alten ungültig; eine zurückgezogene Einladung gilt nicht mehr.
        var second = await inviter.Trust.InviteAsync("hanna@example.org", "de", false, "https://localhost/");
        var renewed = await inviter.Trust.RenewAsync((await inviter.Family.GetInvitationsAsync(Ct)).Single(i => i.Id == second.Id), false, "https://localhost/");
        Assert.Equal(FamilyErrors.TokenInvalid, (await Assert.ThrowsAsync<FamilyException>(() => family.GetInviteInfoAsync(Fragment(second.Link)["t"], Ct))).Message);
        Assert.Equal(Fragment(second.Link)["s"], Fragment(renewed.Link)["s"]);
        await inviter.Family.RevokeInvitationAsync(second.Id, Ct);
        Assert.Equal(FamilyErrors.TokenInvalid, (await Assert.ThrowsAsync<FamilyException>(() => family.GetInviteInfoAsync(Fragment(renewed.Link)["t"], Ct))).Message);
    }

    [Fact]
    public async Task AdministratorsSeeAndRevokeOpenInvitations()
    {
        var inviter = await BrowserAsync();
        var link = await inviter.Trust.InviteAsync("ida@example.org", "de", false, "https://localhost/");
        var open = await inviter.Family.GetOpenInvitationsAsAdminAsync(Ct);
        Assert.Contains(open, i => i.Id == link.Id && i.InviterName == "Daniel");

        await inviter.Family.RevokeInvitationAsAdminAsync(link.Id, Ct);
        Assert.DoesNotContain(await inviter.Family.GetOpenInvitationsAsAdminAsync(Ct), i => i.Id == link.Id);

        // Wer kein Admin ist, sieht die Einladungen anderer nicht.
        var other = await AcceptAsync((await inviter.Trust.InviteAsync("jan@example.org", "de", false, "https://localhost/")).Link, "Jan");
        var jan = await LoginAsync("jan@example.org", other);
        await Assert.ThrowsAsync<FamilyException>(() => jan.Family.GetOpenInvitationsAsAdminAsync(Ct));
        Assert.Empty(await jan.Family.GetInvitationsAsync(Ct));
    }

    private sealed record Browser(VaultBrowser Vault, FamilyClient Family, KeyTrustService Trust)
    {
        public HttpClient Http => Vault.Http;

        public ReferenceMyLifeCrypto Crypto => Vault.Crypto;

        public SessionService Session => Vault.Session;
    }

    private async Task<Browser> BrowserAsync() => Wrap(await VaultBrowser.OpenAsync(_account));

    private async Task<Browser> LoginAsync(string email, string totpSecret) => Wrap(await VaultBrowser.OpenAsync(_account, email, () => Code(totpSecret)));

    private static Browser Wrap(VaultBrowser vault)
    {
        var family = new FamilyClient(vault.Http);
        return new Browser(vault, family, new KeyTrustService(family, vault.Crypto, vault.Session));
    }

    /// <summary>Wie der Browser der eingeladenen Person: Link lesen, TOTP einrichten, Tresor erzeugen, binden, annehmen.</summary>
    private async Task<string> AcceptAsync(string link, string name, string? email = null)
    {
        var fragment = Fragment(link);
        using var http = server.CreateApiClient();
        await using var crypto = new ReferenceMyLifeCrypto();
        var family = new FamilyClient(http);
        var info = await family.GetInviteInfoAsync(fragment["t"], Ct);
        var enrollment = await family.CreateInviteTotpAsync(fragment["t"], Ct);
        var confirmation = await family.VerifyInviteTotpAsync(fragment["t"], enrollment.Enrollment, Code(enrollment.Secret), Ct);
        var setup = new VaultSetupService(crypto);
        var recoveryKey = await setup.CreateRecoveryKeyAsync();
        byte[]? pin = null;
        var request = await setup.CreateVaultAsync(new SetupAccount(name, email ?? info.Email, "de"), SetupTestSupport.Password, recoveryKey, fragment["t"], confirmation,
            async vaultKey => pin = await crypto.EncryptAsync(vaultKey, Convert.FromHexString(fragment["f"]), KeyContexts.Pin(info.InviterId)));
        var fingerprint = await crypto.Sha256Async(KeyBinding.FingerprintData(request.PublicEncryptionKey, request.PublicSigningKey));
        var binding = await crypto.HmacSha256Async(KeyTrustService.FromBase64Url(fragment["s"])!, KeyBinding.InviteBindingData(info.Id, fingerprint));
        await family.AcceptAsync(new AcceptInvitationRequest(request, binding, pin), Ct);
        return enrollment.Secret;
    }

    private static Dictionary<string, string> Fragment(string link) =>
        link[(link.IndexOf('#', StringComparison.Ordinal) + 1)..].Split('&').Select(p => p.Split('=', 2)).ToDictionary(p => p[0], p => p[1]);

    private string Code(string secret)
    {
        server.Clock.Advance(TimeSpan.FromSeconds(Totp.PeriodSeconds));
        return Totp.Compute(Base32.Decode(secret), Totp.StepAt(server.Clock.GetUtcNow()));
    }

    [GeneratedRegex(@"https://\S+/invite#\S+")]
    private static partial Regex MailLink();
}
