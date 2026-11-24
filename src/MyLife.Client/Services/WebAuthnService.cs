using Microsoft.JSInterop;

namespace MyLife.Client.Services;

/// <summary>Passkeys im Browser über <c>wwwroot/js/mylife.webauthn.js</c> (navigator.credentials).</summary>
public sealed class WebAuthnService(IJSRuntime js) : IAsyncDisposable
{
    private IJSObjectReference? _module;

    public async Task<bool> IsSupportedAsync() => await (await ModuleAsync()).InvokeAsync<bool>("isSupported");

    /// <summary>Registriert einen neuen Passkey. Liefert die Antwort des Authenticators als JSON.</summary>
    public Task<string> CreateAsync(string optionsJson) => CallAsync("create", optionsJson);

    /// <summary>Meldet sich mit einem Passkey an. Liefert die Antwort des Authenticators als JSON.</summary>
    public Task<string> GetAsync(string optionsJson) => CallAsync("get", optionsJson);

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

    private async Task<string> CallAsync(string function, string optionsJson)
    {
        try
        {
            return await (await ModuleAsync()).InvokeAsync<string>(function, optionsJson);
        }
        catch (JSException exception)
        {
            // Abbruch durch den Nutzer (NotAllowedError) oder kein passender Authenticator.
            throw new AuthException(
                exception.Message.Contains("mylife:unsupported", StringComparison.Ordinal) ? ClientAuthErrors.PasskeyUnsupported : ClientAuthErrors.PasskeyCancelled,
                exception);
        }
    }

    private async Task<IJSObjectReference> ModuleAsync() =>
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/mylife.webauthn.js");
}
