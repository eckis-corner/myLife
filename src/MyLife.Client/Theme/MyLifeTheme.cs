using MudBlazor;
using MyCommon.Ui.Theme;

namespace MyLife.Client.Theme;

/// <summary>
/// Farbschema nach dem rixio-Template (Entscheidung 71), gemeinsam mit myDocs in MyCommon.Ui (144): Graphit-Flächen,
/// Stahlblau als Zweitfarbe und das riXio-Orange #FAA500 für Hauptaktionen. Schrift Titillium Web, selbst gehostet
/// unter <c>_content/MyCommon.Ui/fonts</c>, damit keine Daten an Dritte (Font-CDN) gehen.
/// </summary>
public static class MyLifeTheme
{
    public static MudTheme Create() => EckisTheme.Create(AppAccent.MyLife);
}
