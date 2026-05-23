using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;

namespace Balance.Services.JournalEntries;

internal static class JournalEntryValidator
{
    internal const string TooFewLinesMessage = "A JournalEntry requires at least 2 JournalLines";
    internal const string ZeroAmountLineMessage = "JournalLine.Amount must be non-zero.";
    internal const string CurrencyMismatchMessage =
        "All JournalLines in a single JournalEntry must share the same CurrencyCode in v1.";
    internal const string UnbalancedMessage = "JournalEntry lines must net to zero per currency";

    public static Result Validate(IReadOnlyList<JournalLineDraft> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        if (lines.Count < 2)
        {
            return new InvariantError(
                ErrorCodes.JournalTooFewLines,
                $"{TooFewLinesMessage}; got {lines.Count}."
            );
        }

        foreach (var line in lines)
        {
            if (line.Amount == 0)
            {
                return new InvariantError(ErrorCodes.JournalZeroAmountLine, ZeroAmountLineMessage);
            }
        }

        var firstCurrency = lines[0].AccountCurrencyCode;
        for (var i = 1; i < lines.Count; i++)
        {
            if (lines[i].AccountCurrencyCode != firstCurrency)
            {
                return new InvariantError(
                    ErrorCodes.JournalCurrencyMismatch,
                    CurrencyMismatchMessage
                );
            }
        }

        long sum = 0L;
        foreach (var line in lines)
        {
            sum = checked(sum + line.Amount);
        }

        if (sum != 0L)
        {
            return new InvariantError(
                ErrorCodes.JournalUnbalanced,
                $"{UnbalancedMessage}; sum for {firstCurrency.Value} is {sum}."
            );
        }

        return Result.Success;
    }
}

internal sealed record JournalLineDraft(long Amount, CurrencyCode AccountCurrencyCode);
