using Balance.Data.Entities;
using Balance.Data.Entities.Ids;

namespace Balance.Web.Endpoints.BankTransactions;

internal sealed record BankTransactionResponse(
    BankTransactionId Id,
    BankAccountId BankAccountId,
    DateOnly BookingDate,
    Money Money,
    DateTime CreatedAt,
    DateTime UpdatedAt
)
{
    public static BankTransactionResponse From(BankTransaction bankTransaction) =>
        new(
            bankTransaction.Id,
            bankTransaction.BankAccountId,
            bankTransaction.BookingDate,
            bankTransaction.Money,
            bankTransaction.CreatedAt,
            bankTransaction.UpdatedAt
        );
}
