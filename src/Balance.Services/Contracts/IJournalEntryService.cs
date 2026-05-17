using Balance.Data.Entities;
using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public interface IJournalEntryService
{
    Task<IReadOnlyList<JournalEntry>> ListAsync(
        int skip,
        int take,
        CancellationToken cancellationToken
    );

    Task<JournalEntry?> GetAsync(JournalEntryId id, CancellationToken cancellationToken);

    Task<JournalEntry> CreateAsync(
        CreateJournalEntryInput input,
        CancellationToken cancellationToken
    );

    Task<JournalEntry> UpdateAsync(
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

public sealed record CreateJournalLineInput(
    AccountId AccountId,
    long Amount,
    string? Description
);

public sealed record UpdateJournalEntryInput(
    DateOnly? Date,
    string? Description,
    BankTransactionId? BankTransactionId,
    CounterpartyId? CounterpartyId,
    IReadOnlyList<CreateJournalLineInput>? Lines
);
