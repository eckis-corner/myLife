using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor.Services;
using MyLife.Crypto;
using MyLife.Shared.Auth;
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
builder.Services.AddScoped<MyLifeAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(services => services.GetRequiredService<MyLifeAuthenticationStateProvider>());
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();

var host = builder.Build();

await host.Services.GetRequiredService<CultureService>().ApplyStoredCultureAsync();

await host.RunAsync();
