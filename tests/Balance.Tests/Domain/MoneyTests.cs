using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Data.Exceptions;

namespace Balance.Tests.Domain;

internal sealed class MoneyTests
{
    private static readonly CurrencyCode Eur = new("EUR");
    private static readonly CurrencyCode Usd = new("USD");
    private static readonly CurrencyCode Jpy = new("JPY");
    private static readonly CurrencyCode Btc = new("BTC");

    [Test]
    public async Task Equality_uses_amount_and_currency()
    {
        var a = new Money(100, Eur);
        var b = new Money(100, Eur);
        var c = new Money(100, Usd);
        var d = new Money(101, Eur);

        await Assert.That(a).IsEqualTo(b);
        await Assert.That(a == b).IsTrue();
        await Assert.That(a.GetHashCode()).IsEqualTo(b.GetHashCode());
        await Assert.That(a).IsNotEqualTo(c);
        await Assert.That(a).IsNotEqualTo(d);
    }

    [Test]
    public async Task Add_same_currency_sums_amount()
    {
        var sum = new Money(150, Eur) + new Money(75, Eur);

        await Assert.That(sum).IsEqualTo(new Money(225, Eur));
    }

    [Test]
    public async Task Subtract_same_currency_subtracts_amount()
    {
        var diff = new Money(150, Eur) - new Money(75, Eur);

        await Assert.That(diff).IsEqualTo(new Money(75, Eur));
    }

    [Test]
    public async Task Subtract_can_produce_negative_result()
    {
        var diff = new Money(50, Eur) - new Money(125, Eur);

        await Assert.That(diff).IsEqualTo(new Money(-75, Eur));
    }

    [Test]
    public async Task UnaryMinus_negates_amount()
    {
        var negated = -new Money(250, Eur);

        await Assert.That(negated).IsEqualTo(new Money(-250, Eur));
    }

    [Test]
    public async Task UnaryMinus_on_negative_returns_positive()
    {
        var negated = -new Money(-250, Eur);

        await Assert.That(negated).IsEqualTo(new Money(250, Eur));
    }

    [Test]
    public async Task UnaryMinus_on_zero_returns_zero()
    {
        var negated = -new Money(0, Jpy);

        await Assert.That(negated).IsEqualTo(new Money(0, Jpy));
    }

    [Test]
    public async Task ScalarMultiply_scales_amount()
    {
        var scaled = new Money(125, Eur) * 4L;

        await Assert.That(scaled).IsEqualTo(new Money(500, Eur));
    }

    [Test]
    public async Task ScalarMultiply_is_commutative()
    {
        var fromLeft = new Money(125, Eur) * 4L;
        var fromRight = 4L * new Money(125, Eur);

        await Assert.That(fromLeft).IsEqualTo(fromRight);
    }

    [Test]
    public async Task ScalarMultiply_by_zero_yields_zero()
    {
        var scaled = new Money(125, Eur) * 0L;

        await Assert.That(scaled).IsEqualTo(new Money(0, Eur));
    }

    [Test]
    public async Task ScalarMultiply_by_negative_inverts_sign()
    {
        var scaled = new Money(125, Eur) * -3L;

        await Assert.That(scaled).IsEqualTo(new Money(-375, Eur));
    }

    [Test]
    public async Task Add_cross_currency_throws_DomainException()
    {
        var act = () => _ = new Money(100, Eur) + new Money(100, Usd);

        var ex = await Assert.That(act).Throws<DomainException>();
        await Assert.That(ex!.Kind).IsEqualTo(DomainExceptionKind.Invariant);
    }

    [Test]
    public async Task Subtract_cross_currency_throws_DomainException()
    {
        var act = () => _ = new Money(100, Eur) - new Money(100, Usd);

        var ex = await Assert.That(act).Throws<DomainException>();
        await Assert.That(ex!.Kind).IsEqualTo(DomainExceptionKind.Invariant);
    }

    [Test]
    public async Task Add_overflow_throws()
    {
        var act = () => _ = new Money(long.MaxValue, Eur) + new Money(1, Eur);

        await Assert.That(act).Throws<OverflowException>();
    }

    [Test]
    public async Task Subtract_overflow_throws()
    {
        var act = () => _ = new Money(long.MinValue, Eur) - new Money(1, Eur);

        await Assert.That(act).Throws<OverflowException>();
    }

    [Test]
    public async Task UnaryMinus_long_min_overflows()
    {
        var act = () => _ = -new Money(long.MinValue, Eur);

        await Assert.That(act).Throws<OverflowException>();
    }

    [Test]
    public async Task ScalarMultiply_overflow_throws()
    {
        var act = () => _ = new Money(long.MaxValue, Eur) * 2L;

        await Assert.That(act).Throws<OverflowException>();
    }

    [Test]
    public async Task Zero_factory_returns_zero_for_currency()
    {
        var zero = Money.Zero(Btc);

        await Assert.That(zero).IsEqualTo(new Money(0, Btc));
    }

    [Test]
    public async Task IsZero_returns_true_for_zero_amount()
    {
        await Assert.That(new Money(0, Eur).IsZero).IsTrue();
        await Assert.That(new Money(1, Eur).IsZero).IsFalse();
    }

    [Test]
    public async Task Format_uses_minor_unit_scale_for_eur()
    {
        var lookup = new FakeCurrencyLookup(("EUR", 2));

        var formatted = new Money(12345, Eur).Format(
            lookup,
            System.Globalization.CultureInfo.InvariantCulture
        );

        await Assert.That(formatted).IsEqualTo("123.45 EUR");
    }

    [Test]
    public async Task Format_handles_zero_scale_jpy()
    {
        var lookup = new FakeCurrencyLookup(("JPY", 0));

        var formatted = new Money(7500, Jpy).Format(
            lookup,
            System.Globalization.CultureInfo.InvariantCulture
        );

        await Assert.That(formatted).IsEqualTo("7500 JPY");
    }

    [Test]
    public async Task Format_handles_large_scale_btc()
    {
        var lookup = new FakeCurrencyLookup(("BTC", 8));

        var formatted = new Money(123_456_789, Btc).Format(
            lookup,
            System.Globalization.CultureInfo.InvariantCulture
        );

        await Assert.That(formatted).IsEqualTo("1.23456789 BTC");
    }

    [Test]
    public async Task Format_handles_negative()
    {
        var lookup = new FakeCurrencyLookup(("EUR", 2));

        var formatted = new Money(-12345, Eur).Format(
            lookup,
            System.Globalization.CultureInfo.InvariantCulture
        );

        await Assert.That(formatted).IsEqualTo("-123.45 EUR");
    }

    [Test]
    public async Task Parse_uses_minor_unit_scale_for_eur()
    {
        var lookup = new FakeCurrencyLookup(("EUR", 2));

        var parsed = Money.Parse(
            "123.45",
            Eur,
            lookup,
            System.Globalization.CultureInfo.InvariantCulture
        );

        await Assert.That(parsed).IsEqualTo(new Money(12345, Eur));
    }

    [Test]
    public async Task Parse_handles_zero_scale_jpy()
    {
        var lookup = new FakeCurrencyLookup(("JPY", 0));

        var parsed = Money.Parse(
            "7500",
            Jpy,
            lookup,
            System.Globalization.CultureInfo.InvariantCulture
        );

        await Assert.That(parsed).IsEqualTo(new Money(7500, Jpy));
    }

    [Test]
    public async Task Parse_handles_large_scale_btc()
    {
        var lookup = new FakeCurrencyLookup(("BTC", 8));

        var parsed = Money.Parse(
            "1.23456789",
            Btc,
            lookup,
            System.Globalization.CultureInfo.InvariantCulture
        );

        await Assert.That(parsed).IsEqualTo(new Money(123_456_789, Btc));
    }

    [Test]
    public async Task Parse_throws_when_currency_is_unknown()
    {
        var lookup = new FakeCurrencyLookup();
        var act = () =>
            _ = Money.Parse("1", Eur, lookup, System.Globalization.CultureInfo.InvariantCulture);

        await Assert.That(act).Throws<DomainException>();
    }

    [Test]
    public async Task Parse_round_trips_through_Format_for_eur()
    {
        var lookup = new FakeCurrencyLookup(("EUR", 2));
        var original = new Money(-9876543, Eur);

        var text = original.Format(lookup, System.Globalization.CultureInfo.InvariantCulture);
        var parsed = Money.Parse(
            text.Replace(" EUR", "", StringComparison.Ordinal),
            Eur,
            lookup,
            System.Globalization.CultureInfo.InvariantCulture
        );

        await Assert.That(parsed).IsEqualTo(original);
    }

    private sealed class FakeCurrencyLookup : Balance.Data.Currencies.ICurrencyLookup
    {
        private readonly Dictionary<CurrencyCode, Balance.Data.Entities.Currency> _byCode;

        public FakeCurrencyLookup(params (string Code, int Scale)[] currencies)
        {
            _byCode = currencies.ToDictionary(
                c => new CurrencyCode(c.Code),
                c => new Balance.Data.Entities.Currency
                {
                    Code = new CurrencyCode(c.Code),
                    Name = c.Code,
                    MinorUnitScale = c.Scale,
                }
            );
        }

        public Balance.Data.Entities.Currency GetByCode(CurrencyCode code) =>
            TryGetByCode(code)
            ?? throw new DomainException(
                DomainExceptionKind.NotFound,
                $"Currency '{code.Value}' is not defined."
            );

        public Balance.Data.Entities.Currency? TryGetByCode(CurrencyCode code) =>
            _byCode.GetValueOrDefault(code);
    }
}
