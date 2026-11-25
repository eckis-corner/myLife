using System.Globalization;
using MyLife.Shared;

namespace MyLife.Client.Help;

/// <summary>
/// Tutorial und Anleitung von myLife. Anders als die kurzen Texte der Oberfläche stehen sie nicht in den
/// Ressourcen-Dateien, sondern je Sprache in einer eigenen Datei (<c>HelpContent.De.cs</c>, <c>HelpContent.En.cs</c>).
/// Beide Sprachen haben dieselben Kapitel, Teile und Themen mit denselben Kurznamen; ein Test hält das fest.
/// Wer einen Ablauf ändert, ändert ihn im Tutorial und in der Anleitung, in beiden Sprachen.
/// </summary>
public static partial class HelpContent
{
    /// <summary>Kurzname des Teils der Anleitung, der immer zuletzt steht.</summary>
    public const string AdministrationPart = "verwaltung";

    /// <summary>Kurzname des Kapitels des Tutorials, das immer zuletzt steht.</summary>
    public const string AdministrationChapter = "verwaltung";

    private static readonly Lazy<HelpTexts> GermanTexts = new(() => Ordered(German()));
    private static readonly Lazy<HelpTexts> EnglishTexts = new(() => Ordered(English()));

    /// <summary>Die Texte in der Sprache der Oberfläche.</summary>
    public static HelpTexts Current => For(CultureInfo.CurrentUICulture.Name);

    /// <summary>Die Texte für eine Sprache wie „de“ oder „en-GB“; für unbekannte Sprachen Deutsch.</summary>
    public static HelpTexts For(string? culture) =>
        SupportedCultures.Resolve(culture) == "en" ? EnglishTexts.Value : GermanTexts.Value;

    /// <summary>Alle Sprachen, für Tests.</summary>
    public static IReadOnlyDictionary<string, HelpTexts> All => new Dictionary<string, HelpTexts>
    {
        ["de"] = GermanTexts.Value,
        ["en"] = EnglishTexts.Value,
    };

    /// <summary>Die Verwaltung steht in Tutorial und Anleitung immer am Ende: Sie betrifft nur Administratoren.</summary>
    private static HelpTexts Ordered(HelpTexts texts) => new(
        [.. texts.Tutorial.Where(c => c.Id != AdministrationChapter), .. texts.Tutorial.Where(c => c.Id == AdministrationChapter)],
        [.. texts.Guide.Where(p => p.Id != AdministrationPart), .. texts.Guide.Where(p => p.Id == AdministrationPart)]);
}
