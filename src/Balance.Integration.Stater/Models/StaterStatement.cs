using System.Collections.Generic;

namespace Balance.Integration.Stater.Models;

// AccountNumber is the bouwdepot's header number, which equals the loan number (not an IBAN).
internal sealed record StaterStatement(
    string AccountNumber,
    IReadOnlyList<StaterStatementRow> Rows
);
