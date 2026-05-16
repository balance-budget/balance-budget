using Balance.Data.Entities;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;

namespace Balance.Web.Endpoints.Accounts;

internal sealed record AccountResponse(
    AccountId Id,
    string Name,
    AccountType AccountType,
    CurrencyCode CurrencyCode,
    DateTime CreatedAt,
    DateTime UpdatedAt
)
{
    public static AccountResponse From(Account account) =>
        new(
            account.Id,
            account.Name,
            account.AccountType,
            account.CurrencyCode,
            account.CreatedAt,
            account.UpdatedAt
        );
}
