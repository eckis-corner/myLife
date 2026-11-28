namespace MyLife.Server.Infrastructure;

/// <summary>
/// Setzt strikte Sicherheits-Header. Skripte nur vom eigenen Server, kein Inline-JavaScript;
/// 'wasm-unsafe-eval' ist für Blazor WebAssembly nötig. Inline-Styles erlaubt MudBlazor (nur CSS).
/// <c>blob:</c> bei Bildern und Frames dient der Vorschau entschlüsselter Dokumente, die nur im Browser existieren.
/// API-Antworten (Chiffrat) landen in keinem Cache. Mit der Brücke (M5) darf der Browser zusätzlich myDocs abfragen.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next, Microsoft.Extensions.Options.IOptions<MyLifeOptions> options)
{
    private readonly string _policy = ContentSecurityPolicy.Replace("connect-src 'self';", options.Value.MyDocsOrigin is { } origin ? $"connect-src 'self' {origin};" : "connect-src 'self';", StringComparison.Ordinal);

    internal const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self' 'wasm-unsafe-eval'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: blob:; " +
        "font-src 'self'; " +
        "connect-src 'self'; " +
        "worker-src 'self' blob:; " +
        "frame-src 'self' blob:; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'";

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.ContentSecurityPolicy = _policy;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = "camera=(self), microphone=(), geolocation=(), payment=(), usb=()";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            headers["Cross-Origin-Resource-Policy"] = "same-origin";
            if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            {
                headers.CacheControl = "no-store";
            }

            return Task.CompletedTask;
        });

        return next(context);
    }
}
