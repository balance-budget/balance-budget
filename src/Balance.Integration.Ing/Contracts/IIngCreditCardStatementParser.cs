using Balance.Integration.Ing.Models.CreditCard;

namespace Balance.Integration.Ing.Contracts;

// A single ING credit-card statement layout. The extractor reads the PDF once
// (IngCreditCardPdfReader) and then asks each registered parser whether it recognizes the
// extracted lines; the one matching layout parses them (ADR 0034). Layout selection is by
// content only — never filename or date.
internal interface IIngCreditCardStatementParser
{
    // Cheap structural probe: does this layout recognize the extracted statement lines?
    bool CanParse(IReadOnlyList<string> lines);

    // ING statements list the most recent transaction first for some layouts. When true, the
    // extractor reverses the rows before insertion so the time-ordered BankTransaction.Id minted
    // per row follows BookingDate and a list sorted by (BookingDate, Id) breaks intra-day ties
    // chronologically.
    bool RowsAreMostRecentFirst { get; }

    CreditCardStatement ParseStatement(
        IReadOnlyList<string> lines,
        CancellationToken cancellationToken
    );
}
