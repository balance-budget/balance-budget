using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Balance.Services.JournalEntries;

namespace Balance.Tests.Validation;

internal sealed class JournalEntryValidatorTests
{
    private static readonly CurrencyCode Eur = new("EUR");
    private static readonly CurrencyCode Usd = new("USD");

    [Test]
    public async Task Single_line_entry_fails_TooFewLines()
    {
        var lines = new List<JournalLineDraft> { new(100, Eur) };

        var result = JournalEntryValidator.Validate(lines);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsTypeOf<InvariantError>();
        var error = (InvariantError)result.Error!;
        await Assert.That(error.Code).IsEqualTo(ErrorCodes.JournalTooFewLines);
        await Assert.That(error.Message).Contains(JournalEntryValidator.TooFewLinesMessage);
    }

    [Test]
    public async Task Empty_entry_fails_TooFewLines()
    {
        var lines = new List<JournalLineDraft>();

        var result = JournalEntryValidator.Validate(lines);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsTypeOf<InvariantError>();
        var error = (InvariantError)result.Error!;
        await Assert.That(error.Code).IsEqualTo(ErrorCodes.JournalTooFewLines);
        await Assert.That(error.Message).Contains(JournalEntryValidator.TooFewLinesMessage);
    }

    [Test]
    public async Task Two_line_balanced_entry_succeeds()
    {
        var lines = new List<JournalLineDraft> { new(4000, Eur), new(-4000, Eur) };

        var result = JournalEntryValidator.Validate(lines);

        await Assert.That(result.IsSuccess).IsTrue();
    }

    [Test]
    public async Task Two_line_unbalanced_entry_fails_Unbalanced()
    {
        var lines = new List<JournalLineDraft> { new(4000, Eur), new(-3000, Eur) };

        var result = JournalEntryValidator.Validate(lines);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsTypeOf<InvariantError>();
        var error = (InvariantError)result.Error!;
        await Assert.That(error.Code).IsEqualTo(ErrorCodes.JournalUnbalanced);
        await Assert.That(error.Message).Contains(JournalEntryValidator.UnbalancedMessage);
    }

    [Test]
    public async Task Three_line_balanced_split_entry_succeeds()
    {
        var lines = new List<JournalLineDraft> { new(6000, Eur), new(4000, Eur), new(-10000, Eur) };

        var result = JournalEntryValidator.Validate(lines);

        await Assert.That(result.IsSuccess).IsTrue();
    }

    [Test]
    public async Task Currency_mismatch_fails_CurrencyMismatch()
    {
        var lines = new List<JournalLineDraft> { new(4000, Eur), new(-4000, Usd) };

        var result = JournalEntryValidator.Validate(lines);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsTypeOf<InvariantError>();
        var error = (InvariantError)result.Error!;
        await Assert.That(error.Code).IsEqualTo(ErrorCodes.JournalCurrencyMismatch);
        await Assert.That(error.Message).Contains(JournalEntryValidator.CurrencyMismatchMessage);
    }

    [Test]
    public async Task Zero_amount_line_fails_ZeroAmount()
    {
        var lines = new List<JournalLineDraft> { new(4000, Eur), new(0, Eur), new(-4000, Eur) };

        var result = JournalEntryValidator.Validate(lines);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsTypeOf<InvariantError>();
        var error = (InvariantError)result.Error!;
        await Assert.That(error.Code).IsEqualTo(ErrorCodes.JournalZeroAmountLine);
        await Assert.That(error.Message).Contains(JournalEntryValidator.ZeroAmountLineMessage);
    }

    [Test]
    public async Task All_debits_two_lines_fails_Unbalanced()
    {
        var lines = new List<JournalLineDraft> { new(4000, Eur), new(4000, Eur) };

        var result = JournalEntryValidator.Validate(lines);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error).IsTypeOf<InvariantError>();
        var error = (InvariantError)result.Error!;
        await Assert.That(error.Code).IsEqualTo(ErrorCodes.JournalUnbalanced);
        await Assert.That(error.Message).Contains(JournalEntryValidator.UnbalancedMessage);
    }

    [Test]
    public async Task Null_lines_throws_ArgumentNullException()
    {
        await Assert
            .That(() => JournalEntryValidator.Validate(null!))
            .Throws<ArgumentNullException>();
    }
}
