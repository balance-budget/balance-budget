using System.Globalization;
using Balance.Data.Entities;
using Balance.Data.Exceptions;

namespace Balance.Tests.Domain;

internal sealed class MoneyFormatTests
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    [Test]
    public async Task Format_scale_zero_emits_no_separator()
    {
        var result = MoneyFormat.Format(7500, 0, Invariant);

        await Assert.That(result).IsEqualTo("7500");
    }

    [Test]
    public async Task Format_scale_two_inserts_decimal_separator()
    {
        var result = MoneyFormat.Format(12345, 2, Invariant);

        await Assert.That(result).IsEqualTo("123.45");
    }

    [Test]
    public async Task Format_pads_with_leading_zeros_when_amount_shorter_than_scale()
    {
        var result = MoneyFormat.Format(5, 2, Invariant);

        await Assert.That(result).IsEqualTo("0.05");
    }

    [Test]
    public async Task Format_handles_large_scale_btc()
    {
        var result = MoneyFormat.Format(123_456_789, 8, Invariant);

        await Assert.That(result).IsEqualTo("1.23456789");
    }

    [Test]
    public async Task Format_handles_negative_amount()
    {
        var result = MoneyFormat.Format(-12345, 2, Invariant);

        await Assert.That(result).IsEqualTo("-123.45");
    }

    [Test]
    public async Task Format_uses_culture_decimal_separator()
    {
        var dutch = CultureInfo.GetCultureInfo("nl-NL");

        var result = MoneyFormat.Format(12345, 2, dutch);

        await Assert.That(result).IsEqualTo("123,45");
    }

    [Test]
    public async Task Format_defaults_to_invariant_culture_when_provider_null()
    {
        var result = MoneyFormat.Format(12345, 2, null);

        await Assert.That(result).IsEqualTo("123.45");
    }

    [Test]
    public async Task Format_throws_when_scale_is_negative()
    {
        var act = () => _ = MoneyFormat.Format(100, -1, Invariant);

        var ex = await Assert.That(act).Throws<DomainException>();
        await Assert.That(ex!.Kind).IsEqualTo(DomainExceptionKind.Invariant);
    }

    [Test]
    public async Task ParseMinorUnits_scale_zero_returns_integer()
    {
        var result = MoneyFormat.ParseMinorUnits("7500", 0, Invariant);

        await Assert.That(result).IsEqualTo(7500L);
    }

    [Test]
    public async Task ParseMinorUnits_scale_two_returns_minor_units()
    {
        var result = MoneyFormat.ParseMinorUnits("123.45", 2, Invariant);

        await Assert.That(result).IsEqualTo(12345L);
    }

    [Test]
    public async Task ParseMinorUnits_pads_short_fractional_part()
    {
        var result = MoneyFormat.ParseMinorUnits("1.2", 2, Invariant);

        await Assert.That(result).IsEqualTo(120L);
    }

    [Test]
    public async Task ParseMinorUnits_handles_negative()
    {
        var result = MoneyFormat.ParseMinorUnits("-123.45", 2, Invariant);

        await Assert.That(result).IsEqualTo(-12345L);
    }

    [Test]
    public async Task ParseMinorUnits_handles_explicit_positive_sign()
    {
        var result = MoneyFormat.ParseMinorUnits("+123.45", 2, Invariant);

        await Assert.That(result).IsEqualTo(12345L);
    }

    [Test]
    public async Task ParseMinorUnits_uses_culture_decimal_separator()
    {
        var dutch = CultureInfo.GetCultureInfo("nl-NL");

        var result = MoneyFormat.ParseMinorUnits("123,45", 2, dutch);

        await Assert.That(result).IsEqualTo(12345L);
    }

    [Test]
    public async Task ParseMinorUnits_defaults_to_invariant_culture_when_provider_null()
    {
        var result = MoneyFormat.ParseMinorUnits("123.45", 2, null);

        await Assert.That(result).IsEqualTo(12345L);
    }

    [Test]
    public async Task ParseMinorUnits_throws_when_empty()
    {
        var act = () => _ = MoneyFormat.ParseMinorUnits("   ", 2, Invariant);

        var ex = await Assert.That(act).Throws<DomainException>();
        await Assert.That(ex!.Kind).IsEqualTo(DomainExceptionKind.Validation);
    }

    [Test]
    public async Task ParseMinorUnits_throws_when_fractional_exceeds_scale()
    {
        var act = () => _ = MoneyFormat.ParseMinorUnits("1.234", 2, Invariant);

        var ex = await Assert.That(act).Throws<DomainException>();
        await Assert.That(ex!.Kind).IsEqualTo(DomainExceptionKind.Validation);
    }

    [Test]
    public async Task ParseMinorUnits_throws_when_text_is_invalid()
    {
        var act = () => _ = MoneyFormat.ParseMinorUnits("abc", 2, Invariant);

        var ex = await Assert.That(act).Throws<DomainException>();
        await Assert.That(ex!.Kind).IsEqualTo(DomainExceptionKind.Validation);
    }

    [Test]
    public async Task ParseMinorUnits_throws_when_scale_is_negative()
    {
        var act = () => _ = MoneyFormat.ParseMinorUnits("1.0", -1, Invariant);

        var ex = await Assert.That(act).Throws<DomainException>();
        await Assert.That(ex!.Kind).IsEqualTo(DomainExceptionKind.Invariant);
    }

    [Test]
    public async Task Format_then_ParseMinorUnits_round_trips()
    {
        var text = MoneyFormat.Format(-9_876_543, 2, Invariant);
        var parsed = MoneyFormat.ParseMinorUnits(text, 2, Invariant);

        await Assert.That(parsed).IsEqualTo(-9_876_543L);
    }
}
