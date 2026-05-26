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
    DateTime UpdatedAt
);
