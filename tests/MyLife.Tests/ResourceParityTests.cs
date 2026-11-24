using System.Xml.Linq;

namespace MyLife.Tests;

/// <summary>Alle Sprachen müssen dieselben Schlüssel haben, kein Text darf leer sein.</summary>
public sealed class ResourceParityTests
{
    private static readonly string ResourceDirectory = Path.Combine(
        FindRepositoryRoot(), "src", "MyLife.Client", "Resources");

    [Fact]
    public void GermanAndEnglishHaveTheSameKeys()
    {
        var german = Load("SharedResource.resx");
        var english = Load("SharedResource.en.resx");

        Assert.Equal(german.Keys.Order(), english.Keys.Order());
    }

    [Theory]
    [InlineData("SharedResource.resx")]
    [InlineData("SharedResource.en.resx")]
    public void NoTextIsEmpty(string file)
    {
        var empty = Load(file).Where(x => string.IsNullOrWhiteSpace(x.Value)).Select(x => x.Key).ToList();
        Assert.Empty(empty);
    }

    [Fact]
    public void GermanTextsUseRealUmlauts()
    {
        string[] substitutes = ["Uebersicht", "Menue", "fuer", "Schluessel", "Woerter", "moeglich", "Waehle"];
        var german = Load("SharedResource.resx");

        var offenders = german
            .Where(x => substitutes.Any(s => x.Value.Contains(s, StringComparison.OrdinalIgnoreCase)))
            .Select(x => x.Key)
            .ToList();

        Assert.Empty(offenders);
    }

    private static Dictionary<string, string> Load(string file) =>
        XDocument.Load(Path.Combine(ResourceDirectory, file))
            .Root!
            .Elements("data")
            .ToDictionary(x => (string)x.Attribute("name")!, x => (string?)x.Element("value") ?? string.Empty);

    internal static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MyLife.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository-Wurzel nicht gefunden.");
    }
}
