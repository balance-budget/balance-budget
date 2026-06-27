using Balance.Data;
using Balance.Data.Entities;
using Balance.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Balance.Services.BankTransactions;

internal sealed class BankStatementDetectionService : IBankStatementDetectionService
{
    private readonly BalanceDbContext _dbContext;
    private readonly IReadOnlyList<IBankTransactionExtractor> _extractors;
    private readonly IBankTransactionImportService _importService;

    public BankStatementDetectionService(
        BalanceDbContext dbContext,
        IEnumerable<IBankTransactionExtractor> extractors,
        IBankTransactionImportService importService
    )
    {
        _dbContext = dbContext;
        _extractors = extractors.ToList();
        _importService = importService;
    }

    public async Task<IReadOnlyList<DetectedImportOutcome>> DetectAndImportAsync(
        IReadOnlyList<ImportFile> files,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(files);

        // Owned accounts are the only import targets (ADR 0009/0034) and the data set is
        // personal-scale, so load them once and resolve anchors in memory.
        var ownedAccounts = await _dbContext
            .BankAccounts.AsNoTracking()
            .Where(b => b.AccountId != null)
            .ToListAsync(cancellationToken);

        var outcomes = new List<DetectedImportOutcome>(files.Count);
        foreach (var file in files)
            outcomes.Add(await DetectOneAsync(file, ownedAccounts, cancellationToken));

        return outcomes;
    }

    private async Task<DetectedImportOutcome> DetectOneAsync(
        ImportFile file,
        IReadOnlyList<BankAccount> ownedAccounts,
        CancellationToken cancellationToken
    )
    {
        var anchors = new HashSet<string>(StringComparer.Ordinal);
        foreach (var extractor in _extractors)
        {
            var identity = await extractor.TryIdentifyAsync(file, cancellationToken);
            if (identity is not null && !string.IsNullOrEmpty(identity.AccountAnchor))
                anchors.Add(identity.AccountAnchor);
        }

        if (anchors.Count == 0)
            return Unresolved(file, ImportFileStatus.Unrecognized, null);

        // The anchor is the bank-side identifier; match it against any owned account's normalized
        // IBAN / account number / card identifier. The unique owned-identifier indexes (ADR 0034)
        // make more than one match impossible in practice, but we still guard for it.
        var anchor = anchors.First();
        var matches = ownedAccounts
            .Where(account =>
                anchors.Contains(Normalize(account.Iban))
                || anchors.Contains(Normalize(account.AccountNumber))
                || anchors.Contains(Normalize(account.CardIdentifier))
            )
            .ToList();

        if (matches.Count == 0)
            return Unresolved(file, ImportFileStatus.NoMatchingAccount, anchor);

        if (matches.Select(m => m.Id).Distinct().Count() > 1)
            return Unresolved(file, ImportFileStatus.AmbiguousMatch, anchor, matches[0].Id);

        var account = matches[0];
        if (account.ImporterKey is null)
            return Unresolved(file, ImportFileStatus.NotImportable, anchor, account.Id);

        // Detection only proposes the target; the account's own importer re-validates the file
        // content, so a wrong guess fails here loudly instead of writing to the wrong account.
        if (file.Content.CanSeek)
            file.Content.Seek(0, SeekOrigin.Begin);

        var result = await _importService.ImportAsync(account.Id, file.Content, cancellationToken);
        if (result.IsFailure)
            return new DetectedImportOutcome(
                file.FileName,
                ImportFileStatus.Failed,
                account.Id,
                anchor,
                0,
                0,
                result.Error!.Code
            );

        return new DetectedImportOutcome(
            file.FileName,
            ImportFileStatus.Imported,
            account.Id,
            anchor,
            result.Value!.Imported,
            result.Value.SkippedAsDuplicate,
            null
        );
    }

    private static DetectedImportOutcome Unresolved(
        ImportFile file,
        ImportFileStatus status,
        string? anchor,
        Data.Entities.Ids.BankAccountId? bankAccountId = null
    ) => new(file.FileName, status, bankAccountId, anchor, 0, 0, null);

    // Match the ING extractors' anchor normalization: strip spaces, upper-case.
    private static string Normalize(string? value) =>
        value is null
            ? string.Empty
            : value.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
}
