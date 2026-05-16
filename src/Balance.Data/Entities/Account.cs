using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;

namespace Balance.Data.Entities;

public sealed class Account : BaseEntity<AccountId>
{
    public required string Name { get; set; }
    public required AccountType AccountType { get; set; }
    public required CurrencyCode CurrencyCode { get; set; }
}
