using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor.Services;
using MyCommon.Ui;
using MyLife.Crypto;
using MyLife.Shared.Auth;
using MyLife.Client.Export;
using MyLife.Client.Export.Pdf;
using MyLife.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddMudServices();
builder.Services.AddLocalization();
builder.Services.AddScoped(_ =>
{
    var http = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
    // Schutz gegen CSRF: Der Server verlangt diesen Header bei schreibenden Aufrufen.
    http.DefaultRequestHeaders.Add(ApiHeaders.Csrf, ApiHeaders.CsrfValue);
    return http;
});
builder.Services.AddScoped<AppInfoClient>();
builder.Services.AddScoped<CultureService>();
builder.Services.AddScoped<IMyLifeCrypto, JsMyLifeCrypto>();
builder.Services.AddScoped<SetupClient>();
builder.Services.AddScoped<VaultSetupService>();
builder.Services.AddScoped<AuthClient>();
builder.Services.AddScoped<WebAuthnService>();
builder.Services.AddScoped<SessionService>();
builder.Services.AddScoped<RecoveryService>();
builder.Services.AddScoped<VaultClient>();
builder.Services.AddScoped<AdminClient>();
builder.Services.AddScoped<FamilyClient>();
builder.Services.AddScoped<KeyTrustService>();
builder.Services.AddScoped<TrustClient>();
builder.Services.AddScoped<TrustService>();
builder.Services.AddScoped<SharedService>();
builder.Services.AddScoped<EmergencyClient>();
builder.Services.AddScoped<ReminderClient>();
builder.Services.AddScoped<ChecklistClient>();
builder.Services.AddScoped<ReadingService>();
builder.Services.AddScoped<VaultService>();
builder.Services.AddMyCommonUi();
builder.Services.AddScoped<SchemaText>();
builder.Services.AddScoped<VaultSearch>();
builder.Services.AddScoped<PaymentOverview>();
builder.Services.AddScoped<BrowserFiles>();
builder.Services.AddScoped<IPdfCrypto, JsPdfCrypto>();
builder.Services.AddScoped<EmergencyFolder>();
builder.Services.AddScoped<VaultExport>();
builder.Services.AddScoped<EmergencyPackage>();
builder.Services.AddScoped<ReminderSync>();
builder.Services.AddScoped<ChecklistService>();
builder.Services.AddScoped<Completeness>();
builder.Services.AddScoped(services => new MyDocsBridge(services.GetRequiredService<VaultClient>(), services.GetRequiredService<VaultService>(),
    services.GetRequiredService<IMyLifeCrypto>(), services.GetRequiredService<SessionService>()));
builder.Services.AddScoped<MyLifeAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(services => services.GetRequiredService<MyLifeAuthenticationStateProvider>());
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();

var host = builder.Build();

await host.Services.GetRequiredService<CultureService>().ApplyStoredCultureAsync();

await host.RunAsync();
