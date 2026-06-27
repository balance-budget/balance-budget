using Balance.Data.Entities.Enums;

namespace Balance.Services.Contracts;

/// <summary>
/// What an <see cref="IBankTransactionExtractor"/> can tell about a dropped file without a chosen
/// BankAccount (ADR 0034): the importer that recognized it, the <see cref="BankAccountType"/> it
/// targets, and the <see cref="AccountAnchor"/> — the normalized bank-side identifier (IBAN /
/// account number / card identifier) used to resolve the owning BankAccount. Advisory only: the
/// resolved account's own importer re-validates the file content at import time, so a probe that
/// guesses wrong fails loudly rather than writing to the wrong account.
/// </summary>
public sealed record ImportIdentity(
    string ImporterKey,
    BankAccountType SupportedType,
    string AccountAnchor
);
