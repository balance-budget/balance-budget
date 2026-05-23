namespace Balance.Services.Contracts;

/// <summary>
/// Stable error-code vocabulary shared between services and API consumers. New codes belong here
/// when they're referenced from more than one site; one-off codes stay inline at the throw site.
/// </summary>
public static class ErrorCodes
{
    // Cross-cutting
    public const string NotFound = "not_found";
    public const string RequestInvalid = "request.invalid";
    public const string UniquenessConflict = "uniqueness_conflict";
    public const string Referenced = "referenced";

    // Account
    public const string AccountNameTaken = "account.name_taken";
    public const string AccountNameEmpty = "account.name_empty";

    // Counterparty
    public const string CounterpartyNameTaken = "counterparty.name_taken";
    public const string CounterpartyNameEmpty = "counterparty.name_empty";

    // BankAccount
    public const string BankAccountIbanTaken = "bank_account.iban_taken";
    public const string BankAccountSlotTaken = "bank_account.slot_taken";
    public const string BankAccountOwnershipXor = "bank_account.ownership_xor";
    public const string BankAccountIdentifierMissing = "bank_account.identifier_missing";
    public const string BankAccountCurrencyRequiredWhenOwned =
        "bank_account.currency_required_when_owned";

    // BankTransaction
    public const string BankTransactionAmountZero = "bank_transaction.amount_zero";
    public const string BankTransactionRequiresOwnAccount = "bank_transaction.requires_own_account";

    // Currency
    public const string CurrencyExists = "currency.exists";
    public const string CurrencyNameEmpty = "currency.name_empty";

    // JournalEntry
    public const string JournalTooFewLines = "journal.too_few_lines";
    public const string JournalZeroAmountLine = "journal.zero_amount_line";
    public const string JournalCurrencyMismatch = "journal.currency_mismatch";
    public const string JournalUnbalanced = "journal.unbalanced";
    public const string JournalLineKeyInvalid = "journal.line_key_invalid";
    public const string JournalLineSetMismatch = "journal.line_set_mismatch";
}
