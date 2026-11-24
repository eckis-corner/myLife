using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using MyLife.Crypto;
using MyLife.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddMudServices();
builder.Services.AddLocalization();
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<AppInfoClient>();
builder.Services.AddScoped<CultureService>();
builder.Services.AddScoped<IMyLifeCrypto, JsMyLifeCrypto>();

var host = builder.Build();

await host.Services.GetRequiredService<CultureService>().ApplyStoredCultureAsync();

await host.RunAsync();
