using Balance.Data.Entities;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;

namespace Balance.Data.Configurations;

internal static class AccountSeed
{
    public static readonly AccountId OpeningBalancesId = new(
        Guid.Parse("00000000-0000-7000-8000-000000000001")
    );

    private static readonly DateTime SeedTimestamp = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static readonly IReadOnlyList<Account> All =
    [
        new()
        {
            Id = OpeningBalancesId,
            Name = "Opening Balances",
            Code = "3900",
            AccountType = AccountType.Equity,
            CurrencyCode = new CurrencyCode("EUR"),
            IsPostable = true,
            ParentAccountId = null,
            CreatedAt = SeedTimestamp,
            UpdatedAt = SeedTimestamp,
        },
    ];
}
