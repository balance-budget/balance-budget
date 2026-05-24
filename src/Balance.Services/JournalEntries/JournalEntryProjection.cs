using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;

namespace Balance.Services.JournalEntries;

/// <summary>
/// Pure UI-shaped projection of a JournalEntry's lines, per ADR-0008 and ADR-0012.
/// No DB access, no DI — every input is in the arguments. Edge cases (transfer,
/// loan disbursement, opening balance, splits, multi-source-multi-destination) are
/// exhaustively unit-testable from this single entry point.
/// </summary>
internal static class JournalEntryProjection
{
    public static JournalEntryProjectionResult Compute(
        IReadOnlyList<JournalLineProjectionInput> lines
    )
    {
        ArgumentNullException.ThrowIfNull(lines);

        long assetSum = 0L;
        long liabilitySum = 0L;
        long grossMagnitude = 0L;

        var debitAccounts = new Dictionary<AccountId, string>();
        var creditAccounts = new Dictionary<AccountId, string>();

        foreach (var line in lines)
        {
            if (line.AccountType == AccountType.Asset)
            {
                assetSum = checked(assetSum + line.Amount);
            }
            else if (line.AccountType == AccountType.Liability)
            {
                liabilitySum = checked(liabilitySum + line.Amount);
            }

            if (line.Amount > 0)
            {
                grossMagnitude = checked(grossMagnitude + line.Amount);
                debitAccounts.TryAdd(line.AccountId, line.AccountName);
            }
            else if (line.Amount < 0)
            {
                creditAccounts.TryAdd(line.AccountId, line.AccountName);
            }
        }

        // ADR-0012's net-worth-change rule. The text reads "Σ ΔAssets − Σ ΔLiabilities":
        // an asset-side debit (+amount) increases the asset balance, and a liability-side
        // debit (+amount) decreases the liability balance, so ΔLiab = −amount(liab). That
        // collapses to (Σ amount(asset)) − (−Σ amount(liab)) = Σ amount(asset) + Σ amount(liab).
        var netWorthChange = checked(assetSum + liabilitySum);
        var isTransfer = netWorthChange == 0L;

        var isSimplifiable = debitAccounts.Count == 1 || creditAccounts.Count == 1;

        IReadOnlyList<JournalEntryLegSummary> fromLegs;
        IReadOnlyList<JournalEntryLegSummary> toLegs;
        if (isSimplifiable)
        {
            fromLegs = creditAccounts
                .OrderBy(kvp => kvp.Value, StringComparer.Ordinal)
                .Select(kvp => new JournalEntryLegSummary(kvp.Key, kvp.Value))
                .ToList();
            toLegs = debitAccounts
                .OrderBy(kvp => kvp.Value, StringComparer.Ordinal)
                .Select(kvp => new JournalEntryLegSummary(kvp.Key, kvp.Value))
                .ToList();
        }
        else
        {
            fromLegs = Array.Empty<JournalEntryLegSummary>();
            toLegs = Array.Empty<JournalEntryLegSummary>();
        }

        return new JournalEntryProjectionResult(
            isTransfer,
            netWorthChange,
            grossMagnitude,
            isSimplifiable,
            fromLegs,
            toLegs
        );
    }
}

internal sealed record JournalLineProjectionInput(
    AccountId AccountId,
    string AccountName,
    AccountType AccountType,
    long Amount
);

internal sealed record JournalEntryProjectionResult(
    bool IsTransfer,
    long NetWorthChange,
    long GrossMagnitude,
    bool IsSimplifiable,
    IReadOnlyList<JournalEntryLegSummary> FromLegs,
    IReadOnlyList<JournalEntryLegSummary> ToLegs
);
