using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using MyLife.Client.Export;
using MyLife.Client.Export.Pdf;
using MyLife.Client.Pages;
using MyLife.Client.Services;
using MyLife.Shared.Vault;
using MyLife.Tests.Server;

namespace MyLife.Tests;

/// <summary>Die Tresor-Seiten mit echtem Server und entsperrtem Tresor (Krypto über die .NET-Referenz).</summary>
public sealed class VaultPageTests : BunitContext, IClassFixture<ServerFixture>, IAsyncLifetime
{
    private readonly ServerFixture _server;
    private VaultBrowser _browser = null!;

    public VaultPageTests(ServerFixture server)
    {
        _server = server;
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de");
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddLocalization();
    }

    public async ValueTask InitializeAsync()
    {
        _browser = await VaultBrowser.OpenAsync(await AuthTestSupport.ForAsync(_server));
        Services.AddSingleton(_browser.Session);
        Services.AddSingleton(_browser.Api);
        Services.AddSingleton(_browser.Vault);
        Services.AddSingleton<SchemaText>();
        Services.AddSingleton<VaultSearch>();
        Services.AddSingleton<BrowserFiles>();
        Services.AddSingleton<IPdfCrypto, MyLife.Tests.Export.DotNetPdfCrypto>();
        Services.AddSingleton<EmergencyFolder>();
        Services.AddSingleton(new VaultExport(_browser.Api, _browser.Vault, _browser.Session, _browser.Crypto));
        Render<MudBlazor.MudPopoverProvider>();
    }

    [Fact]
    public async Task DashboardShowsAreasWithCountsAndMarksThemReviewed()
    {
        await _browser.Vault.CreateAsync("card", new VaultPayload { Title = "Visa", Fields = { ["cardKind"] = "visa", ["last4"] = "4242" } });

        var page = Render<Home>();

        page.WaitForAssertion(() => Assert.NotNull(page.Find("[data-testid=area-tile-2]")));
        Assert.Equal(13, page.FindAll("[data-testid^=area-tile-]").Count);
        Assert.Contains("Kreditkarten", page.Find("[data-testid=area-tile-2]").TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Checkliste", page.Markup, StringComparison.Ordinal);

        page.Find("[data-testid=area-reviewed-2]").Click();
        page.WaitForAssertion(() => Assert.Contains("Zuletzt geprüft am", page.Find("[data-testid=area-tile-2]").TextContent, StringComparison.Ordinal));
    }

    [Fact]
    public void NewObjectFormIsBuiltFromTheSchemaAndSaves()
    {
        var page = Render<ObjectPage>(p => p.Add(x => x.TypeKey, "bankAccount"));

        page.WaitForAssertion(() => Assert.NotNull(page.Find("input[data-testid=object-title]")));
        Assert.NotNull(page.Find("input[data-testid=field-bank]"));
        Assert.NotNull(page.Find("input[data-testid=field-iban]"));
        Assert.Equal("Bankkonto", page.Find("input[data-testid=object-title]").GetAttribute("value"));

        page.Find("input[data-testid=field-iban]").Change("DE00 1234");
        page.Find("[data-testid=save]").Click();
        page.WaitForAssertion(() => Assert.Contains("Keine gültige IBAN", page.Markup, StringComparison.Ordinal));
        Assert.Contains("Pflichtfeld", page.Markup, StringComparison.Ordinal);

        page.Find("input[data-testid=object-title]").Change("Girokonto Testbank");
        page.Find("input[data-testid=field-bank]").Change("Testbank");
        page.Find("input[data-testid=field-iban]").Change("de89 3704 0044 0532 0130 00");
        page.Find("[data-testid=save]").Click();

        var navigation = Services.GetRequiredService<NavigationManager>();
        page.WaitForAssertion(() => Assert.Contains("/object/", navigation.Uri, StringComparison.Ordinal));
        var created = _browser.Vault.Current.Single(i => i.Title == "Girokonto Testbank");
        Assert.Equal("DE89370400440532013000", created.Payload.Get("iban"));
        Assert.EndsWith($"/object/{created.Id}", navigation.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DatasheetShowsFieldsAndLinksInBothDirections()
    {
        var house = await _browser.Vault.CreateAsync("property", new VaultPayload { Title = "Haus am See", Fields = { ["propertyKind"] = "house", ["address"] = "Seeweg 1" } });
        var loan = await _browser.Vault.CreateAsync("loan", new VaultPayload
        {
            Title = "Baufinanzierung", Fields = { ["loanDirection"] = "iOwe", ["counterparty"] = "Bausparkasse", ["collateral"] = house.Id.ToString(), ["interestRate"] = "3.15" },
        });

        var loanPage = Render<ObjectPage>(p => p.Add(x => x.Id, loan.Id));
        loanPage.WaitForAssertion(() => Assert.Equal("Baufinanzierung", loanPage.Find("[data-testid=object-heading]").TextContent));
        var sheet = loanPage.Find("[data-testid=datasheet]").TextContent;
        Assert.Contains("Ich schulde", sheet, StringComparison.Ordinal);
        Assert.Contains("3,15 %", sheet, StringComparison.Ordinal);
        Assert.Contains("Haus am See", sheet, StringComparison.Ordinal);

        var housePage = Render<ObjectPage>(p => p.Add(x => x.Id, house.Id));
        housePage.WaitForAssertion(() => Assert.Contains("Baufinanzierung", housePage.Find("[data-testid=datasheet]").TextContent, StringComparison.Ordinal));
    }

    [Fact]
    public async Task AreaListAndSearchShowEntries()
    {
        await _browser.Vault.CreateAsync("domain", new VaultPayload { Title = "familie-beispiel.de", Fields = { ["domainName"] = "familie-beispiel.de", ["registrar"] = "INWX" } });

        var area = Render<AreaPage>(p => p.Add(x => x.Number, 8));
        area.WaitForAssertion(() => Assert.Contains("familie-beispiel.de", area.Find("[data-testid=objects-table]").TextContent, StringComparison.Ordinal));
        Assert.NotNull(area.Find("[data-testid=select-mode]"));

        var search = Render<Search>();
        search.WaitForAssertion(() => Assert.NotNull(search.Find("input[data-testid=search-input]")));
        search.Find("input[data-testid=search-input]").Input("inwx");
        search.WaitForAssertion(() => Assert.Contains("familie-beispiel.de", search.Find("[data-testid=search-results]").TextContent, StringComparison.Ordinal));
    }

    [Fact]
    public async Task TrashListsDeletedEntriesAndRestoresThem()
    {
        var item = await _browser.Vault.CreateAsync("otherWish", new VaultPayload { Title = "Baum pflanzen", Fields = { ["wishText"] = "Eine Linde." } });
        await _browser.Vault.DeleteAsync(item.Id);

        var page = Render<Trash>();
        page.WaitForAssertion(() => Assert.Contains("Baum pflanzen", page.Find("[data-testid=trash-table]").TextContent, StringComparison.Ordinal));
        page.FindAll("[data-testid=restore]")[0].Click();
        page.WaitForAssertion(() => Assert.False(_browser.Vault.Find(item.Id)!.IsDeleted));
    }

    [Fact]
    public void PrintViewRendersSelectedAreas()
    {
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("export/print?areas=2");

        var page = Render<PrintView>();

        page.WaitForAssertion(() => Assert.Contains("Notfallordner", page.Find("[data-testid=print-view]").TextContent, StringComparison.Ordinal));
    }
}
