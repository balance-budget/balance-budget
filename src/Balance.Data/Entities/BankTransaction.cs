using Balance.Data.Entities.Ids;

namespace Balance.Data.Entities;

public sealed class BankTransaction : BaseEntity<BankTransactionId>
{
    public required BankAccountId BankAccountId { get; init; }
    public required DateOnly BookingDate { get; init; }
    public required Money Money { get; init; }
    public required string Description { get; init; }
    public string? CounterpartyName { get; init; }
    public string? CounterpartyAccountNumber { get; init; }
    public required string RawSource { get; init; }
    public required string RowHash { get; init; }
}
