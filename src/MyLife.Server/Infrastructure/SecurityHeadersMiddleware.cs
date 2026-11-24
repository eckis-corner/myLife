namespace MyLife.Server.Infrastructure;

/// <summary>
/// Setzt strikte Sicherheits-Header. Skripte nur vom eigenen Server, kein Inline-JavaScript;
/// 'wasm-unsafe-eval' ist für Blazor WebAssembly nötig. Inline-Styles erlaubt MudBlazor (nur CSS).
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    internal const string ContentSecurityPolicy =
        "default-src 'self'; " +
        "script-src 'self' 'wasm-unsafe-eval'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: blob:; " +
        "font-src 'self'; " +
        "connect-src 'self'; " +
        "worker-src 'self' blob:; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'none'";

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.ContentSecurityPolicy = ContentSecurityPolicy;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = "camera=(self), microphone=(), geolocation=(), payment=(), usb=()";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            return Task.CompletedTask;
        });

        return next(context);
    }
}
