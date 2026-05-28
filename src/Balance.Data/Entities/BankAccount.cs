using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;

namespace Balance.Data.Entities;

public sealed class BankAccount : BaseEntity<BankAccountId>
{
    public BankAccountType Type { get; set; } = BankAccountType.Current;
    public string? Iban { get; set; }
    public string? AccountNumber { get; set; }
    public string? CardIdentifier { get; set; }
    public string? Bic { get; set; }
    public string? BankName { get; set; }
    public string? AccountHolderName { get; set; }
    public CurrencyCode? CurrencyCode { get; set; }
    public string? ImporterKey { get; set; }
    public AccountId? AccountId { get; set; }
    public CounterpartyId? CounterpartyId { get; set; }
}
