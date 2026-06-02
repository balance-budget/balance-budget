using System.Linq.Expressions;
using Balance.Data.Entities;
using Balance.Services.Contracts;

namespace Balance.Services.BankTransactions;

/// <summary>
/// Single source for the BankTransaction wire-shape projections, so the column list stays in one
/// place rather than being re-typed per query.
/// </summary>
internal static class BankTransactionProjections
{
    /// <summary>
    /// BankTransaction -> BankTransactionOutput. Used directly as an EF projection by the list
    /// query and, compiled, by the in-memory mappers (Create / Dismiss / Undismiss). The Inbox
    /// <c>MatchingJournalEntry</c> hint is layered on afterward, not part of this shape.
    /// </summary>
    public static readonly Expression<Func<BankTransaction, BankTransactionOutput>> ToOutput =
        b => new BankTransactionOutput(
            b.Id,
            b.BankAccountId,
            b.BookingDate,
            b.Money,
            b.Description,
            b.CounterpartyName,
            b.CounterpartyAccountNumber,
            b.ValueDate,
            b.Reference,
            b.MandateId,
            b.SepaCreditorId,
            b.ForeignAmount,
            b.ForeignCurrencyCode,
            b.ExchangeRate,
            b.ImporterKey,
            b.JournalEntryId,
            b.DismissedAt,
            b.DismissedReason,
            b.CreatedAt,
            b.UpdatedAt
        );

    private static readonly Func<BankTransaction, BankTransactionOutput> ToOutputCompiled =
        ToOutput.Compile();

    /// <summary>In-memory equivalent of <see cref="ToOutput"/> for already-materialised entities.</summary>
    public static BankTransactionOutput ToOutputInMemory(BankTransaction bankTransaction) =>
        ToOutputCompiled(bankTransaction);

    /// <summary>
    /// BankTransaction -> BankTransactionDetailOutput (adds the ordered metadata bag). Used as a
    /// top-level EF projection by Get. NOTE: <c>JournalEntryService.ProjectDetailOutput</c>
    /// constructs the same record inline inside its JournalEntry projection because EF cannot
    /// invoke a captured expression inside a nested Select — keep the two value-mappings in sync
    /// (the positional record means any field add/remove breaks both at compile time).
    /// </summary>
    public static readonly Expression<
        Func<BankTransaction, BankTransactionDetailOutput>
    > ToDetailOutput = b => new BankTransactionDetailOutput(
        b.Id,
        b.BankAccountId,
        b.BookingDate,
        b.Money,
        b.Description,
        b.CounterpartyName,
        b.CounterpartyAccountNumber,
        b.ValueDate,
        b.Reference,
        b.MandateId,
        b.SepaCreditorId,
        b.ForeignAmount,
        b.ForeignCurrencyCode,
        b.ExchangeRate,
        b.ImporterKey,
        b.JournalEntryId,
        b.DismissedAt,
        b.DismissedReason,
        b.CreatedAt,
        b.UpdatedAt,
        b.Metadata.OrderBy(m => m.Key!.Name)
            .Select(m => new BankTransactionMetadataEntryOutput(
                m.Key!.Name,
                m.StringValue,
                m.IntegerValue
            ))
            .ToList()
    );
}
