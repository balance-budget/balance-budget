using Balance.Data.Entities.Enums;

namespace Balance.Services.Accounts;

internal static class AccountSignConvention
{
    public static bool IsCreditNormal(AccountType accountType) =>
        accountType switch
        {
            AccountType.Asset or AccountType.Expense => false,
            AccountType.Liability or AccountType.Equity or AccountType.Income => true,
            _ => throw new ArgumentOutOfRangeException(
                nameof(accountType),
                accountType,
                "Unknown AccountType."
            ),
        };
}
