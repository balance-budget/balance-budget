using Balance.Data.Entities;
using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

/// <summary>
/// Detail-endpoint shape: every <see cref="BankTransactionOutput"/> field plus the
/// typed key-value <c>Metadata</c> bag (ADR 0015). The list endpoint stays on the
/// narrower <see cref="BankTransactionOutput"/> shape — pulling per-row metadata
/// for every Inbox row would be wasted work for fields the user only consults on
/// detail.
/// </summary>
public sealed record BankTransactionDetailOutput(
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
    IReadOnlyList<BankTransactionMetadataEntryOutput> Metadata
);
