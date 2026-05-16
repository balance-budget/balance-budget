using Balance.Data.Entities.Ids;

namespace Balance.Data.Entities;

public sealed class BankAccount : BaseEntity<BankAccountId>
{
    public string? Iban { get; set; }
    public string? AccountNumber { get; set; }
    public string? Bic { get; set; }
    public string? BankName { get; set; }
    public string? AccountHolderName { get; set; }
    public CurrencyCode? CurrencyCode { get; set; }
    public AccountId? AccountId { get; set; }
    public CounterpartyId? CounterpartyId { get; set; }
}
