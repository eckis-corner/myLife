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

    [Inject] private IServiceProvider Services { get; set; } = default!;

    protected bool Loading { get; private set; } = true;

    protected bool LoadFailed { get; private set; }

    protected override async Task OnInitializedAsync()
    {
        Vault.Changed += OnVaultChanged;
        try
        {
            await Vault.EnsureLoadedAsync();
            OnVaultReady();
            _ = SyncRemindersAsync();
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
            _ = SyncRemindersAsync();
        }

        _ = InvokeAsync(StateHasChanged);
    }

    /// <summary>Meldet nach dem Laden und jeder Änderung den nächsten Termin für Erinnerungen (M3.1); Fehler stören die Seite nicht.</summary>
    private async Task SyncRemindersAsync()
    {
        if (Services.GetService(typeof(ReminderSync)) is not ReminderSync reminders)
        {
            return;
        }

        try
        {
            await reminders.SyncAsync();
        }
        catch (Exception exception) when (exception is HttpRequestException or System.Text.Json.JsonException or InvalidOperationException)
        {
            // Beim nächsten Laden wird es erneut versucht.
        }
    }
}
