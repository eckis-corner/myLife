using System.Reflection;
using MyLife.Crypto;
using MyLife.Shared;

namespace MyLife.Server.Api;

internal static class InfoEndpoints
{
    public static IEndpointRouteBuilder MapInfoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/info", () => TypedResults.Ok(new AppInfoDto(AppVersion, CryptoFormat.CurrentVersion)))
            .WithName("GetAppInfo");
        return app;
    }

    internal static string AppVersion { get; } =
        typeof(InfoEndpoints).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? "0.0.0";
}
