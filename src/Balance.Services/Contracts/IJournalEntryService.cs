using Balance.Data.Entities;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public interface IJournalEntryService
{
    Task<PagedOutput<JournalEntryOutput>> ListAsync(
        int skip,
        int take,
        string? search,
        CounterpartyId? counterpartyId,
        AccountId? accountId,
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken cancellationToken
    );

    Task<Result<JournalEntryDetailOutput>> GetAsync(
        JournalEntryId id,
        CancellationToken cancellationToken
    );

    Task<Result<UpdateJournalEntryInput>> GetSnapshotAsync(
        JournalEntryId id,
        CancellationToken cancellationToken
    );

    Task<Result<JournalEntryDetailOutput>> CreateAsync(
        CreateJournalEntryInput input,
        CancellationToken cancellationToken
    );

    Task<Result<JournalEntryDetailOutput>> UpdateAsync(
        JournalEntryId id,
        UpdateJournalEntryInput input,
        CancellationToken cancellationToken
    );

    Task<Result<JournalEntryDetailOutput>> ReplaceAsync(
        JournalEntryId id,
        ReplaceJournalEntryInput input,
        CancellationToken cancellationToken
    );

    Task<Result> DeleteAsync(JournalEntryId id, CancellationToken cancellationToken);

    /// <summary>
    /// Re-points every line in <paramref name="lineIds"/> to <paramref name="targetAccountId"/>
    /// in one transaction — the whole batch moves or nothing does. The target must be postable
    /// and share each line's currency; a line whose <c>ReconciliationStatus</c> is not
    /// <c>Uncleared</c> is frozen (ADR-0014) and rejects the batch. Amounts, descriptions and
    /// the owning entries' other legs are untouched, so every entry still nets to zero.
    /// </summary>
    Task<Result> ReassignLinesAsync(
        IReadOnlyList<JournalLineId> lineIds,
        AccountId targetAccountId,
        CancellationToken cancellationToken
    );
}

public sealed record CreateJournalEntryInput(
    DateOnly Date,
    string? Description,
    CounterpartyId? CounterpartyId,
    IReadOnlyList<CreateJournalLineInput> Lines
);

/// <summary>
/// <see cref="LoanPartId"/> is the Loan Part attribution set only by loan-aware flows
/// (ADR-0025). A line targeting a loan-managed account must carry the owning part's id — that
/// attribution is what makes the flow loan-aware; generic flows never populate it and are
/// refused. A line attributed to a part may only target that part's account (principal) or the
/// loan's interest Expense account (interest, prepayment penalty).
/// </summary>
public sealed record CreateJournalLineInput(
    AccountId AccountId,
    long Amount,
    string? Description,
    ReconciliationStatus ReconciliationStatus = ReconciliationStatus.Uncleared,
    LoanPartId? LoanPartId = null
);

/// <summary>
/// Patchable surface of a <see cref="JournalEntry"/>. <see cref="Lines"/> is keyed by the
/// <see cref="JournalLineId"/> rendered as a "D"-format Guid string; the service parses keys
/// back to typed IDs and enforces key-set equality so lines cannot be added or removed via
/// PATCH. The BT↔JE link is mutated through Attach/Detach on the BankTransaction side
/// (ADR 0012) and is not part of this surface.
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

/// <summary>
/// Full-body replace surface of a <see cref="JournalEntry"/> (ADR 0014). The client sends the
/// desired final state; the server validates that lines whose current
/// <see cref="ReconciliationStatus"/> is not <see cref="ReconciliationStatus.Uncleared"/> appear
/// in <see cref="Lines"/> with unchanged <see cref="ReplaceJournalLineInput.AccountId"/> and
/// <see cref="ReplaceJournalLineInput.Amount"/>. Existing <c>Uncleared</c> lines omitted from
/// <see cref="Lines"/> are deleted; lines without an <see cref="ReplaceJournalLineInput.Id"/> are
/// inserted with a server-assigned id and default to <c>Uncleared</c>. Per-line
/// <c>ReconciliationStatus</c> is validated to match current when supplied (the PUT does not
/// mutate it). The BT↔JE link lives on the BankTransaction side now (ADR 0012) and is
/// mutated via Attach/Detach, not this endpoint.
/// </summary>
public sealed record ReplaceJournalEntryInput(
    DateOnly Date,
    string? Description,
    CounterpartyId? CounterpartyId,
    IReadOnlyList<ReplaceJournalLineInput> Lines
);

public sealed record ReplaceJournalLineInput(
    JournalLineId? Id,
    AccountId AccountId,
    long Amount,
    string? Description,
    ReconciliationStatus? ReconciliationStatus = null
);
