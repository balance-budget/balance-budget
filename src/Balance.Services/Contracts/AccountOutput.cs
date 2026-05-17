using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public sealed record AccountOutput(
    AccountId Id,
    string Name,
    AccountType AccountType,
    CurrencyCode CurrencyCode,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
