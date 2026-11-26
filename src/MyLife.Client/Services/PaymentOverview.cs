using MyLife.Shared.Vault;

namespace MyLife.Client.Services;

/// <summary>
/// Eine Zahlung aus einem Eintrag: Betrag wie eingetragen, Rhythmus (Wert der Auswahlliste <c>frequency</c>) und
/// das belastete Konto. Je Monat und je Jahr sind nur bei regelmäßigem Rhythmus bekannt.
/// </summary>
public sealed record Payment(VaultItem Item, decimal Amount, string? Frequency, Guid? AccountId)
{
    /// <summary>Zahlungen je Jahr; null bei „einmalig“ und ohne Rhythmus.</summary>
    public int? TimesPerYear => Frequency switch
    {
        "monthly" => 12,
        "quarterly" => 4,
        "halfYearly" => 2,
        "yearly" => 1,
        _ => null,
    };

    public bool IsOnce => Frequency == "once";

    public bool HasNoFrequency => Frequency is null;

    public decimal? Yearly => TimesPerYear * Amount;

    public decimal? Monthly => Yearly / 12;
}

/// <summary>Summen einer Gruppe von Zahlungen. Je Monat wird aus der Jahressumme gerechnet, damit sich Rundungen nicht addieren.</summary>
public sealed record PaymentTotals(int Count, decimal Yearly, decimal Once, int WithoutFrequency)
{
    public decimal Monthly => Yearly / 12;

    public static PaymentTotals Of(IEnumerable<Payment> payments)
    {
        var list = payments.ToList();
        return new(list.Count, list.Sum(p => p.Yearly ?? 0), list.Where(p => p.IsOnce).Sum(p => p.Amount), list.Count(p => p.HasNoFrequency));
    }
}

/// <summary>Zahlungen eines Bereichs mit ihren Summen.</summary>
public sealed record PaymentArea(int Area, IReadOnlyList<Payment> Payments)
{
    public PaymentTotals Totals { get; } = PaymentTotals.Of(Payments);
}

/// <summary>
/// Übersicht „Zahlungen“ (Entscheidung 142): alle Zahlungen, die laufen, über alle Bereiche. Welche Felder eine Zahlung
/// sind, legt das Schema fest (<see cref="ObjectTypeDefinition.Payment"/>). Beendete Einträge, Einträge ohne Betrag
/// und Kredite, die mir geschuldet werden, zählen nicht.
/// </summary>
public sealed class PaymentOverview(VaultService vault)
{
    public IReadOnlyList<Payment> All() =>
        [.. vault.Current.Select(From).OfType<Payment>().OrderBy(p => p.Item.Area).ThenBy(p => p.Item.Title, StringComparer.CurrentCultureIgnoreCase)];

    public IReadOnlyList<PaymentArea> ByArea() => ByArea(All());

    public static IReadOnlyList<PaymentArea> ByArea(IEnumerable<Payment> payments) =>
        [.. payments.GroupBy(p => p.Item.Area).OrderBy(g => g.Key).Select(g => new PaymentArea(g.Key, [.. g]))];

    /// <summary>Die Zahlung eines Eintrags oder null, wenn er keine beschreibt.</summary>
    public static Payment? From(VaultItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item is not { IsDeleted: false, Status: ObjectStatus.Active } || item.Type.Payment is not { } definition)
        {
            return null;
        }

        if (definition.When is { } condition && !condition.Values.Contains(item.Payload.Get(condition.FieldKey) ?? string.Empty))
        {
            return null;
        }

        if (!FieldValues.TryNumber(item.Payload.Get(definition.AmountField), out var amount) || amount <= 0)
        {
            return null;
        }

        var frequency = definition.FixedFrequency ?? (definition.FrequencyField is { } field ? item.Payload.Get(field) : null);
        var account = definition.AccountField is { } accountField && Guid.TryParse(item.Payload.Get(accountField), out var id) ? id : (Guid?)null;
        return new Payment(item, amount, string.IsNullOrEmpty(frequency) ? null : frequency, account);
    }
}
