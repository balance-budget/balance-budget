using Balance.Data.Entities.Ids;

namespace Balance.Data.Entities;

public sealed class BankTransaction : BaseEntity<BankTransactionId>
{
    public required BankAccountId BankAccountId { get; init; }
    public required DateOnly BookingDate { get; init; }
    public required Money Money { get; init; }
}
