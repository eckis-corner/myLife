using System.Globalization;
using System.Text.RegularExpressions;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using MyLife.Client.Help;
using MyLife.Client.Layout;
using MyLife.Client.Pages;
using MyLife.Client.Services;
using MyLife.Crypto;

namespace MyLife.Tests;

/// <summary>
/// Tutorial und Anleitung (Entscheidung 139): Beide Sprachen sind gleich gegliedert, alle Texte bleiben im erlaubten
/// Markdown, Verweise führen ins Leere nie, und die beiden Menüpunkte stehen ganz unten direkt über „Konto“.
/// </summary>
public sealed partial class HelpTests : BunitContext
{
    public HelpTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de");
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddLocalization();
    }

    public static TheoryData<string> Languages => new() { "de", "en" };

    [Fact]
    public void BothLanguagesHaveTheSameStructure()
    {
        var german = HelpContent.All["de"];
        var english = HelpContent.All["en"];

        Assert.Equal(german.Tutorial.Select(c => (c.Id, string.Join(',', c.Topics))), english.Tutorial.Select(c => (c.Id, string.Join(',', c.Topics))));
        Assert.Equal(german.Guide.Select(p => p.Id), english.Guide.Select(p => p.Id));
        Assert.Equal(
            german.Guide.SelectMany(p => p.Topics).Select(t => (t.Id, t.Page, t.Steps.Length, t.Notes.Length)),
            english.Guide.SelectMany(p => p.Topics).Select(t => (t.Id, t.Page, t.Steps.Length, t.Notes.Length)));
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void IdsAreUniqueAndReferencesResolve(string language)
    {
        var texts = HelpContent.All[language];
        var topics = texts.Guide.SelectMany(p => p.Topics).ToList();

        Assert.Empty(texts.Tutorial.GroupBy(c => c.Id).Where(g => g.Count() > 1).Select(g => g.Key));
        Assert.Empty(texts.Guide.GroupBy(p => p.Id).Where(g => g.Count() > 1).Select(g => g.Key));
        Assert.Empty(topics.GroupBy(t => t.Id).Where(g => g.Count() > 1).Select(g => g.Key));

        var ids = topics.Select(t => t.Id).ToHashSet(StringComparer.Ordinal);
        var dangling = texts.Tutorial.SelectMany(c => c.Topics.Where(t => !ids.Contains(t)).Select(t => $"{c.Id} → {t}"));
        Assert.Empty(dangling);
        Assert.All(topics, t => Assert.NotEmpty(t.Steps));
        Assert.All(texts.Tutorial, c => Assert.NotEmpty(c.Topics));
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void AllTextsAreAllowedMarkdown(string language)
    {
        var texts = HelpContent.All[language];
        var all = texts.Tutorial.SelectMany(c => new[] { (c.Id, c.Intro), (c.Id, c.Text) })
            .Concat(texts.Guide.SelectMany(p => p.Topics).SelectMany(t =>
                new[] { t.Intro }.Concat(t.Steps).Concat(t.Notes).Select(text => (t.Id, text))));

        var errors = all.SelectMany(x => HelpMarkdown.Validate(x.Item2).Select(e => $"{x.Item1}: {e}")).ToList();

        Assert.Empty(errors);
    }

    [Fact]
    public void EnglishContainsNoGerman()
    {
        var texts = HelpContent.All["en"];
        var all = texts.Tutorial.SelectMany(c => new[] { c.Title, c.Intro, c.Text })
            .Concat(texts.Guide.SelectMany(p => new[] { p.Title, p.Description }))
            .Concat(texts.Guide.SelectMany(p => p.Topics).SelectMany(t =>
                new[] { t.Title, t.Who, t.Intro, t.PageName ?? string.Empty }.Concat(t.Steps).Concat(t.Notes)));

        Assert.DoesNotContain(all, text => Umlaut().IsMatch(text));
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void AdministrationComesLast(string language)
    {
        var texts = HelpContent.All[language];

        Assert.Equal(HelpContent.AdministrationChapter, texts.Tutorial[^1].Id);
        Assert.Equal(HelpContent.AdministrationPart, texts.Guide[^1].Id);
    }

    /// <summary>Jede Seite, auf die ein Thema verweist, gibt es im Client.</summary>
    [Fact]
    public void LinkedPagesExist()
    {
        var root = ResourceParityTests.FindRepositoryRoot();
        var routes = Directory.GetFiles(Path.Combine(root, "src", "MyLife.Client", "Pages"), "*.razor", SearchOption.AllDirectories)
            .SelectMany(f => Route().Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value.TrimStart('/')))
            .ToHashSet(StringComparer.Ordinal);

        var missing = HelpContent.All["de"].Guide.SelectMany(p => p.Topics)
            .Where(t => t.Page is not null && !routes.Contains(t.Page == "./" ? string.Empty : t.Page))
            .Select(t => $"{t.Id} → {t.Page}");

        Assert.Empty(missing);
        Assert.Contains("tutorial", routes);
        Assert.Contains("guide", routes);
    }

    [Fact]
    public void MarkdownIsEncodedAndRendered()
    {
        var html = HelpMarkdown.ToHtml("""
            Text mit **fett** und <script>alert(1)</script>.

            - eins
            - zwei

            1. erstens
            2. zweitens

            > Hinweis

            | A | B |
            | --- | --- |
            | `x` | y |
            """);

        Assert.Contains("<strong>fett</strong>", html);
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("<ul><li>eins</li><li>zwei</li></ul>", html);
        Assert.Contains("<ol><li>erstens</li><li>zweitens</li></ol>", html);
        Assert.Contains("<blockquote>Hinweis</blockquote>", html);
        Assert.Contains("<th>A</th>", html);
        Assert.Contains("<td><code>x</code></td>", html);
        Assert.NotEmpty(HelpMarkdown.Validate("- Liste\nText"));
        Assert.NotEmpty(HelpMarkdown.Validate("**offen"));
    }

    [Fact]
    public void TutorialOpensTheRequestedChapterAndLinksOn()
    {
        var page = Open<TutorialPage>("tutorial?chapter=notfallzugang");

        Assert.Equal("Der Notfallzugang", page.Find("[data-testid=chapter-title]").TextContent);
        var chapters = HelpContent.All["de"].Tutorial;
        var index = Array.FindIndex(chapters, c => c.Id == "notfallzugang");
        Assert.Equal($"tutorial?chapter={chapters[index + 1].Id}", page.Find("[data-testid=chapter-next]").GetAttribute("href"));
        Assert.Equal($"tutorial?chapter={chapters[index - 1].Id}", page.Find("[data-testid=chapter-previous]").GetAttribute("href"));
        Assert.Contains(page.FindAll("a"), a => a.GetAttribute("href") == "guide?topic=lebenszeichen");
    }

    [Fact]
    public void TutorialStartsWithTheFirstChapterAndEndsAtTheOverview()
    {
        var first = Render<TutorialPage>();
        Assert.Equal(HelpContent.All["de"].Tutorial[0].Title, first.Find("[data-testid=chapter-title]").TextContent);

        var last = Open<TutorialPage>($"tutorial?chapter={HelpContent.AdministrationChapter}");
        Assert.Equal("./", last.Find("[data-testid=chapter-finish]").GetAttribute("href"));
    }

    [Fact]
    public void GuideOpensTheRequestedTopic()
    {
        var page = Open<GuidePage>("guide?topic=passkeys");

        var topic = page.Find("[data-testid=topic-passkeys]");
        Assert.Contains("Passkey hinzufügen", topic.TextContent);
        Assert.Contains(page.FindAll("a"), a => a.GetAttribute("href") == "account/security");
    }

    [Fact]
    public void GuideSearchFindsTopicsAndSaysWhenThereIsNothing()
    {
        var page = Render<GuidePage>();

        page.Find("input[data-testid=guide-search]").Input("Schlüsselblatt");
        // Die Suche wartet 200 ms auf weitere Eingaben; erst danach verschwinden die anderen Themen.
        page.WaitForAssertion(() => Assert.Empty(page.FindAll("[data-testid=topic-passkeys]")));
        Assert.NotNull(page.Find("[data-testid=topic-notfallpaket]"));

        page.Find("input[data-testid=guide-search]").Input("Quxbazfoo");
        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-testid=guide-no-hits]")));
    }

    [Fact]
    public void EnglishPagesUseEnglishContent()
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
        try
        {
            var page = Open<TutorialPage>("tutorial?chapter=notfallzugang");
            Assert.Equal("Emergency access", page.Find("[data-testid=chapter-title]").TextContent);
        }
        finally
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de");
        }
    }

    /// <summary>Tutorial und Anleitung stehen in der unteren, immer sichtbaren Leiste direkt über „Konto“.</summary>
    [Fact]
    public void HelpLinksSitDirectlyAboveAccount()
    {
        Services.AddSingleton(new SessionService(new AuthClient(new HttpClient()), new ReferenceMyLifeCrypto(), new WebAuthnService(JSInterop.JSRuntime)));
        Services.AddSingleton<CultureService>();
        AddAuthorization().SetAuthorized("anna@example.org");

        var layout = Render<MainLayout>(p => p.Add(x => x.Body, (RenderFragment)(_ => { })));

        var bottom = layout.Find(".mylife-nav-account");
        var items = bottom.QuerySelectorAll("[data-testid]").Select(e => e.GetAttribute("data-testid")).ToList();
        Assert.Equal(["nav-tutorial", "nav-guide", "nav-account"], items.Take(3));
        Assert.Empty(layout.Find(".mylife-nav-main").QuerySelectorAll("[data-testid=nav-tutorial], [data-testid=nav-guide]"));
        Assert.Equal("tutorial", layout.Find("[data-testid=nav-tutorial]").GetAttribute("href"));
        Assert.Equal("guide", layout.Find("[data-testid=nav-guide]").GetAttribute("href"));
    }

    /// <summary>Parameter aus der Adresse kommen in bUnit nur über den NavigationManager.</summary>
    private IRenderedComponent<TPage> Open<TPage>(string uri)
        where TPage : IComponent
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo(uri);
        return Render<TPage>();
    }

    [GeneratedRegex("[äöüÄÖÜß]")]
    private static partial Regex Umlaut();

    [GeneratedRegex("""@page "([^"{]*)"(?=\s)""")]
    private static partial Regex Route();
}
