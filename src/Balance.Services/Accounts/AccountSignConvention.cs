using System.Diagnostics;
using Balance.Data.Entities;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;

namespace Balance.Services.Accounts;

internal static class AccountSignConvention
{
    public static bool IsCreditNormal(AccountType accountType) =>
        accountType switch
        {
            AccountType.Asset or AccountType.Expense => false,
            AccountType.Liability or AccountType.Equity or AccountType.Income => true,
            _ => throw new UnreachableException($"Unknown AccountType '{accountType}'."),
        };

    /// <summary>
    /// Converts a raw <c>SUM(JournalLine.Amount)</c> (debit-positive, ADR-0002) into the account's
    /// running balance per the ADR-0011 sign convention: debit-normal accounts (Asset/Expense) keep
    /// the sum; credit-normal accounts (Liability/Equity/Income) negate it.
    /// </summary>
    public static Money ToBalance(
        AccountType accountType,
        long rawSum,
        CurrencyCode currencyCode
    ) => new(IsCreditNormal(accountType) ? checked(-rawSum) : rawSum, currencyCode);
}
