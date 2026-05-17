using Balance.Data.Exceptions;

namespace Balance.Data.Entities;

/// <summary>
/// Extensions that bridge <see cref="Money"/> with <see cref="Currency"/> reference data
/// for human-readable formatting and parsing. Callers fetch the Currency once and pass it in.
/// </summary>
public static class MoneyExtensions
{
    public static string Format(
        this Money money,
        Currency currency,
        IFormatProvider? formatProvider = null
    )
    {
        ArgumentNullException.ThrowIfNull(currency);
        EnsureCurrencyMatches(money, currency);
        return MoneyFormat.Format(money.Amount, currency.MinorUnitScale, formatProvider)
            + " "
            + money.CurrencyCode.Value;
    }

    public static Money Parse(
        string text,
        Currency currency,
        IFormatProvider? formatProvider = null
    )
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(currency);
        var amount = MoneyFormat.ParseMinorUnits(text, currency.MinorUnitScale, formatProvider);
        return new Money(amount, currency.Code);
    }

    private static void EnsureCurrencyMatches(Money money, Currency currency)
    {
        if (currency.Code != money.CurrencyCode)
        {
            throw new DomainException(
                DomainExceptionKind.Invariant,
                $"Currency mismatch: Money is {money.CurrencyCode.Value}, "
                    + $"caller supplied {currency.Code.Value}."
            );
        }
    }
}
