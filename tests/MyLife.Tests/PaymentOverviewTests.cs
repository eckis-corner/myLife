using MyLife.Client.Services;
using MyLife.Shared.Vault;

namespace MyLife.Tests;

/// <summary>Übersicht „Zahlungen“ (Entscheidung 142): was zählt, wie umgerechnet und wie summiert wird.</summary>
public sealed class PaymentOverviewTests
{
    private static VaultItem Item(string type, Dictionary<string, string> fields, ObjectStatus status = ObjectStatus.Active)
    {
        var definition = ObjectCatalog.Find(type)!;
        var payload = new VaultPayload { Title = type };
        foreach (var (key, value) in fields)
        {
            payload.Fields[key] = value;
        }

        return new VaultItem { Id = Guid.NewGuid(), Area = definition.Area, Type = definition, Payload = payload, Status = status };
    }

    [Fact]
    public void EveryPaymentPointsAtFieldsOfTheRightType()
    {
        var types = ObjectCatalog.Types.Where(t => t.Payment is not null).ToList();
        Assert.Equal(["card", "loan", "insurance", "contract"], types.Select(t => t.Key));
        foreach (var type in types)
        {
            var payment = type.Payment!;
            Assert.Equal(FieldType.Money, type.Field(payment.AmountField)?.Type);
            Assert.True(payment.FixedFrequency is not null ^ payment.FrequencyField is not null);
            if (payment.FrequencyField is { } frequency)
            {
                Assert.Equal("frequency", type.Field(frequency)?.OptionSet);
            }
            else
            {
                Assert.Contains(payment.FixedFrequency!, ObjectCatalog.OptionSets["frequency"]);
            }

            Assert.Equal(FieldType.Reference, type.Field(payment.AccountField!)?.Type);
        }
    }

    [Theory]
    [InlineData("monthly", 12)]
    [InlineData("quarterly", 4)]
    [InlineData("halfYearly", 2)]
    [InlineData("yearly", 1)]
    public void RegularPaymentsAreConvertedPerYearAndMonth(string frequency, int times)
    {
        var payment = PaymentOverview.From(Item("contract", new() { ["cost"] = "30", ["frequency"] = frequency }))!;
        Assert.Equal(30m * times, payment.Yearly);
        Assert.Equal(30m * times / 12, payment.Monthly);
    }

    [Fact]
    public void OneOffAndMissingFrequencyStayOutOfTheTotals()
    {
        Payment[] payments =
        [
            PaymentOverview.From(Item("contract", new() { ["cost"] = "10", ["frequency"] = "monthly" }))!,
            PaymentOverview.From(Item("insurance", new() { ["premium"] = "100", ["frequency"] = "quarterly" }))!,
            PaymentOverview.From(Item("contract", new() { ["cost"] = "250", ["frequency"] = "once" }))!,
            PaymentOverview.From(Item("insurance", new() { ["premium"] = "99" }))!,
        ];

        Assert.True(payments[3].HasNoFrequency);
        Assert.Null(payments[3].Monthly);
        var totals = PaymentTotals.Of(payments);
        Assert.Equal(4, totals.Count);
        Assert.Equal(520m, totals.Yearly);
        Assert.Equal(520m / 12, totals.Monthly);
        Assert.Equal(250m, totals.Once);
        Assert.Equal(1, totals.WithoutFrequency);
    }

    [Fact]
    public void TheCardFeeIsYearlyAndTheAccountIsTaken()
    {
        var account = Guid.NewGuid();
        var payment = PaymentOverview.From(Item("card", new() { ["cardKind"] = "visa", ["annualFee"] = "39", ["referenceAccount"] = account.ToString() }))!;
        Assert.Equal("yearly", payment.Frequency);
        Assert.Equal(39m, payment.Yearly);
        Assert.Equal(account, payment.AccountId);
    }

    [Fact]
    public void OnlyCurrentOutgoingPaymentsWithAnAmountCount()
    {
        Assert.NotNull(PaymentOverview.From(Item("loan", new() { ["loanDirection"] = "iOwe", ["installment"] = "400", ["frequency"] = "monthly" })));
        Assert.Null(PaymentOverview.From(Item("loan", new() { ["loanDirection"] = "owedToMe", ["installment"] = "400", ["frequency"] = "monthly" })));
        Assert.Null(PaymentOverview.From(Item("contract", new() { ["cost"] = "10", ["frequency"] = "monthly" }, ObjectStatus.Ended)));
        Assert.Null(PaymentOverview.From(Item("contract", new() { ["frequency"] = "monthly" })));
        Assert.Null(PaymentOverview.From(Item("contract", new() { ["cost"] = "0", ["frequency"] = "monthly" })));
        Assert.Null(PaymentOverview.From(Item("bankAccount", new() { ["bank"] = "Sparkasse" })));
    }

    [Fact]
    public void PaymentsAreGroupedByAreaInAreaOrder()
    {
        Payment[] payments =
        [
            PaymentOverview.From(Item("contract", new() { ["cost"] = "10", ["frequency"] = "monthly" }))!,
            PaymentOverview.From(Item("card", new() { ["annualFee"] = "24" }))!,
            PaymentOverview.From(Item("contract", new() { ["cost"] = "5", ["frequency"] = "monthly" }))!,
        ];

        var areas = PaymentOverview.ByArea(payments);
        Assert.Equal([2, 5], areas.Select(a => a.Area));
        Assert.Equal(2m, areas[0].Totals.Monthly);
        Assert.Equal(180m, areas[1].Totals.Yearly);
    }
}
