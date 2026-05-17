using Balance.Data.Entities.Ids;
using Balance.Data.Exceptions;

namespace Balance.Data.Entities;

/// <summary>
/// A value object pairing an integer amount of minor units (cents, satoshi, etc.) with a Currency.
/// Same-currency arithmetic is type-checked; cross-currency arithmetic throws DomainException.
/// Human-readable formatting/parsing lives in <see cref="MoneyExtensions"/> and requires a Currency.
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
}
