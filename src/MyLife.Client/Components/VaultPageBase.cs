using Microsoft.AspNetCore.Components;
using MyLife.Client.Services;

namespace MyLife.Client.Components;

/// <summary>
/// Basis für Seiten mit Tresorinhalten: lädt und entschlüsselt den Tresor beim ersten Aufruf, zeigt Fehler an
/// und zeichnet bei Änderungen neu.
/// </summary>
public abstract class VaultPageBase : ComponentBase, IDisposable
{
    [Inject] protected VaultService Vault { get; set; } = default!;

    [Inject] protected SchemaText Text { get; set; } = default!;

    protected bool Loading { get; private set; } = true;

    protected bool LoadFailed { get; private set; }

    protected override async Task OnInitializedAsync()
    {
        Vault.Changed += OnVaultChanged;
        try
        {
            await Vault.EnsureLoadedAsync();
            OnVaultReady();
        }
        catch (Exception exception) when (exception is VaultException or HttpRequestException)
        {
            LoadFailed = true;
        }
        finally
        {
            Loading = false;
        }
    }

    /// <summary>Wird aufgerufen, sobald der Tresor entschlüsselt vorliegt, und nach jeder Änderung.</summary>
    protected virtual void OnVaultReady()
    {
    }

    public void Dispose()
    {
        Vault.Changed -= OnVaultChanged;
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
    }

    private void OnVaultChanged()
    {
        if (Vault.IsLoaded)
        {
            OnVaultReady();
        }

        _ = InvokeAsync(StateHasChanged);
    }
}
