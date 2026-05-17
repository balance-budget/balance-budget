using Balance.Data.Entities.Ids;
using Balance.Services.JournalEntries;

namespace Balance.Tests.Validation;

internal sealed class JournalEntryValidatorTests
{
    private static readonly CurrencyCode Eur = new("EUR");
    private static readonly CurrencyCode Usd = new("USD");

    [Test]
    public async Task Single_line_entry_throws_TooFewLines()
    {
        var lines = new List<JournalLineDraft> { new(100, Eur) };

        await Assert
            .That(() => JournalEntryValidator.Validate(lines))
            .Throws<JournalEntryTooFewLinesException>();
    }

    [Test]
    public async Task Empty_entry_throws_TooFewLines()
    {
        var lines = new List<JournalLineDraft>();

        await Assert
            .That(() => JournalEntryValidator.Validate(lines))
            .Throws<JournalEntryTooFewLinesException>();
    }

    [Test]
    public async Task Two_line_balanced_entry_succeeds()
    {
        var lines = new List<JournalLineDraft> { new(4000, Eur), new(-4000, Eur) };

        await Assert.That(() => JournalEntryValidator.Validate(lines)).ThrowsNothing();
    }

    [Test]
    public async Task Two_line_unbalanced_entry_throws_Unbalanced()
    {
        var lines = new List<JournalLineDraft> { new(4000, Eur), new(-3000, Eur) };

        await Assert
            .That(() => JournalEntryValidator.Validate(lines))
            .Throws<JournalEntryUnbalancedException>();
    }

    [Test]
    public async Task Three_line_balanced_split_entry_succeeds()
    {
        var lines = new List<JournalLineDraft> { new(6000, Eur), new(4000, Eur), new(-10000, Eur) };

        await Assert.That(() => JournalEntryValidator.Validate(lines)).ThrowsNothing();
    }

    [Test]
    public async Task Currency_mismatch_throws_CurrencyMismatch()
    {
        var lines = new List<JournalLineDraft> { new(4000, Eur), new(-4000, Usd) };

        await Assert
            .That(() => JournalEntryValidator.Validate(lines))
            .Throws<JournalEntryCurrencyMismatchException>();
    }

    [Test]
    public async Task Zero_amount_line_throws_ZeroAmount()
    {
        var lines = new List<JournalLineDraft> { new(4000, Eur), new(0, Eur), new(-4000, Eur) };

        await Assert
            .That(() => JournalEntryValidator.Validate(lines))
            .Throws<JournalEntryZeroAmountLineException>();
    }

    [Test]
    public async Task All_debits_two_lines_throws_Unbalanced()
    {
        var lines = new List<JournalLineDraft> { new(4000, Eur), new(4000, Eur) };

        await Assert
            .That(() => JournalEntryValidator.Validate(lines))
            .Throws<JournalEntryUnbalancedException>();
    }

    [Test]
    public async Task Null_lines_throws_ArgumentNullException()
    {
        await Assert
            .That(() => JournalEntryValidator.Validate(null!))
            .Throws<ArgumentNullException>();
    }
}
