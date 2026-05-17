using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public interface IJournalEntryService
{
    Task<IReadOnlyList<JournalEntryOutput>> ListAsync(
        int skip,
        int take,
        CancellationToken cancellationToken
    );

    Task<JournalEntryOutput?> GetAsync(JournalEntryId id, CancellationToken cancellationToken);

    Task<JournalEntryOutput> CreateAsync(
        CreateJournalEntryInput input,
        CancellationToken cancellationToken
    );

    Task<JournalEntryOutput> UpdateAsync(
        JournalEntryId id,
        UpdateJournalEntryInput input,
        CancellationToken cancellationToken
    );

    Task DeleteAsync(JournalEntryId id, CancellationToken cancellationToken);
}

public sealed record CreateJournalEntryInput(
    DateOnly Date,
    string? Description,
    BankTransactionId? BankTransactionId,
    CounterpartyId? CounterpartyId,
    IReadOnlyList<CreateJournalLineInput> Lines
);

public sealed record CreateJournalLineInput(AccountId AccountId, long Amount, string? Description);

public sealed record UpdateJournalEntryInput(
    DateOnly? Date,
    string? Description,
    BankTransactionId? BankTransactionId,
    CounterpartyId? CounterpartyId,
    IReadOnlyList<CreateJournalLineInput>? Lines
);
