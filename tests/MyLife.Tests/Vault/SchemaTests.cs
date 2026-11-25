using MyLife.Shared.Checklist;
using System.Xml.Linq;
using MyLife.Client.Services;
using MyLife.Shared.Vault;

namespace MyLife.Tests.Vault;

/// <summary>Der Objekttyp-Katalog (M1.4) ist in sich stimmig und vollständig übersetzt.</summary>
public sealed class CatalogTests
{
    private static readonly string ResourceDirectory = Path.Combine(ResourceParityTests.FindRepositoryRoot(), "src", "MyLife.Client", "Resources");

    public static TheoryData<string> Files => new() { "SharedResource.resx", "SharedResource.en.resx" };

    [Fact]
    public void KeysAreUniqueAndAreasMatchTheCatalog()
    {
        Assert.Equal(ObjectCatalog.Types.Count, ObjectCatalog.Types.Select(t => t.Key).Distinct().Count());
        Assert.Equal(Enumerable.Range(0, 14), ObjectCatalog.Areas.Select(a => a.Number));
        foreach (var type in ObjectCatalog.Types)
        {
            Assert.True(ObjectCatalog.IsUsableArea(type.Area), type.Key);
            Assert.Equal(type.Fields.Count, type.Fields.Select(f => f.Key).Distinct().Count());
        }

        // Jeder Bereich hat mindestens einen Objekttyp; die Checkliste (seit M3.2) nur die Aufgabe.
        foreach (var area in ObjectCatalog.Areas)
        {
            Assert.True(area.IsAvailable);
            Assert.NotEmpty(ObjectCatalog.TypesIn(area.Number));
        }

        Assert.Equal(["task"], ObjectCatalog.TypesIn(ObjectCatalog.ChecklistArea).Select(t => t.Key));
    }

    [Fact]
    public void TheChecklistTemplateUsesOnlyKnownPhasesAreasAndWishes()
    {
        var areas = ObjectCatalog.OptionSets["checklistArea"];
        Assert.All(areas, key => Assert.True(ObjectCatalog.AreaByKey(key) is { } area && area != ObjectCatalog.ChecklistArea, key));
        Assert.Equal(ChecklistTemplate.Tasks.Count, ChecklistTemplate.Tasks.Select(t => t.Id).Distinct().Count());
        foreach (var task in ChecklistTemplate.Tasks)
        {
            Assert.Contains(task.Phase, ObjectCatalog.OptionSets["checklistPhase"]);
            Assert.True(task.LinkedArea is null || areas.Contains(task.LinkedArea), task.Id);
            Assert.True(task.LinkedWish is null || ObjectCatalog.OptionSets["wish"].Contains(task.LinkedWish), task.Id);
        }

        // Jede Phase kommt vor.
        Assert.Equal(ObjectCatalog.OptionSets["checklistPhase"], ChecklistTemplate.Tasks.Select(t => t.Phase).Distinct());
    }

    [Fact]
    public void FieldDefinitionsAreConsistent()
    {
        foreach (var type in ObjectCatalog.Types)
        {
            foreach (var field in type.Fields)
            {
                var name = $"{type.Key}.{field.Key}";
                if (field.Type == FieldType.Choice)
                {
                    Assert.True(field.OptionSet is not null && ObjectCatalog.OptionSets.ContainsKey(field.OptionSet), name);
                }

                foreach (var target in field.ReferenceTypes)
                {
                    Assert.True(ObjectCatalog.Find(target) is not null, $"{name} → {target}");
                }

                if (field.VisibleWhen is { } condition)
                {
                    var source = type.Field(condition.FieldKey);
                    Assert.True(source is not null, name);
                    var allowed = source!.Type == FieldType.YesNo ? ["true", "false"] : ObjectCatalog.OptionSets[source.OptionSet!];
                    Assert.All(condition.Values, v => Assert.Contains(v, allowed));
                }

                if (field.DefaultValue is not null)
                {
                    Assert.Null(FieldValues.Validate(field, field.DefaultValue));
                }
            }

            Assert.All(type.Wishes, w => Assert.Contains(w, ObjectCatalog.OptionSets["wish"]));
        }
    }

    [Theory]
    [MemberData(nameof(Files))]
    public void EverySchemaTextExists(string file)
    {
        var keys = XDocument.Load(Path.Combine(ResourceDirectory, file)).Root!.Elements("data")
            .Select(d => d.Attribute("name")!.Value).ToHashSet(StringComparer.Ordinal);
        var missing = new List<string>();
        void Require(string key)
        {
            if (!keys.Contains(key))
            {
                missing.Add(key);
            }
        }

        foreach (var area in ObjectCatalog.Areas)
        {
            Require($"Area_{area.Number}");
            Require($"AreaText_{area.Number}");
        }

        foreach (var type in ObjectCatalog.Types)
        {
            Require($"Type_{type.Key}");
            if (type.HasExpiryLabel)
            {
                Require($"Expiry_{type.Key}");
            }

            foreach (var field in type.Fields)
            {
                if (!keys.Contains($"Field_{type.Key}_{field.Key}"))
                {
                    Require($"Field_{field.Key}");
                }

                if (field.HasHint)
                {
                    Require($"FieldHint_{field.Key}");
                }
            }
        }

        foreach (var (set, values) in ObjectCatalog.OptionSets)
        {
            foreach (var value in values)
            {
                Require($"Option_{set}_{value}");
            }
        }

        foreach (var code in new[] { "required", "too-long", "number", "date", "iban", "bic", "email", "url", "phone", "last4", "invalid" })
        {
            Require($"FieldError_{code}");
        }

        foreach (var action in typeof(AuditActions).GetFields().Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!))
        {
            Require($"Audit_{action}");
        }

        foreach (var code in typeof(VaultErrors).GetFields().Concat(typeof(ClientVaultErrors).GetFields()).Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!))
        {
            Require($"VaultError_{code}");
        }

        foreach (var code in new[] { ContentChecks.CardNumber, ContentChecks.Secret, ContentChecks.SeedPhrase })
        {
            Require($"ContentWarning_{code}");
        }

        Require("Expiry_default");
        Assert.Empty(missing);
    }

}

public sealed class FieldValueTests
{
    private static readonly ObjectTypeDefinition BankAccount = ObjectCatalog.Find("bankAccount")!;

    [Theory]
    [InlineData("DE89370400440532013000", true)]
    [InlineData("de89 3704 0044 0532 0130 00", true)]
    [InlineData("GB82WEST12345698765432", true)]
    [InlineData("DE89370400440532013001", false)]
    [InlineData("DE8937040044", false)]
    [InlineData("", false)]
    public void ChecksIbanDigits(string iban, bool valid) => Assert.Equal(valid, FieldValues.IsIban(iban));

    [Fact]
    public void ValidatesByType()
    {
        Assert.Null(FieldValues.Validate(new FieldDefinition("x", FieldType.Money), "1234.50"));
        Assert.Equal("number", FieldValues.Validate(new FieldDefinition("x", FieldType.Money), "1.234,50"));
        Assert.Null(FieldValues.Validate(new FieldDefinition("x", FieldType.Date), "2026-11-24"));
        Assert.Equal("date", FieldValues.Validate(new FieldDefinition("x", FieldType.Date), "24.11.2026"));
        Assert.Null(FieldValues.Validate(new FieldDefinition("x", FieldType.Bic), "COBADEFFXXX"));
        Assert.Equal("bic", FieldValues.Validate(new FieldDefinition("x", FieldType.Bic), "COBA"));
        Assert.Equal("last4", FieldValues.Validate(new FieldDefinition("x", FieldType.Last4), "41111"));
        Assert.Equal("invalid", FieldValues.Validate(BankAccount.Field("accountKind")!, "lottery"));
        Assert.Null(FieldValues.Validate(BankAccount.Field("accountKind")!, "joint"));
        Assert.Equal("too-long", FieldValues.Validate(new FieldDefinition("x", FieldType.Text), new string('a', 501)));
    }

    [Fact]
    public void ValidatesObjectsIncludingCommonRules()
    {
        var payload = new VaultPayload { Title = " ", Fields = { ["iban"] = "DE00123" } };
        var errors = FieldValues.ValidateObject(BankAccount, payload);
        Assert.Equal("required", errors["title"]);
        Assert.Equal("required", errors["bank"]);
        Assert.Equal("iban", errors["iban"]);

        var document = ObjectCatalog.Find("legalDocument")!;
        var will = new VaultPayload { Title = "Testament", Fields = { ["documentKind"] = "will" } };
        Assert.Equal("required", FieldValues.ValidateObject(document, will)["originalLocation"]);
        will.OriginalLocation = "Notar";
        Assert.Empty(FieldValues.ValidateObject(document, will));
    }

    [Fact]
    public void HiddenFieldsAreNotValidated()
    {
        var insurance = ObjectCatalog.Find("insurance")!;
        var payload = new VaultPayload { Title = "Haftpflicht", Fields = { ["insuranceKind"] = "liability", ["insurer"] = "X", ["vehicle"] = "kein-guid" } };

        Assert.False(FieldValues.IsVisible(insurance.Field("vehicle")!, payload));
        Assert.Empty(FieldValues.ValidateObject(insurance, payload));
        payload.Set("insuranceKind", "car");
        Assert.Equal("invalid", FieldValues.ValidateObject(insurance, payload)["vehicle"]);
    }

    [Fact]
    public void PayloadRoundTripsAndRemapsReferences()
    {
        var contact = Guid.NewGuid();
        var account = Guid.NewGuid();
        var loan = ObjectCatalog.Find("loan")!;
        var payload = new VaultPayload { Title = "Kredit", ContactId = contact, Links = [account, Guid.NewGuid()], ExpiresOn = new DateOnly(2030, 1, 31) };
        payload.Set("debitAccount", account.ToString());
        payload.Set("note", "  ");

        var copy = VaultPayload.FromUtf8(payload.ToUtf8());
        Assert.Equal(new DateOnly(2030, 1, 31), copy.ExpiresOn);
        Assert.False(copy.Fields.ContainsKey("note"));
        Assert.Equal([contact, account, payload.Links[1], account], copy.References(loan).ToList());

        var newContact = Guid.NewGuid();
        var newAccount = Guid.NewGuid();
        copy.RemapReferences(loan, new Dictionary<Guid, Guid> { [contact] = newContact, [account] = newAccount });
        Assert.Equal(newContact, copy.ContactId);
        Assert.Equal([newAccount], copy.Links);
        Assert.Equal(newAccount.ToString(), copy.Get("debitAccount"));
    }

    [Fact]
    public void FollowUpIsOptional()
    {
        var type = ObjectCatalog.Find("contract")!;
        var empty = new VaultPayload { Title = "Strom" };
        Assert.False(empty.HasFollowUp);
        Assert.False(empty.IsFollowUpDue(new DateOnly(2026, 11, 24)));
        Assert.DoesNotContain("followUpOn", System.Text.Encoding.UTF8.GetString(empty.ToUtf8()), StringComparison.Ordinal);
        Assert.False(FieldValues.ValidateObject(type, empty).ContainsKey("followUpNote"));

        var payload = new VaultPayload { Title = "Strom", FollowUpOn = new DateOnly(2026, 12, 1), FollowUpNote = "Tarif vergleichen" };
        var copy = VaultPayload.FromUtf8(payload.ToUtf8());
        Assert.Equal(new DateOnly(2026, 12, 1), copy.FollowUpOn);
        Assert.Equal("Tarif vergleichen", copy.FollowUpNote);
        Assert.True(copy.HasFollowUp);
        Assert.False(copy.IsFollowUpDue(new DateOnly(2026, 11, 30)));
        Assert.True(copy.IsFollowUpDue(new DateOnly(2026, 12, 1)));

        Assert.True(new VaultPayload { FollowUpNote = "nur Hinweis" }.HasFollowUp);
        copy.FollowUpNote = new string('x', FieldValues.MaxLongTextLength + 1);
        Assert.Equal("too-long", FieldValues.ValidateObject(type, copy)["followUpNote"]);
    }
}

public sealed class ContentCheckTests
{
    [Theory]
    [InlineData("Karte 4111 1111 1111 1111", ContentChecks.CardNumber)]
    [InlineData("Nummer 4111-1111-1111-1111", ContentChecks.CardNumber)]
    [InlineData("Die PIN ist 1234", ContentChecks.Secret)]
    [InlineData("Passwort: geheim123", ContentChecks.Secret)]
    [InlineData("abandon ability able about above absent absorb abstract absurd abuse access accident", ContentChecks.SeedPhrase)]
    public void WarnsAboutSecrets(string text, string expected) => Assert.Contains(expected, ContentChecks.Check(text));

    [Theory]
    [InlineData("IBAN DE89 3704 0044 0532 0130 00")]
    [InlineData("Telefon 0049 30 1234567")]
    [InlineData("Zugang im Passwortmanager unter „Bank“")]
    [InlineData("Master-Passwort liegt im Bankschließfach")]
    [InlineData("4111 1111 1111 1112")]
    [InlineData("The quick brown fox jumps over the lazy dog every single day of the week")]
    public void StaysQuietForNormalText(string text) => Assert.Empty(ContentChecks.Check(text));

    [Fact]
    public void ChecksAllFreeTextsOfAnObject()
    {
        var card = ObjectCatalog.Find("card")!;
        var payload = new VaultPayload { Title = "Visa", Fields = { ["cardKind"] = "visa", ["last4"] = "1111" } };
        Assert.Empty(ContentChecks.Check(card, payload));

        payload.CustomFields.Add(new CustomField("Nummer", "4111111111111111"));
        Assert.Equal([ContentChecks.CardNumber], ContentChecks.Check(card, payload));
    }
}
