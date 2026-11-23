using Balance.Data.Entities;

namespace Balance.Services.Helpers;

// A statement identifies the other side of a row by IBAN, account number, or card identifier,
// written with or without spaces and in either case.
internal static class BankAccountIdentifiers
{
    public static string Normalize(string? value) =>
        value is null
            ? string.Empty
            : value.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();

    public static bool Identifies(BankAccount bankAccount, string? identifier)
    {
        var normalized = Normalize(identifier);
        return normalized.Length > 0
            && (
                Normalize(bankAccount.Iban) == normalized
                || Normalize(bankAccount.AccountNumber) == normalized
                || Normalize(bankAccount.CardIdentifier) == normalized
            );
    }
}
