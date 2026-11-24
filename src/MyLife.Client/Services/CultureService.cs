using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MyLife.Shared;

namespace MyLife.Client.Services;

/// <summary>Liest, setzt und speichert die Sprache der Oberfläche (im Browser, nicht auf dem Server).</summary>
public sealed class CultureService(IJSRuntime js, NavigationManager navigation)
{
    public static string Current => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    public async Task ApplyStoredCultureAsync()
    {
        var stored = await js.InvokeAsync<string?>("myLife.getStoredCulture");
        var culture = stored ?? await js.InvokeAsync<string?>("myLife.getBrowserLanguage");
        Apply(SupportedCultures.Resolve(culture));
        await js.InvokeVoidAsync("myLife.setDocumentLanguage", Current);
    }

    /// <summary>Speichert die Sprache und lädt die Seite neu, damit alle Ressourcen in der neuen Sprache geladen werden.</summary>
    public async Task ChangeAsync(string culture)
    {
        var resolved = SupportedCultures.Resolve(culture);
        if (resolved == Current)
        {
            return;
        }

        await js.InvokeVoidAsync("myLife.setStoredCulture", resolved);
        navigation.NavigateTo(navigation.Uri, forceLoad: true);
    }

    private static void Apply(string culture)
    {
        var info = CultureInfo.GetCultureInfo(culture);
        CultureInfo.DefaultThreadCurrentCulture = info;
        CultureInfo.DefaultThreadCurrentUICulture = info;
        CultureInfo.CurrentCulture = info;
        CultureInfo.CurrentUICulture = info;
    }
}
