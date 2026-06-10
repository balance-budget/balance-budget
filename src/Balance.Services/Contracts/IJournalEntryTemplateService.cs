using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

/// <summary>
/// Manages the user-confirmed <c>JournalEntryTemplate</c>s that drive the Outlook
/// <c>Projection</c> (ADR-0027), and proposes detected candidates mined from history. The
/// templates are the only stored half of the feature; everything forward-looking is computed by
/// <see cref="IOutlookService"/>.
/// </summary>
public interface IJournalEntryTemplateService
{
    Task<IReadOnlyList<JournalEntryTemplateOutput>> ListAsync(CancellationToken cancellationToken);

    Task<Result<JournalEntryTemplateOutput>> GetAsync(
        JournalEntryTemplateId id,
        CancellationToken cancellationToken
    );

    Task<Result<JournalEntryTemplateOutput>> CreateAsync(
        CreateJournalEntryTemplateInput input,
        CancellationToken cancellationToken
    );

    Task<Result<JournalEntryTemplateOutput>> UpdateAsync(
        JournalEntryTemplateId id,
        UpdateJournalEntryTemplateInput input,
        CancellationToken cancellationToken
    );

    Task<Result> DeleteAsync(JournalEntryTemplateId id, CancellationToken cancellationToken);

    /// <summary>
    /// Detected recurring patterns not yet matched to an existing template — the accept/dismiss
    /// queue. Recomputed from the ledger each call; nothing is stored.
    /// </summary>
    Task<IReadOnlyList<TemplateCandidateOutput>> DetectCandidatesAsync(
        CancellationToken cancellationToken
    );
}
