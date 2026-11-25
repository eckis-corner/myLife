using MyLife.Client.Services;
using MyLife.Shared.Vault;
using MyLife.Tests.Server;

namespace MyLife.Tests.Vault;

/// <summary>Vollständigkeitsanzeige (M3.3): Pflicht- und empfohlene Felder, Wunsch, Ablageort, Ablaufdatum, typische Einträge.</summary>
public sealed class CompletenessTests
{
    private readonly Completeness _completeness = new(VaultBrowser.Text());

    [Fact]
    public void AnEntryIsCompleteWhenRequiredRecommendedFieldsAndTheWishAreFilled()
    {
        var account = Item("bankAccount", new VaultPayload { Title = "Giro", Fields = { ["bank"] = "Sparkasse" } });
        Assert.Equal(["iban", "holder", "powerOfAttorney", "wish"], _completeness.Missing(account).Select(m => m.Key));
        Assert.Contains("Wunsch für die Hinterbliebenen", _completeness.Missing(account).Select(m => m.Label));
        Assert.Equal(0.2, _completeness.Score(account), 3);

        account.Payload.Fields["iban"] = "DE89370400440532013000";
        account.Payload.Fields["holder"] = "Daniel";
        account.Payload.Fields["powerOfAttorney"] = "Vera";
        account.Payload.Wish = "close";
        Assert.Empty(_completeness.Missing(account));
        Assert.Equal(1, _completeness.Score(account));
    }

    [Fact]
    public void CertificatesNeedTheOriginalLocationAndIdsTheirExpiry()
    {
        var will = Item("legalDocument", new VaultPayload { Title = "Testament", Fields = { ["documentKind"] = "will" } });
        Assert.Equal(["originalLocation"], _completeness.Missing(will).Select(m => m.Key));

        var id = Item("idDocument", new VaultPayload { Title = "Ausweis", Fields = { ["idDocumentKind"] = "idCard", ["number"] = "L01X00T47" } });
        Assert.Equal(["expiresOn"], _completeness.Missing(id).Select(m => m.Key));
    }

    [Fact]
    public void AreasCountOnlyActiveEntriesAndSuggestTypicalOnes()
    {
        var will = Item("legalDocument", new VaultPayload { Title = "Testament", OriginalLocation = "Schreibtisch", Fields = { ["documentKind"] = "will" } });
        var ended = Item("legalDocument", new VaultPayload { Title = "Alt", Fields = { ["documentKind"] = "birth" } }, ObjectStatus.Ended);

        var area = _completeness.ForArea(6, [will, ended]);
        Assert.Equal(100, area.Percent);
        Assert.Equal(0, area.Incomplete);
        Assert.Equal(["birth", "livingWill", "healthcareProxy"], area.MissingTypical.Select(t => t.Value));
        Assert.Equal("Urkunde / Dokument: Patientenverfügung", _completeness.Label(area.MissingTypical[1]));
        Assert.Equal("object/new/legalDocument?field=documentKind&value=livingWill", Completeness.NewHref(area.MissingTypical[1]));

        var empty = _completeness.ForArea(3, [will]);
        Assert.True(empty.IsEmpty);
        Assert.Empty(empty.MissingTypical);
        Assert.Null(_completeness.Overall([]));
    }

    [Fact]
    public void RecommendedFieldsAndTypicalEntriesExistInTheCatalog()
    {
        Assert.Contains(ObjectCatalog.Types, t => t.Fields.Any(f => f.Recommended));
        Assert.All(ObjectCatalog.Types.SelectMany(t => t.Fields), f => Assert.False(f.Required && f.Recommended, f.Key));
        foreach (var area in ObjectCatalog.Areas)
        {
            foreach (var typical in area.Typical)
            {
                var type = ObjectCatalog.Find(typical.TypeKey);
                Assert.True(type is not null && type.Area == area.Number, typical.TypeKey);
                if (typical.FieldKey is { } key)
                {
                    Assert.Contains(typical.Value!, ObjectCatalog.OptionSets[type!.Field(key)!.OptionSet!]);
                }
            }
        }
    }

    private static VaultItem Item(string type, VaultPayload payload, ObjectStatus status = ObjectStatus.Active) => new()
    {
        Id = Guid.NewGuid(), Area = ObjectCatalog.Find(type)!.Area, Type = ObjectCatalog.Find(type)!, Payload = payload, Status = status,
    };
}
