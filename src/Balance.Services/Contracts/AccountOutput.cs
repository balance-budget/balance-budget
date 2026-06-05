using Balance.Data.Entities;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public sealed record AccountOutput(
    AccountId Id,
    string Name,
    string Code,
    AccountType AccountType,
    CurrencyCode CurrencyCode,
    bool IsPostable,
    AccountId? ParentAccountId,
    string? IconName,
    Money Balance,
    BankAccountSummary? BankAccount,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
