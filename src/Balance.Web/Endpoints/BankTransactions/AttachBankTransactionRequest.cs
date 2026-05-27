using Balance.Data.Entities.Ids;
using FluentValidation;

namespace Balance.Web.Endpoints.BankTransactions;

internal sealed record AttachBankTransactionRequest(JournalEntryId JournalEntryId);

internal sealed class AttachBankTransactionRequestValidator
    : AbstractValidator<AttachBankTransactionRequest>
{
    public AttachBankTransactionRequestValidator()
    {
        RuleFor(x => x.JournalEntryId.Value).NotEqual(Guid.Empty);
    }
}
