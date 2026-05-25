using Balance.Data.Entities;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public interface IJournalEntryService
{
    Task<IReadOnlyList<JournalEntryOutput>> ListAsync(
        int skip,
        int take,
        CancellationToken cancellationToken
    );

    Task<Result<JournalEntryOutput>> GetAsync(
        JournalEntryId id,
        CancellationToken cancellationToken
    );

    Task<Result<UpdateJournalEntryInput>> GetSnapshotAsync(
        JournalEntryId id,
        CancellationToken cancellationToken
    );

    Task<Result<JournalEntryOutput>> CreateAsync(
        CreateJournalEntryInput input,
        CancellationToken cancellationToken
    );

    Task<Result<JournalEntryOutput>> UpdateAsync(
        JournalEntryId id,
        UpdateJournalEntryInput input,
        CancellationToken cancellationToken
    );

    Task<Result> DeleteAsync(JournalEntryId id, CancellationToken cancellationToken);
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
    string? Description,
    ReconciliationStatus ReconciliationStatus = ReconciliationStatus.Uncleared
);

/// <summary>
/// Patchable surface of a <see cref="JournalEntry"/>. <see cref="Lines"/> is keyed by the
/// <see cref="JournalLineId"/> rendered as a "D"-format Guid string; the service parses keys
/// back to typed IDs and enforces key-set equality so lines cannot be added or removed via
/// PATCH. <c>BankTransactionId</c> is intentionally not part of this surface — once an entry
/// links to an import row, that link is part of the audit trail.
/// </summary>
public sealed record UpdateJournalEntryInput
{
    public required DateOnly Date { get; set; }
    public string? Description { get; set; }
    public CounterpartyId? CounterpartyId { get; set; }
    public required IDictionary<string, UpdateJournalLineInput> Lines { get; init; }
}

public sealed record UpdateJournalLineInput
{
    public string? Description { get; set; }
}
