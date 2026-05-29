using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public sealed record SearchOutput(
    SearchSection<AccountHit> Accounts,
    SearchSection<CounterpartyHit> Counterparties,
    SearchSection<BankAccountHit> BankAccounts,
    SearchSection<JournalEntryHit> JournalEntries,
    SearchSection<PageHit> Pages
);

public sealed record SearchSection<T>(IReadOnlyList<T> Items, int TotalCount);

public sealed record AccountHit(AccountId Id, string Name, AccountType AccountType);

public sealed record CounterpartyHit(CounterpartyId Id, string Name);

public sealed record BankAccountHit(
    BankAccountId Id,
    BankAccountType Type,
    string? Iban,
    string? AccountNumber,
    string? CardIdentifier,
    string? BankName,
    string? AccountHolderName
);

public sealed record JournalEntryHit(JournalEntryId Id, DateOnly Date, string? Description);

public sealed record PageHit(string Label, string Route);
