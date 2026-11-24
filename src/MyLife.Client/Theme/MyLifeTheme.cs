using MudBlazor;

namespace MyLife.Client.Theme;

/// <summary>Farbschema nach der Vorlage eckis-corner (#355269, Petrol #008080), passend zum Logo. Schriften aus dem System, damit keine Daten an Dritte (Font-CDN) gehen.</summary>
public static class MyLifeTheme
{
    private static readonly string[] SystemFonts =
        ["system-ui", "-apple-system", "Segoe UI", "Roboto", "Helvetica Neue", "Arial", "sans-serif"];

    public static MudTheme Create() => new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#355269",
            Secondary = "#008080",
            Tertiary = "#5B7A90",
            AppbarBackground = "#355269",
            Background = "#F7F7F4",
            Surface = "#FFFFFF",
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#9FB6CA",
            Secondary = "#4DB8B8",
            Tertiary = "#8FA9BE",
            AppbarBackground = "#1C2A35",
            Background = "#151B22",
            Surface = "#1E2630",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = SystemFonts },
        },
        LayoutProperties = new LayoutProperties { DefaultBorderRadius = "8px" },
    };
}
