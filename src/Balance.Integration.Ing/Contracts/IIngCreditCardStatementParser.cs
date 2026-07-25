using Balance.Integration.Ing.Helpers;
using Balance.Integration.Ing.Models.CreditCard;

namespace Balance.Integration.Ing.Contracts;

// A single ING credit-card statement layout. The extractor asks each registered layout whether it
// recognizes the file; the one matching layout parses it (ADR 0034). Layout selection is by content
// only — never filename or date.
//
// Layouts take an IngCreditCardStatementSource rather than a Stream so the PDF layouts share one
// text extraction per file while the CSV layout reads the raw stream (ADR 0038).
internal interface IIngCreditCardStatementParser
{
    // Cheap structural probe: does this layout recognize this file?
    ValueTask<bool> CanParseAsync(
        IngCreditCardStatementSource source,
        CancellationToken cancellationToken
    );

    // ING statements list the most recent transaction first for some layouts. When true, the
    // extractor reverses the rows before insertion so the time-ordered BankTransaction.Id minted
    // per row follows BookingDate and a list sorted by (BookingDate, Id) breaks intra-day ties
    // chronologically.
    bool RowsAreMostRecentFirst { get; }

    ValueTask<CreditCardStatement> ParseStatementAsync(
        IngCreditCardStatementSource source,
        CancellationToken cancellationToken
    );
}
