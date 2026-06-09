using Balance.Data.Entities;
using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public sealed record BankTransactionOutput(
    BankTransactionId Id,
    BankAccountId BankAccountId,
    DateOnly BookingDate,
    Money Money,
    string Description,
    string? CounterpartyName,
    string? CounterpartyAccountNumber,
    DateOnly? ValueDate,
    string? Reference,
    string? MandateId,
    string? SepaCreditorId,
    long? ForeignAmount,
    string? ForeignCurrencyCode,
    decimal? ExchangeRate,
    string? ImporterKey,
    JournalEntryId? JournalEntryId,
    DateTime? DismissedAt,
    string? DismissedReason,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    AttachHintOutput? MatchingJournalEntry = null,
    LoanPaymentHintOutput? LoanPaymentHint = null
);

/// <summary>
/// Inbox hint that a row looks like a Loan payment (ADR-0025): its counterparty account number
/// belongs to a counterparty that is some Loan's lender. One click away from the loan-aware
/// categorize mode; analogous to the Attach hint of ADR-0012.
/// </summary>
public sealed record LoanPaymentHintOutput(LoanId LoanId, string LoanName);
