using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MyLife.Tests;

/// <summary>
/// Durchsicht der Texte (M3.6): Platzhalter in beiden Sprachen gleich, kein Deutsch im Englischen, keine Hinweise auf
/// künftige Meilensteine; Barrierefreiheit: Symbolknöpfe und Bilder haben eine Beschriftung.
/// </summary>
public sealed partial class TextQualityTests
{
    private static readonly string Root = ResourceParityTests.FindRepositoryRoot();

    public static TheoryData<string> Files => new() { "src/MyLife.Client/Resources/SharedResource", "src/MyLife.Server/Resources/MailResource" };

    [Theory]
    [MemberData(nameof(Files))]
    public void PlaceholdersMatchAndEnglishIsEnglish(string file)
    {
        var german = Load($"{file}.resx");
        var english = Load($"{file}.en.resx");
        foreach (var (key, value) in german)
        {
            Assert.True(Placeholders(value).SequenceEqual(Placeholders(english[key])), $"Platzhalter in {key}: {value} | {english[key]}");
            Assert.False(Umlaut().IsMatch(english[key]) && key != "Option_pensionKind_ruerup", $"Deutsch im Englischen: {key}: {english[key]}");
            Assert.DoesNotContain('"', english[key]);
        }
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void TextsDoNotPromiseFutureMilestones(string file)
    {
        foreach (var language in new[] { ".resx", ".en.resx" })
        {
            foreach (var (key, value) in Load(file + language))
            {
                Assert.False(Milestone().IsMatch(value), $"Veralteter Hinweis in {key}: {value}");
            }
        }
    }

    [Fact]
    public void IconButtonsAndImagesHaveAnAccessibleName()
    {
        foreach (var path in Directory.GetFiles(Path.Combine(Root, "src", "MyLife.Client"), "*.razor", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(path);
            foreach (Match tag in Tag().Matches(text))
            {
                var attributes = tag.Groups[2].Value;
                var named = tag.Groups[1].Value is "MudImage" or "img"
                    ? attributes.Contains("Alt=", StringComparison.Ordinal) || attributes.Contains("alt=", StringComparison.Ordinal)
                    : attributes.Contains("aria-label", StringComparison.Ordinal);
                Assert.True(named, $"{Path.GetFileName(path)}: <{tag.Groups[1].Value}> ohne Beschriftung");
            }
        }
    }

    private static Dictionary<string, string> Load(string relative) =>
        XDocument.Load(Path.Combine(Root, relative)).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty, StringComparer.Ordinal);

    private static IEnumerable<string> Placeholders(string value) => Placeholder().Matches(value).Select(m => m.Value).Order(StringComparer.Ordinal);

    [GeneratedRegex(@"\{\w+\}")]
    private static partial Regex Placeholder();

    [GeneratedRegex("[äöüÄÖÜß]")]
    private static partial Regex Umlaut();

    [GeneratedRegex(@"\bM[1-9](\.\d+)?\b|folgt mit|folgen mit|coming with|follows? with")]
    private static partial Regex Milestone();

    [GeneratedRegex(@"<(MudIconButton|MudFab|MudImage|img)\b(.*?)/?>", RegexOptions.Singleline)]
    private static partial Regex Tag();
}
