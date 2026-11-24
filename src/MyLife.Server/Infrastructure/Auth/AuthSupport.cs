using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyLife.Crypto;
using MyLife.Server.Data;

namespace MyLife.Server.Infrastructure.Auth;

internal static class SecurityStamps
{
    public static string New() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
}

internal static class MyLifeClaims
{
    public const string Stamp = "mylife:stamp";

    public static Guid? UserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}

/// <summary>Zweck eines Zwischenschritt-Tokens. Jeder Zweck hat eigene Data-Protection-Schlüssel.</summary>
internal enum PendingPurpose
{
    SecondFactor,
    Recovery,
    PasskeyRegistration,
    PasskeyLogin,
}

/// <summary>
/// Kurzlebige, geschützte Tokens für mehrstufige Abläufe (Passwort → zweiter Faktor, Recovery-Key → neues Passwort).
/// Der Server hält dafür keinen Zustand. Jedes Token ist an Nutzer und Sicherheitsstempel gebunden und wird mit
/// dem nächsten Passwortwechsel ungültig.
/// </summary>
internal sealed class PendingTokens(IDataProtectionProvider protection, TimeProvider time)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    public string Issue(PendingPurpose purpose, User user, string payload = "") =>
        Protector(purpose).Protect($"{user.Id:N}|{user.SecurityStamp}|{payload}", time.GetUtcNow().Add(Lifetime));

    /// <summary>Liefert Nutzer-ID, Stempel und Nutzlast oder null, wenn das Token abgelaufen oder gefälscht ist.</summary>
    public (Guid UserId, string Stamp, string Payload)? Read(PendingPurpose purpose, string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        try
        {
            var parts = Protector(purpose).Unprotect(token, out var expiration).Split('|', 3);
            return expiration >= time.GetUtcNow() && parts.Length == 3 && Guid.TryParseExact(parts[0], "N", out var id)
                ? (id, parts[1], parts[2])
                : null;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private ITimeLimitedDataProtector Protector(PendingPurpose purpose) =>
        protection.CreateProtector($"MyLife.Auth.Pending.{purpose}.v1").ToTimeLimitedDataProtector();
}

/// <summary>Fehlversuche zählen und Konten vorübergehend sperren (Architektur 4.4).</summary>
internal sealed class LoginAttempts(IOptions<MyLifeOptions> options, TimeProvider time, IHttpContextAccessor http)
{
    public bool IsLocked(User user) => user.LockoutUntil is { } until && until > time.GetUtcNow();

    /// <summary>Zählt einen Fehlversuch und vermerkt ihn im Audit-Log des Kontos (gespeichert mit dem nächsten SaveChanges).</summary>
    public void Failed(User user, MyLifeDbContext db)
    {
        db.AuditLog.Add(AuditLog.Entry(MyLife.Shared.Vault.AuditActions.LoginFailed, null, user.Id, time.GetUtcNow(), http.HttpContext?.Connection.RemoteIpAddress));
        user.FailedLoginCount++;
        if (user.FailedLoginCount >= options.Value.LockoutThreshold)
        {
            user.LockoutUntil = time.GetUtcNow().AddMinutes(options.Value.LockoutMinutes);
            user.FailedLoginCount = 0;
        }
    }

    public static void Succeeded(User user)
    {
        user.FailedLoginCount = 0;
        user.LockoutUntil = null;
    }
}

/// <summary>
/// Scheinwerte für unbekannte Adressen bei der Voranmeldung: gleichbleibend je Adresse, damit sich nicht erkennen
/// lässt, welche Adressen ein Konto haben. Das Geheimnis liegt geschützt in den Systemeinstellungen.
/// </summary>
internal sealed class PreloginDecoy(IServiceScopeFactory scopes, IDataProtectionProvider protection) : IDisposable
{
    private const string SettingKey = "auth:prelogin-secret";
    private readonly SemaphoreSlim _lock = new(1, 1);
    private byte[]? _secret;

    public async Task<byte[]> SaltForAsync(string email, CancellationToken cancellationToken)
    {
        var secret = await GetSecretAsync(cancellationToken);
        return HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes("prelogin:" + email))[..16];
    }

    public void Dispose() => _lock.Dispose();

    private async Task<byte[]> GetSecretAsync(CancellationToken cancellationToken)
    {
        if (_secret is not null)
        {
            return _secret;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_secret is not null)
            {
                return _secret;
            }

            var protector = protection.CreateProtector("MyLife.Auth.PreloginSecret.v1");
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<MyLifeDbContext>();
            var setting = await db.SystemSettings.FindAsync([SettingKey], cancellationToken);
            if (setting?.Value is null)
            {
                var value = protector.Protect(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
                db.SystemSettings.Add(new SystemSetting { Key = SettingKey, Value = value, UpdatedAt = DateTimeOffset.UtcNow });
                try
                {
                    await db.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException)
                {
                    // Ein paralleler Aufruf war schneller; dessen Wert gilt.
                    db.ChangeTracker.Clear();
                }

                setting = await db.SystemSettings.AsNoTracking().SingleAsync(s => s.Key == SettingKey, cancellationToken);
            }

            return _secret = Convert.FromBase64String(protector.Unprotect(setting.Value!));
        }
        finally
        {
            _lock.Release();
        }
    }
}

/// <summary>Meldet einen Nutzer mit Sitzungs-Cookie an (nach Passwort und zweitem Faktor).</summary>
internal static class UserSignIn
{
    public static Task SignInAsync(HttpContext context, User user)
    {
        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(MyLifeClaims.Stamp, user.SecurityStamp),
        ];
        if (user.IsAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, "admin"));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        return context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
    }

    /// <summary>Prüft bei jeder Anfrage, ob Konto und Sicherheitsstempel noch gelten.</summary>
    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var id = context.Principal is null ? null : MyLifeClaims.UserId(context.Principal);
        var stamp = context.Principal?.FindFirstValue(MyLifeClaims.Stamp);
        var db = context.HttpContext.RequestServices.GetRequiredService<MyLifeDbContext>();
        var valid = id is not null && await db.Users.AnyAsync(u => u.Id == id && u.SecurityStamp == stamp && u.Status == UserStatus.Active);
        if (!valid)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}

/// <summary>
/// Verlangt bei schreibenden API-Aufrufen den Header <c>X-MyLife-Request</c>. Fremde Seiten können ihn ohne
/// CORS-Freigabe nicht setzen; das ergänzt SameSite=Strict als Schutz gegen CSRF.
/// </summary>
internal sealed class CsrfHeaderMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        if (request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
            && !HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method) && !HttpMethods.IsOptions(request.Method)
            && request.Headers[MyLife.Shared.Auth.ApiHeaders.Csrf] != MyLife.Shared.Auth.ApiHeaders.CsrfValue)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return context.Response.WriteAsJsonAsync(new { title = "csrf-header-missing", status = 400 });
        }

        return next(context);
    }
}

/// <summary>Vergleich mit einem Schein-Hash, damit unbekannte Adressen genauso lange brauchen wie bekannte.</summary>
internal static class DummyAuthHash
{
    private static readonly Lazy<Task<string>> Value = new(() => AuthHashHasher.HashAsync(RandomNumberGenerator.GetBytes(CryptoFormat.KeyLength)));

    public static async Task BurnAsync(byte[] authHash) => await AuthHashHasher.VerifyAsync(await Value.Value, authHash);
}
