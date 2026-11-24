using Microsoft.JSInterop;

namespace MyLife.Client.Services;

/// <summary>Entschlüsselte Dateien im Browser anzeigen oder speichern (Blob-URLs, nie über den Server).</summary>
public sealed class BrowserFiles(IJSRuntime js)
{
    public ValueTask SaveAsync(byte[] content, string fileName, string contentType) =>
        js.InvokeVoidAsync("myLife.downloadBytes", content, fileName, contentType);

    public ValueTask<string> CreateUrlAsync(byte[] content, string contentType) =>
        js.InvokeAsync<string>("myLife.createObjectUrl", content, contentType);

    public ValueTask RevokeUrlAsync(string url) => js.InvokeVoidAsync("myLife.revokeObjectUrl", url);

    public ValueTask PrintAsync() => js.InvokeVoidAsync("myLife.print");

    /// <summary>Text in die Zwischenablage; false, wenn der Browser es nicht erlaubt.</summary>
    public ValueTask<bool> CopyTextAsync(string text) => js.InvokeAsync<bool>("myLife.copyText", text);

    /// <summary>Dateiname mit Datum, etwa „myLife-Notfallordner-2026-11-24.pdf“.</summary>
    public static string DatedName(string prefix, string extension, DateTimeOffset now) =>
        $"{prefix}-{now.ToLocalTime():yyyy-MM-dd}.{extension}";

    public static bool CanPreview(string contentType) =>
        contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) || contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase);
}
