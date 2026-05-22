using Balance.Data.Entities;
using Balance.Data.Entities.Ids;

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
    public async Task Add_cross_currency_throws_InvalidOperation()
    {
        var act = () => _ = new Money(100, Eur) + new Money(100, Usd);

        await Assert.That(act).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Subtract_cross_currency_throws_InvalidOperation()
    {
        var act = () => _ = new Money(100, Eur) - new Money(100, Usd);

        await Assert.That(act).Throws<InvalidOperationException>();
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
}
