using System.Globalization;
using Balance.Data.Currencies;
using Balance.Data.Entities.Ids;
using Balance.Data.Exceptions;

namespace Balance.Data.Entities;

/// <summary>
/// A value object pairing an integer amount of minor units (cents, satoshi, etc.) with a Currency.
/// Same-currency arithmetic is type-checked; cross-currency arithmetic throws DomainException.
/// </summary>
public readonly record struct Money(long Amount, CurrencyCode CurrencyCode)
{
    public static Money Zero(CurrencyCode currencyCode) => new(0L, currencyCode);

    public bool IsZero => Amount == 0L;

    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(checked(left.Amount + right.Amount), left.CurrencyCode);
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(checked(left.Amount - right.Amount), left.CurrencyCode);
    }

    public static Money operator -(Money money) => new(checked(-money.Amount), money.CurrencyCode);

    public static Money operator *(Money money, long factor) =>
        new(checked(money.Amount * factor), money.CurrencyCode);

    public static Money operator *(long factor, Money money) => money * factor;

    public static Money Add(Money left, Money right) => left + right;

    public static Money Subtract(Money left, Money right) => left - right;

    public static Money Negate(Money money) => -money;

    public static Money Multiply(Money money, long factor) => money * factor;

    public string Format(ICurrencyLookup lookup, IFormatProvider? formatProvider = null)
    {
        ArgumentNullException.ThrowIfNull(lookup);
        var currency = lookup.Get(CurrencyCode);
        return FormatMinorUnits(Amount, currency.MinorUnitScale, formatProvider)
            + " "
            + CurrencyCode.Value;
    }

    public static Money Parse(
        string majorUnits,
        CurrencyCode currencyCode,
        ICurrencyLookup lookup,
        IFormatProvider? formatProvider = null
    )
    {
        ArgumentNullException.ThrowIfNull(majorUnits);
        ArgumentNullException.ThrowIfNull(lookup);
        var currency = lookup.Get(currencyCode);
        var amount = ParseMinorUnits(majorUnits, currency.MinorUnitScale, formatProvider);
        return new Money(amount, currencyCode);
    }

    private static void EnsureSameCurrency(Money left, Money right)
    {
        if (left.CurrencyCode != right.CurrencyCode)
        {
            throw new DomainException(
                DomainExceptionKind.Invariant,
                $"Cannot operate on Money values of different currencies: "
                    + $"{left.CurrencyCode.Value} and {right.CurrencyCode.Value}."
            );
        }
    }

    private static string FormatMinorUnits(long amount, int scale, IFormatProvider? formatProvider)
    {
        if (scale < 0)
        {
            throw new DomainException(
                DomainExceptionKind.Invariant,
                $"MinorUnitScale must be non-negative; got {scale}."
            );
        }

        formatProvider ??= CultureInfo.InvariantCulture;
        if (scale == 0)
        {
            return amount.ToString(formatProvider);
        }

        var info = NumberFormatInfo.GetInstance(formatProvider);
        var separator = info.NumberDecimalSeparator;

        var isNegative = amount < 0;
        var absolute = isNegative ? unchecked((ulong)-amount) : (ulong)amount;
        var asString = absolute.ToString(CultureInfo.InvariantCulture);
        if (asString.Length <= scale)
        {
            asString = asString.PadLeft(scale + 1, '0');
        }

        var integerPart = asString[..^scale];
        var fractionalPart = asString[^scale..];
        var sign = isNegative ? "-" : string.Empty;
        return sign + integerPart + separator + fractionalPart;
    }

    private static long ParseMinorUnits(string text, int scale, IFormatProvider? formatProvider)
    {
        if (scale < 0)
        {
            throw new DomainException(
                DomainExceptionKind.Invariant,
                $"MinorUnitScale must be non-negative; got {scale}."
            );
        }

        formatProvider ??= CultureInfo.InvariantCulture;
        var info = NumberFormatInfo.GetInstance(formatProvider);
        var separator = info.NumberDecimalSeparator;

        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            throw new DomainException(
                DomainExceptionKind.Validation,
                "Money value cannot be empty."
            );
        }

        var isNegative = false;
        var start = 0;
        if (trimmed[0] == '-')
        {
            isNegative = true;
            start = 1;
        }
        else if (trimmed[0] == '+')
        {
            start = 1;
        }

        var body = trimmed[start..];
        var separatorIndex = body.IndexOf(separator, StringComparison.Ordinal);
        string integerPart;
        string fractionalPart;
        if (separatorIndex < 0)
        {
            integerPart = body;
            fractionalPart = string.Empty;
        }
        else
        {
            integerPart = body[..separatorIndex];
            fractionalPart = body[(separatorIndex + separator.Length)..];
        }

        if (integerPart.Length == 0 && fractionalPart.Length == 0)
        {
            throw new DomainException(
                DomainExceptionKind.Validation,
                $"'{text}' is not a valid money value."
            );
        }

        if (fractionalPart.Length > scale)
        {
            throw new DomainException(
                DomainExceptionKind.Validation,
                $"'{text}' has more fractional digits than the currency allows ({scale})."
            );
        }

        var paddedFractional = fractionalPart.PadRight(scale, '0');
        var combined =
            (integerPart.Length == 0 ? "0" : integerPart)
            + (scale == 0 ? string.Empty : paddedFractional);

        if (
            !long.TryParse(combined, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
        )
        {
            throw new DomainException(
                DomainExceptionKind.Validation,
                $"'{text}' is not a valid money value."
            );
        }

        return isNegative ? checked(-value) : value;
    }
}
