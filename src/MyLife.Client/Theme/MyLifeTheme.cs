using MudBlazor;

namespace MyLife.Client.Theme;

/// <summary>
/// Farbschema nach dem rixio-Template (Entscheidung 71): Graphit-Flächen, Stahlblau als Zweitfarbe und das
/// riXio-Orange #FAA500 für Hauptaktionen. Schrift Titillium Web, selbst gehostet unter wwwroot/fonts, damit keine
/// Daten an Dritte (Font-CDN) gehen.
/// </summary>
public static class MyLifeTheme
{
    public const string Orange = "#faa500";
    private const string OrangeInk = "#241b0b";
    private const string SteelBlue = "#587894";

    private static readonly string[] Fonts =
        ["Titillium Web", "-apple-system", "BlinkMacSystemFont", "Segoe UI", "Roboto", "Helvetica Neue", "Arial", "sans-serif"];

    public static MudTheme Create() => new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = Orange,
            PrimaryContrastText = OrangeInk,
            Secondary = SteelBlue,
            Tertiary = "#865500",
            Background = "#f3f6f9",
            BackgroundGray = "#eaf0f5",
            Surface = "#ffffff",
            AppbarBackground = "#23394c",
            AppbarText = "#d5dfe7",
            DrawerBackground = "#eaf0f5",
            DrawerText = "#53687a",
            DrawerIcon = "#53687a",
            TextPrimary = "#202d39",
            TextSecondary = "#53687a",
            ActionDefault = "#4f6679",
            LinesDefault = "#d5dfe7",
            LinesInputs = "#b8cddd",
            TableLines = "#d5dfe7",
            TableStriped = "#f7fafc",
            Divider = "#d5dfe7",
            OverlayDark = "#23394c66",
        },
        PaletteDark = new PaletteDark
        {
            Primary = Orange,
            PrimaryContrastText = OrangeInk,
            Secondary = SteelBlue,
            Tertiary = "#f4d394",
            Black = "#111519",
            Background = "#111519",
            BackgroundGray = "#171e25",
            Surface = "#1a2026",
            AppbarBackground = "#111519",
            AppbarText = "#96a5b1",
            DrawerBackground = "#151b21",
            DrawerText = "#96a5b1",
            DrawerIcon = "#96a5b1",
            TextPrimary = "#edf2f6",
            TextSecondary = "#96a5b1",
            TextDisabled = "#96a5b173",
            ActionDefault = "#aab6b8",
            LinesDefault = "#2c3741",
            LinesInputs = "#3c4e5e",
            TableLines = "#2c3741",
            TableStriped = "#ffffff05",
            TableHover = "#ffffff0a",
            Divider = "#2c3741",
            DividerLight = "#2c374180",
            OverlayDark = "#000000aa",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = Fonts },
            H4 = new H4Typography { FontFamily = Fonts, FontWeight = "600", LetterSpacing = "-1px", LineHeight = "1.2" },
            H5 = new H5Typography { FontFamily = Fonts, FontWeight = "600" },
            H6 = new H6Typography { FontFamily = Fonts, FontWeight = "600" },
            Button = new ButtonTypography { FontFamily = Fonts, FontWeight = "600", TextTransform = "none" },
        },
        LayoutProperties = new LayoutProperties { DefaultBorderRadius = "8px", DrawerWidthLeft = "248px", AppbarHeight = "64px" },
    };
}
