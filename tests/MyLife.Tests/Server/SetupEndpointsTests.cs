using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyLife.Client.Services;
using MyLife.Crypto;
using MyLife.Server.Api;
using MyLife.Server.Data;
using MyLife.Server.Infrastructure;
using MyLife.Shared.Setup;

namespace MyLife.Tests.Server;

/// <summary>Gemeinsame Bausteine für die Tests des Einrichtungsassistenten.</summary>
internal static class SetupTestSupport
{
    public const string Password = "Mein sehr langes Master-Passwort für Tests";

    public static string Token(ServerFixture server) =>
        server.Services.GetRequiredService<SetupTokenService>().CurrentToken
        ?? throw new InvalidOperationException("Kein Einrichtungscode ausgestellt.");

    public static string CurrentCode(string base32Secret) =>
        Totp.Compute(Base32.Decode(base32Secret), Totp.StepAt(DateTimeOffset.UtcNow));

    /// <summary>Spielt den Assistenten bis zur fertigen Anfrage durch, mit der .NET-Referenz anstelle des Browsers.</summary>
    public static async Task<(SetupCompleteRequest Request, byte[] RecoveryKey, string TotpSecret)> PrepareAsync(
        HttpClient client, string token, ReferenceMyLifeCrypto crypto, CancellationToken cancellationToken)
    {
        var api = new SetupClient(client);
        var enrollment = await api.CreateTotpAsync(token, cancellationToken);
        var confirmation = await api.VerifyTotpAsync(token, enrollment.Enrollment, CurrentCode(enrollment.Secret), cancellationToken);
        var vault = new VaultSetupService(crypto);
        var recoveryKey = await vault.CreateRecoveryKeyAsync();
        var request = await vault.CreateVaultAsync(new SetupAccount("Daniel", " Daniel@Example.org ", "de"), Password, recoveryKey, token, confirmation);
        return (request, recoveryKey, enrollment.Secret);
    }
}

/// <summary>Der vollständige Ablauf auf einer frischen Datenbank, vom Einrichtungscode bis zum gespeicherten Administrator.</summary>
public sealed class SetupFlowTests(ServerFixture server) : IClassFixture<ServerFixture>
{
    [Fact]
    public async Task FirstUserBecomesAdminAndServerOnlyStoresCiphertext()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = server.CreateClient();
        var api = new SetupClient(client);
        var token = SetupTestSupport.Token(server);

        Assert.True(await api.IsRequiredAsync(cancellationToken));
        var wrongToken = await Assert.ThrowsAsync<SetupException>(() => api.VerifyTokenAsync("FALSCH-CODE", cancellationToken));
        Assert.Equal("setup-invalid-token", wrongToken.Message);
        await api.VerifyTokenAsync(token.ToLowerInvariant().Replace("-", " ", StringComparison.Ordinal), cancellationToken);

        var enrollment = await api.CreateTotpAsync(token, cancellationToken);
        var wrongCode = await Assert.ThrowsAsync<SetupException>(() => api.VerifyTotpAsync(token, enrollment.Enrollment, "000000", cancellationToken));
        Assert.Equal("setup-totp-invalid-code", wrongCode.Message);

        await using var crypto = new ReferenceMyLifeCrypto();
        var (request, recoveryKey, totpSecret) = await SetupTestSupport.PrepareAsync(client, token, crypto, cancellationToken);
        await api.CompleteAsync(request, cancellationToken);

        Assert.False(await api.IsRequiredAsync(cancellationToken));
        var again = await Assert.ThrowsAsync<SetupException>(() => api.CompleteAsync(request, cancellationToken));
        Assert.Equal("setup-already-completed", again.Message);
        Assert.Null(server.Services.GetRequiredService<SetupTokenService>().CurrentToken);

        await using var scope = server.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MyLifeDbContext>();
        var user = await db.Users.SingleAsync(cancellationToken);
        Assert.True(user.IsAdmin);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Equal("daniel@example.org", user.Email);
        Assert.Equal("de", user.Language);
        Assert.True(await db.SystemSettings.AnyAsync(s => s.Key == SetupEndpoints.CompletedSettingKey, cancellationToken));

        // Anmeldehash nur als eigener Argon2id-Hash, TOTP-Geheimnis nur mit Data Protection verschlüsselt.
        Assert.StartsWith("$argon2id$", user.AuthHash, StringComparison.Ordinal);
        Assert.True(await AuthHashHasher.VerifyAsync(user.AuthHash, request.AuthHash));
        Assert.DoesNotContain(totpSecret, user.TotpSecret!, StringComparison.Ordinal);
        var protector = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>().CreateProtector(SetupEndpoints.TotpSecretPurpose);
        Assert.Equal(totpSecret, protector.Unprotect(user.TotpSecret!));

        // Was in der Datenbank liegt, öffnet sich nur mit Passwort oder Recovery-Key.
        var master = await crypto.DeriveMasterKeyAsync(SetupTestSupport.Password, user.KdfSalt,
            new Argon2Parameters(user.KdfMemoryKiB, user.KdfIterations, user.KdfParallelism));
        var kek = await crypto.DeriveKeyEncryptionKeyAsync(master);
        var vmk = await crypto.UnwrapKeyAsync(kek, user.WrappedVmk, KeyContexts.Vmk);
        var recovery = await crypto.ImportRecoveryKeyAsync(recoveryKey);
        var vmkByRecovery = await crypto.UnwrapKeyAsync(recovery, user.WrappedVmkByRecovery, KeyContexts.Vmk);
        var signing = await crypto.UnwrapPrivateKeyAsync(KeyPairKind.Ecdsa, vmkByRecovery, user.WrappedSigningPrivateKey, KeyContexts.SigningPrivateKey);
        var signature = await crypto.SignAsync(signing, [1, 2, 3], "test");
        Assert.True(await crypto.VerifyAsync(user.PublicSigningKey, [1, 2, 3], signature, "test"));
        await crypto.UnwrapPrivateKeyAsync(KeyPairKind.Ecdh, vmk, user.WrappedEncryptionPrivateKey, KeyContexts.EncryptionPrivateKey);
    }
}

/// <summary>Fehlerfälle, ohne die Einrichtung abzuschließen.</summary>
public sealed class SetupValidationTests(ServerFixture server) : IClassFixture<ServerFixture>
{
    [Fact]
    public async Task EveryStepNeedsTheSetupToken()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = server.CreateClient();

        foreach (var (uri, body) in new (string, object)[]
                 {
                     ("/api/setup/token", new SetupTokenRequest("")),
                     ("/api/setup/totp", new SetupTokenRequest("ABCD-EFGH")),
                     ("/api/setup/totp/verify", new TotpVerifyRequest("ABCD", "x", "123456")),
                 })
        {
            using var response = await client.PostAsJsonAsync(uri, body, cancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    [Fact]
    public async Task ForgedTotpConfirmationIsRejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = server.CreateClient();
        await using var crypto = new ReferenceMyLifeCrypto();
        var (request, _, _) = await SetupTestSupport.PrepareAsync(client, SetupTestSupport.Token(server), crypto, cancellationToken);

        var api = new SetupClient(client);
        var enrollment = await api.CreateTotpAsync(SetupTestSupport.Token(server), cancellationToken);
        // Die Anmeldung zur Einrichtung ist keine Bestätigung: anderer Zweck, andere Data-Protection-Schlüssel.
        var forged = request with { TotpConfirmation = enrollment.Enrollment };

        var error = await Assert.ThrowsAsync<SetupException>(() => api.CompleteAsync(forged, cancellationToken));
        Assert.Equal("setup-totp-expired", error.Message);
        Assert.True(await api.IsRequiredAsync(cancellationToken));
    }

    [Fact]
    public async Task MalformedKeyMaterialIsRejected()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = server.CreateClient();
        await using var crypto = new ReferenceMyLifeCrypto();
        var (request, _, _) = await SetupTestSupport.PrepareAsync(client, SetupTestSupport.Token(server), crypto, cancellationToken);
        var notOnCurve = new byte[65];
        notOnCurve[0] = 0x04;

        var broken = request with
        {
            Email = "keine-adresse",
            PublicSigningKey = notOnCurve,
            WrappedVmk = request.WrappedVmk[..40],
            KdfMemoryKiB = 1024,
            WrappedEncryptionPrivateKey = request.WrappedSigningPrivateKey,
        };

        using var response = await client.PostAsJsonAsync("/api/setup/complete", broken, cancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>(cancellationToken);
        Assert.NotNull(problem);
        Assert.Equal(
            ["Email", "KdfMemoryKiB", "PublicSigningKey", "WrappedEncryptionPrivateKey", "WrappedVmk"],
            problem.Errors.Keys.Order(StringComparer.Ordinal));
    }

    private sealed record ValidationProblem(Dictionary<string, string[]> Errors);
}
