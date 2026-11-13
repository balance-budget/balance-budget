using Balance.Integration.Rabobank.Models;

namespace Balance.Integration.Rabobank.Contracts;

internal interface IRabobankCreditCardStatementParser
{
    /// <summary>
    /// Parses a credit-card export. Returns <c>null</c> when the file does not carry this
    /// layout's header, and throws when it does but a row cannot be read.
    /// </summary>
    public ValueTask<IReadOnlyList<CreditCardStatementRow>?> ParseStatementsAsync(
        Stream stream,
        CancellationToken cancellationToken
    );
}
