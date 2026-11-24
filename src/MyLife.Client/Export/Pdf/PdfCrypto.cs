using Microsoft.JSInterop;

namespace MyLife.Client.Export.Pdf;

/// <summary>Krypto-Bausteine für den PDF-Passwortschutz. Im Browser über die Web Crypto API, in Tests über .NET.</summary>
public interface IPdfCrypto
{
    /// <summary>SHA-256, SHA-384 oder SHA-512.</summary>
    Task<byte[]> DigestAsync(int bits, byte[] data);

    /// <summary>AES-CBC (Schlüssel 16 oder 32 Byte) mit PKCS#7-Auffüllung; das Ergebnis ist also immer um 1–16 Byte länger.</summary>
    Task<byte[]> AesCbcEncryptAsync(byte[] key, byte[] iv, byte[] data);
}

/// <summary>Über <c>wwwroot/js/mylife.pdf.js</c>; AES ist in .NET unter WebAssembly nicht verfügbar.</summary>
public sealed class JsPdfCrypto(IJSRuntime js) : IPdfCrypto, IAsyncDisposable
{
    private IJSObjectReference? _module;

    public async Task<byte[]> DigestAsync(int bits, byte[] data) =>
        await (await ModuleAsync()).InvokeAsync<byte[]>("digest", bits, data);

    public async Task<byte[]> AesCbcEncryptAsync(byte[] key, byte[] iv, byte[] data) =>
        await (await ModuleAsync()).InvokeAsync<byte[]>("aesCbcEncrypt", key, iv, data);

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Seite wird geschlossen.
            }
        }
    }

    private async Task<IJSObjectReference> ModuleAsync() =>
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/mylife.pdf.js");
}
