using MudBlazor;

namespace MyLife.Client.Theme;

/// <summary>Ruhiges Farbschema. Schriften aus dem System, damit keine Daten an Dritte (Font-CDN) gehen.</summary>
public static class MyLifeTheme
{
    private static readonly string[] SystemFonts =
        ["system-ui", "-apple-system", "Segoe UI", "Roboto", "Helvetica Neue", "Arial", "sans-serif"];

    public static MudTheme Create() => new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#3E5C76",
            Secondary = "#748CAB",
            Tertiary = "#8A9A5B",
            AppbarBackground = "#3E5C76",
            Background = "#F7F7F4",
            Surface = "#FFFFFF",
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#9DB4CC",
            Secondary = "#748CAB",
            Tertiary = "#A9B98A",
            AppbarBackground = "#1F2A36",
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
