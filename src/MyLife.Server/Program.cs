using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using MyLife.Server.Api;
using MyLife.Server.Components;
using MyLife.Server.Data;
using MyLife.Server.Infrastructure;
using MyLife.Server.Infrastructure.Admin;
using MyLife.Server.Infrastructure.Auth;
using MyLife.Server.Infrastructure.Mail;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
// Kein „Server: Kestrel“ in den Antworten (M3.6).
builder.WebHost.ConfigureKestrel(kestrel => kestrel.AddServerHeader = false);

builder.Services.AddSerilog((services, configuration) => configuration
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture));

var options = builder.Configuration.GetSection(MyLifeOptions.SectionName).Get<MyLifeOptions>() ?? new MyLifeOptions();
builder.Services.Configure<MyLifeOptions>(builder.Configuration.GetSection(MyLifeOptions.SectionName));

var dataDirectory = Path.GetFullPath(options.DataDirectory);

// Rücksicherung von der Kommandozeile (M3.4): dotnet MyLife.Server.dll restore <Datei> --confirm
if (args is ["restore", ..])
{
    return await MyLife.Server.Infrastructure.Backup.BackupCli.RestoreAsync(args, builder.Configuration, options);
}

Directory.CreateDirectory(Path.Combine(dataDirectory, "keys"));
builder.Services.AddDataProtection()
    .SetApplicationName("myLife")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDirectory, "keys")));

builder.Services.AddDbContext<MyLifeDbContext>(db =>
    db.UseNpgsql(builder.Configuration.GetConnectionString("MyLife")));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<MyLifeDbContext>("database", tags: ["ready"]);

builder.Services.Configure<ForwardedHeadersOptions>(forwarded =>
{
    forwarded.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    forwarded.KnownIPNetworks.Clear();
    forwarded.KnownProxies.Clear();
    foreach (var network in options.TrustedProxyNetworks)
    {
        forwarded.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
    }
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<SetupTokenService>();
builder.Services.AddHostedService<SetupTokenIssuer>();
builder.Services.AddSingleton<PendingTokens>();
builder.Services.AddSingleton<LoginAttempts>();
builder.Services.AddSingleton<PreloginDecoy>();
builder.Services.AddSingleton<PasskeyService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuditLog>();
builder.Services.AddSingleton<FileStore>();
builder.Services.AddSingleton<TrashPurgeService>();
builder.Services.AddHostedService(services => services.GetRequiredService<TrashPurgeService>());
builder.Services.AddSingleton<SettingsStore>();
builder.Services.AddSingleton<MailSignal>();
builder.Services.AddSingleton<IMailTransport, SmtpMailTransport>();
builder.Services.AddScoped<MailComposer>();
builder.Services.AddScoped<MailOutbox>();
builder.Services.AddSingleton<MailOutboxService>();
builder.Services.AddHostedService(services => services.GetRequiredService<MailOutboxService>());
builder.Services.AddHttpClient(NtfyClient.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddSingleton<NtfyClient>();
builder.Services.AddSingleton<MyLife.Server.Infrastructure.Emergency.KnownBaseUrl>();
builder.Services.AddScoped<MyLife.Server.Infrastructure.Emergency.EmergencyEngine>();
builder.Services.AddSingleton<MyLife.Server.Infrastructure.Emergency.EmergencySwitchService>();
builder.Services.AddHostedService(services => services.GetRequiredService<MyLife.Server.Infrastructure.Emergency.EmergencySwitchService>());
builder.Services.AddScoped<MyLife.Server.Infrastructure.Reminders.ReminderEngine>();
builder.Services.AddSingleton<MyLife.Server.Infrastructure.Reminders.ReminderService>();
builder.Services.AddHostedService(services => services.GetRequiredService<MyLife.Server.Infrastructure.Reminders.ReminderService>());
builder.Services.AddSingleton<MyLife.Server.Infrastructure.Backup.BackupService>();
builder.Services.AddHostedService(services => services.GetRequiredService<MyLife.Server.Infrastructure.Backup.BackupService>());

// Sitzungs-Cookie (Architektur 4.4): HttpOnly, SameSite=Strict, gleitende Laufzeit wie die automatische Abmeldung.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(cookie =>
    {
        cookie.Cookie.Name = "mylife.session";
        cookie.Cookie.HttpOnly = true;
        cookie.Cookie.SameSite = SameSiteMode.Strict;
        cookie.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        cookie.ExpireTimeSpan = TimeSpan.FromMinutes(options.IdleTimeoutMinutes);
        cookie.SlidingExpiration = true;
        cookie.Events.OnValidatePrincipal = UserSignIn.ValidateAsync;
        // Die API antwortet mit 401/403 statt auf eine Anmeldeseite umzuleiten.
        cookie.Events.OnRedirectToLogin = context => WriteStatusAsync(context.Response, StatusCodes.Status401Unauthorized, "auth-required");
        cookie.Events.OnRedirectToAccessDenied = context => WriteStatusAsync(context.Response, StatusCodes.Status403Forbidden, "auth-forbidden");
    });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AdminEndpoints.Policy, policy => policy.RequireRole("admin"));

builder.Services.AddRateLimiter(limiter =>
{
    limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    limiter.AddPolicy(SetupEndpoints.RateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1) }));
    limiter.AddPolicy(AuthEndpoints.RateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = options.AuthRequestsPerMinute, Window = TimeSpan.FromMinutes(1) }));
    limiter.AddPolicy(VaultEndpoints.RateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = options.VaultRequestsPerMinute, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

var app = builder.Build();

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseSerilogRequestLogging();
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseMiddleware<CsrfHeaderMiddleware>();
// Merkt sich die Adresse der Instanz für Links in Mails, die der Hintergrunddienst verschickt.
app.Use((context, next) =>
{
    context.RequestServices.GetRequiredService<MyLife.Server.Infrastructure.Emergency.KnownBaseUrl>().Remember(context.Request);
    return next(context);
});
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseAntiforgery();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.MapInfoEndpoints();
app.MapSetupEndpoints();
app.MapAuthEndpoints();
app.MapAccountEndpoints();
app.MapVaultEndpoints();
app.MapAdminEndpoints();
app.MapAccountDeletionEndpoints();
app.MapFamilyEndpoints();
app.MapTrustEndpoints();
app.MapEmergencyEndpoints();
app.MapReminderEndpoints();
app.MapChecklistEndpoints();

app.MapStaticAssets();
// Die Seiten sind nur die leere Hülle (kein Prerendering); [Authorize] prüft der Client. Ohne AllowAnonymous
// würde der Server die Hülle geschützter Seiten verweigern und der Browser käme nie zur Anmeldung.
// Geschützt sind die Daten: Jeder API-Endpunkt verlangt die Anmeldung selbst.
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(MyLife.Client._Imports).Assembly)
    .AllowAnonymous();

await DatabaseMigrator.MigrateAsync(app.Services);
// Lädt die Einstellungen vorab; dabei gilt auch die vom Admin gewählte Abmeldezeit für das Sitzungs-Cookie.
await app.Services.GetRequiredService<SettingsStore>().GetAsync();

await app.RunAsync();
return 0;

static Task WriteStatusAsync(HttpResponse response, int status, string code)
{
    response.StatusCode = status;
    return response.WriteAsJsonAsync(new { title = code, status });
}

/// <summary>Für WebApplicationFactory in den Tests.</summary>
public partial class Program;
