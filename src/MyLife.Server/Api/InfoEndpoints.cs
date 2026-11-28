using System.Reflection;
using MyLife.Crypto;
using MyLife.Shared;

namespace MyLife.Server.Api;

internal static class InfoEndpoints
{
    public static IEndpointRouteBuilder MapInfoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/info", (Microsoft.Extensions.Options.IOptions<MyLife.Server.Infrastructure.MyLifeOptions> options) =>
                TypedResults.Ok(new AppInfoDto(AppVersion, CryptoFormat.CurrentVersion, options.Value.MyDocsOrigin is { } origin ? origin + "/" : null)))
            .WithName("GetAppInfo");
        return app;
    }

    internal static string AppVersion { get; } =
        typeof(InfoEndpoints).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? "0.0.0";
}
