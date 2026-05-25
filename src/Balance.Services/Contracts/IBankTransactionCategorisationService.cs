using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

/// <summary>
/// Composite write that turns one unmatched <c>BankTransaction</c> into exactly one
/// <c>JournalEntry</c>, optionally creating a new <c>Counterparty</c> and a counterparty-owned
/// <c>BankAccount</c> on the way. All side effects commit in a single DB transaction; any
/// failure rolls the whole thing back. See ADR-0014.
/// </summary>
public interface IBankTransactionCategorisationService
{
    Task<Result<JournalEntryOutput>> CategorizeAsync(
        BankTransactionId id,
        CategorizeBankTransactionInput input,
        CancellationToken cancellationToken
    );
}

/// <summary>
/// Input for <see cref="IBankTransactionCategorisationService.CategorizeAsync"/>. Exactly one
/// of <see cref="CounterpartyId"/> or <see cref="NewCounterparty"/> must be set; the
/// <see cref="Lines"/> are the counter-side lines only — the bank-side line is derived
/// from the BT's <c>BankAccount</c> and must sum with the counter-side to zero.
/// </summary>
public sealed record CategorizeBankTransactionInput(
    CounterpartyId? CounterpartyId,
    NewCounterpartyInput? NewCounterparty,
    DateOnly Date,
    string? Description,
    IReadOnlyList<CategorizeBankTransactionLineInput> Lines
);

public sealed record NewCounterpartyInput(string Name);

public sealed record CategorizeBankTransactionLineInput(
    AccountId AccountId,
    long Amount,
    string? Description
);
