using Balance.Data.Exceptions;

namespace Balance.Services.JournalEntries;

internal abstract class JournalEntryValidationException : DomainException
{
    protected JournalEntryValidationException() { }

    protected JournalEntryValidationException(string message)
        : base(DomainExceptionKind.Invariant, message) { }

    protected JournalEntryValidationException(string message, Exception innerException)
        : base(DomainExceptionKind.Invariant, message, innerException) { }
}

internal sealed class JournalEntryTooFewLinesException : JournalEntryValidationException
{
    public JournalEntryTooFewLinesException() { }

    public JournalEntryTooFewLinesException(string message)
        : base(message) { }

    public JournalEntryTooFewLinesException(string message, Exception innerException)
        : base(message, innerException) { }

    public JournalEntryTooFewLinesException(int actualLineCount)
        : base($"A JournalEntry requires at least 2 JournalLines; got {actualLineCount}.") { }
}

internal sealed class JournalEntryUnbalancedException : JournalEntryValidationException
{
    public JournalEntryUnbalancedException() { }

    public JournalEntryUnbalancedException(string message)
        : base(message) { }

    public JournalEntryUnbalancedException(string message, Exception innerException)
        : base(message, innerException) { }

    public JournalEntryUnbalancedException(string currencyCode, long sum)
        : base(
            $"JournalEntry lines must net to zero per currency; "
                + $"sum for {currencyCode} is {sum}."
        ) { }
}

internal sealed class JournalEntryCurrencyMismatchException : JournalEntryValidationException
{
    public JournalEntryCurrencyMismatchException()
        : base("All JournalLines in a single JournalEntry must share the same CurrencyCode in v1.")
    { }

    public JournalEntryCurrencyMismatchException(string message)
        : base(message) { }

    public JournalEntryCurrencyMismatchException(string message, Exception innerException)
        : base(message, innerException) { }
}

internal sealed class JournalEntryZeroAmountLineException : JournalEntryValidationException
{
    public JournalEntryZeroAmountLineException()
        : base("JournalLine.Amount must be non-zero.") { }

    public JournalEntryZeroAmountLineException(string message)
        : base(message) { }

    public JournalEntryZeroAmountLineException(string message, Exception innerException)
        : base(message, innerException) { }
}
