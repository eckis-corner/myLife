using MudBlazor;

namespace MyLife.Client.Components;

/// <summary>Symbole der Bereiche für Dashboard, Navigation und Listen.</summary>
public static class AreaIcons
{
    public static string For(int area) => area switch
    {
        0 => Icons.Material.Outlined.Person,
        1 => Icons.Material.Outlined.AccountBalance,
        2 => Icons.Material.Outlined.CreditCard,
        3 => Icons.Material.Outlined.RequestQuote,
        4 => Icons.Material.Outlined.Shield,
        5 => Icons.Material.Outlined.Description,
        6 => Icons.Material.Outlined.Gavel,
        7 => Icons.Material.Outlined.Home,
        8 => Icons.Material.Outlined.Cloud,
        9 => Icons.Material.Outlined.Key,
        10 => Icons.Material.Outlined.Contacts,
        11 => Icons.Material.Outlined.FavoriteBorder,
        12 => Icons.Material.Outlined.Checklist,
        13 => Icons.Material.Outlined.Savings,
        _ => Icons.Material.Outlined.Folder,
    };
}
