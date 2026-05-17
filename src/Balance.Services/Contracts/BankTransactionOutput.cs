using Balance.Data.Entities;
using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public sealed record BankTransactionOutput(
    BankTransactionId Id,
    BankAccountId BankAccountId,
    DateOnly BookingDate,
    Money Money,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
