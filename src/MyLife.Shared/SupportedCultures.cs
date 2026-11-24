namespace MyLife.Shared;

/// <summary>Sprachen der Oberfläche. Weitere Sprachen: Ressourcen-Datei ergänzen und hier eintragen.</summary>
public static class SupportedCultures
{
    public const string Default = "de";

    public static IReadOnlyList<string> All { get; } = ["de", "en"];

    /// <summary>Liefert die passende unterstützte Sprache zu einem Kulturnamen wie "en-US", sonst die Standardsprache.</summary>
    public static string Resolve(string? cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
        {
            return Default;
        }

        var language = cultureName.Split('-', 2)[0].ToLowerInvariant();
        return All.Contains(language) ? language : Default;
    }
}
