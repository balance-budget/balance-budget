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
    public const string AccountCodeTaken = "account.code_taken";
    public const string AccountCodeEmpty = "account.code_empty";
    public const string AccountParentMustBeNonPostable = "account.parent_must_be_non_postable";
    public const string AccountSubtreeTypeMismatch = "account.subtree_type_mismatch";
    public const string AccountSubtreeCurrencyMismatch = "account.subtree_currency_mismatch";
    public const string AccountParentCycle = "account.parent_cycle";
    public const string AccountHasChildren = "account.has_children";
    public const string AccountHasLines = "account.has_lines";
    public const string AccountTypeLockedInTree = "account.type_locked_in_tree";

    // Counterparty
    public const string CounterpartyNameTaken = "counterparty.name_taken";
    public const string CounterpartyNameEmpty = "counterparty.name_empty";

    // BankAccount
    public const string BankAccountIbanTaken = "bank_account.iban_taken";
    public const string BankAccountSlotTaken = "bank_account.slot_taken";
    public const string BankAccountOwnershipXor = "bank_account.ownership_xor";
    public const string BankAccountIdentifierMissing = "bank_account.identifier_missing";
    public const string BankAccountCardOwnedOnly = "bank_account.card_owned_only";
    public const string BankAccountCurrencyRequiredWhenOwned =
        "bank_account.currency_required_when_owned";
    public const string BankAccountImporterUnknown = "bank_account.importer_unknown";
    public const string BankAccountImporterTypeMismatch = "bank_account.importer_type_mismatch";

    // BankTransaction
    public const string BankTransactionAmountZero = "bank_transaction.amount_zero";
    public const string BankTransactionRequiresOwnAccount = "bank_transaction.requires_own_account";
    public const string BankTransactionAlreadyDismissed = "bank_transaction.already_dismissed";
    public const string BankTransactionNotDismissed = "bank_transaction.not_dismissed";
    public const string BankTransactionAlreadyCategorised = "bank_transaction.already_categorised";
    public const string BankTransactionDismissed = "bank_transaction.dismissed";
    public const string BankTransactionNotAttached = "bank_transaction.not_attached";
    public const string CategoriseCounterpartySelection = "categorise.counterparty_selection";

    // BankTransaction Attach (ADR 0013)
    public const string AttachPredicateFailed = "attach.predicate_failed";
    public const string AttachSelfTransferGuard = "attach.self_transfer_guard";

    // BankTransaction import
    public const string ImportIbanMismatch = "import.iban_mismatch";
    public const string ImportAccountColumnDivergence = "import.account_column_divergence";
    public const string ImportCurrencyMismatch = "import.currency_mismatch";
    public const string ImportBankAccountNotOwned = "import.bank_account_not_owned";
    public const string ImportBankAccountNotImportable = "import.bank_account_not_importable";
    public const string ImportBankAccountWrongImporter = "import.bank_account_wrong_importer";
    public const string ImportFormatInvalid = "import.format_invalid";
    public const string ImportConcurrentConflict = "import.concurrent_conflict";

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
    public const string JournalLineFrozen = "journal.line_frozen";
    public const string JournalLineUnknown = "journal.line_unknown";
    public const string JournalLineStatusMutation = "journal.line_status_mutation";
}
