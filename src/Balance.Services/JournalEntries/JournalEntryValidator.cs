using Balance.Data.Entities.Ids;

namespace Balance.Services.JournalEntries;

/// <summary>
/// Pure validator for a draft <c>JournalEntry</c>. Throws a specific
/// <see cref="JournalEntryValidationException"/> subtype on invariant violation.
/// </summary>
internal static class JournalEntryValidator
{
    public static void Validate(IReadOnlyList<JournalLineDraft> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        if (lines.Count < 2)
        {
            throw new JournalEntryTooFewLinesException(lines.Count);
        }

        foreach (var line in lines)
        {
            if (line.Amount == 0)
            {
                throw new JournalEntryZeroAmountLineException();
            }
        }

        var firstCurrency = lines[0].AccountCurrencyCode;
        for (var i = 1; i < lines.Count; i++)
        {
            if (lines[i].AccountCurrencyCode != firstCurrency)
            {
                throw new JournalEntryCurrencyMismatchException();
            }
        }

        long sum = 0L;
        foreach (var line in lines)
        {
            sum = checked(sum + line.Amount);
        }
        if (sum != 0L)
        {
            throw new JournalEntryUnbalancedException(firstCurrency.Value, sum);
        }
    }
}

internal sealed record JournalLineDraft(long Amount, CurrencyCode AccountCurrencyCode);
