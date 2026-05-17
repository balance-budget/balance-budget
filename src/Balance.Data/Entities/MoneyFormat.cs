using System.Globalization;
using Balance.Data.Exceptions;

namespace Balance.Data.Entities;

/// <summary>
/// Pure formatting and parsing for Money values expressed as minor units against a given scale.
/// No DI dependencies; lookup of the scale is the caller's responsibility.
/// </summary>
internal static class MoneyFormat
{
    public static string Format(long amount, int minorUnitScale, IFormatProvider? formatProvider)
    {
        EnsureNonNegativeScale(minorUnitScale);

        formatProvider ??= CultureInfo.InvariantCulture;
        if (minorUnitScale == 0)
        {
            return amount.ToString(formatProvider);
        }

        var info = NumberFormatInfo.GetInstance(formatProvider);
        var separator = info.NumberDecimalSeparator;

        var isNegative = amount < 0;
        var absolute = isNegative ? unchecked((ulong)-amount) : (ulong)amount;
        var asString = absolute.ToString(CultureInfo.InvariantCulture);
        if (asString.Length <= minorUnitScale)
        {
            asString = asString.PadLeft(minorUnitScale + 1, '0');
        }

        var integerPart = asString[..^minorUnitScale];
        var fractionalPart = asString[^minorUnitScale..];
        var sign = isNegative ? "-" : string.Empty;
        return sign + integerPart + separator + fractionalPart;
    }

    public static long ParseMinorUnits(
        string text,
        int minorUnitScale,
        IFormatProvider? formatProvider
    )
    {
        ArgumentNullException.ThrowIfNull(text);
        EnsureNonNegativeScale(minorUnitScale);

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

        if (fractionalPart.Length > minorUnitScale)
        {
            throw new DomainException(
                DomainExceptionKind.Validation,
                $"'{text}' has more fractional digits than the currency allows ({minorUnitScale})."
            );
        }

        var paddedFractional = fractionalPart.PadRight(minorUnitScale, '0');
        var combined =
            (integerPart.Length == 0 ? "0" : integerPart)
            + (minorUnitScale == 0 ? string.Empty : paddedFractional);

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

    private static void EnsureNonNegativeScale(int minorUnitScale)
    {
        if (minorUnitScale < 0)
        {
            throw new DomainException(
                DomainExceptionKind.Invariant,
                $"MinorUnitScale must be non-negative; got {minorUnitScale}."
            );
        }
    }
}
