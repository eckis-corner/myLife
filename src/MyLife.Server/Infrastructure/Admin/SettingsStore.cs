using System.Text.Json;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyLife.Server.Data;
using MyLife.Shared.Admin;

namespace MyLife.Server.Infrastructure.Admin;

/// <summary>
/// Globale Einstellungen (Architektur 6.1, Tabelle settings) als ein JSON-Wert in <c>system_settings</c>.
/// SMTP-Passwort und ntfy-Token liegen darin mit Data Protection verschlüsselt und verlassen den Server nie.
/// Die Einstellungen werden zwischengespeichert; die Abmeldezeit wirkt sofort auf das Sitzungs-Cookie.
/// </summary>
internal sealed class SettingsStore(
    IServiceScopeFactory scopes, IDataProtectionProvider protection, IOptions<MyLifeOptions> options,
    IOptionsMonitor<CookieAuthenticationOptions> cookies, TimeProvider time) : IDisposable
{
    internal const string SettingKey = "admin:settings";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _lock = new(1, 1);
    private AdminSettingsDto? _cache;

    /// <summary>Gespeicherte Form: Geheimnisse noch verschlüsselt (siehe <see cref="SmtpPassword"/>).</summary>
    public async Task<AdminSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_cache is { } cached)
        {
            return cached;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_cache is null)
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<MyLifeDbContext>();
                var setting = await db.SystemSettings.AsNoTracking().SingleOrDefaultAsync(s => s.Key == SettingKey, cancellationToken);
                _cache = setting?.Value is { } value ? JsonSerializer.Deserialize<AdminSettingsDto>(value, Json) ?? Defaults() : Defaults();
                ApplyIdleTimeout(_cache);
            }

            return _cache;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Für die Oberfläche: ohne Geheimnisse, nur ob welche hinterlegt sind.</summary>
    public async Task<AdminSettingsDto> GetForAdminAsync(CancellationToken cancellationToken = default)
    {
        var stored = await GetAsync(cancellationToken);
        return stored with
        {
            Smtp = stored.Smtp with { Password = null, HasPassword = stored.Smtp.Password is not null },
            Ntfy = stored.Ntfy with { AccessToken = null, HasAccessToken = stored.Ntfy.AccessToken is not null },
        };
    }

    /// <summary>
    /// Speichert geprüfte Einstellungen. Geheimnisse: null behält den alten Wert, leer entfernt ihn.
    /// Der Aufrufer prüft vorher mit <see cref="AdminSettingsValidation"/>.
    /// </summary>
    public async Task SaveAsync(MyLifeDbContext db, AdminSettingsDto incoming, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(incoming);
        var current = await GetAsync(cancellationToken);
        var normalized = Normalize(incoming);
        var stored = normalized with
        {
            Smtp = normalized.Smtp with { Password = Secret(incoming.Smtp.Password, current.Smtp.Password, SmtpPurpose), HasPassword = false },
            Ntfy = normalized.Ntfy with { AccessToken = Secret(incoming.Ntfy.AccessToken, current.Ntfy.AccessToken, NtfyPurpose), HasAccessToken = false },
        };

        var value = JsonSerializer.Serialize(stored, Json);
        var setting = await db.SystemSettings.SingleOrDefaultAsync(s => s.Key == SettingKey, cancellationToken);
        if (setting is null)
        {
            db.SystemSettings.Add(new SystemSetting { Key = SettingKey, Value = value, UpdatedAt = time.GetUtcNow() });
        }
        else
        {
            setting.Value = value;
            setting.UpdatedAt = time.GetUtcNow();
        }

        await db.SaveChangesAsync(cancellationToken);
        _cache = stored;
        ApplyIdleTimeout(stored);
    }

    public string? SmtpPassword(AdminSettingsDto stored) => Reveal(stored?.Smtp.Password, SmtpPurpose);

    public string? NtfyToken(AdminSettingsDto stored) => Reveal(stored?.Ntfy.AccessToken, NtfyPurpose);

    public static bool IsMailConfigured(AdminSettingsDto settings) =>
        !string.IsNullOrWhiteSpace(settings?.Smtp.Host) && !string.IsNullOrWhiteSpace(settings.Smtp.FromAddress);

    public void Dispose() => _lock.Dispose();

    /// <summary>Verwirft den Zwischenspeicher (Tests, die die Datenbank direkt ändern).</summary>
    public void Invalidate() => _cache = null;

    private const string SmtpPurpose = "MyLife.Settings.SmtpPassword.v1";
    private const string NtfyPurpose = "MyLife.Settings.NtfyToken.v1";

    private AdminSettingsDto Defaults() => new()
    {
        IdleTimeoutMinutes = Math.Clamp(options.Value.IdleTimeoutMinutes, AdminLimits.IdleMinMinutes, AdminLimits.IdleMaxMinutes),
    };

    private static AdminSettingsDto Normalize(AdminSettingsDto settings) => settings with
    {
        PublicUrl = string.IsNullOrWhiteSpace(settings.PublicUrl) ? null : settings.PublicUrl.Trim().TrimEnd('/'),
        Smtp = settings.Smtp with
        {
            Host = settings.Smtp.Host?.Trim() ?? string.Empty,
            Username = string.IsNullOrWhiteSpace(settings.Smtp.Username) ? null : settings.Smtp.Username.Trim(),
            FromAddress = settings.Smtp.FromAddress?.Trim() ?? string.Empty,
            FromName = string.IsNullOrWhiteSpace(settings.Smtp.FromName) ? "myLife" : settings.Smtp.FromName.Trim(),
        },
        Ntfy = settings.Ntfy with { ServerUrl = string.IsNullOrWhiteSpace(settings.Ntfy.ServerUrl) ? null : settings.Ntfy.ServerUrl.Trim().TrimEnd('/') },
        PrivacyNotice = string.IsNullOrWhiteSpace(settings.PrivacyNotice) ? null : settings.PrivacyNotice.Trim(),
    };

    private string? Secret(string? incoming, string? currentProtected, string purpose) => incoming switch
    {
        null => currentProtected,
        "" => null,
        _ => protection.CreateProtector(purpose).Protect(incoming),
    };

    private string? Reveal(string? protectedValue, string purpose)
    {
        if (protectedValue is null)
        {
            return null;
        }

        try
        {
            return protection.CreateProtector(purpose).Unprotect(protectedValue);
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            // Schlüsselring verloren (Rücksicherung ohne data/app/keys): wie nicht hinterlegt behandeln.
            return null;
        }
    }

    /// <summary>
    /// Die Cookie-Optionen sind je Schema ein zwischengespeichertes Objekt, das der Handler bei jeder Anfrage liest:
    /// Eine geänderte Abmeldezeit gilt damit ab der nächsten Anfrage.
    /// </summary>
    private void ApplyIdleTimeout(AdminSettingsDto settings) =>
        cookies.Get(CookieAuthenticationDefaults.AuthenticationScheme).ExpireTimeSpan = TimeSpan.FromMinutes(settings.IdleTimeoutMinutes);
}
