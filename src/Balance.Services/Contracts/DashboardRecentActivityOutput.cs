using Balance.Data.Entities;
using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

/// <summary>
/// Recent register activity for every postable account, batched into one payload so the
/// dashboard issues a single request instead of one register request per account.
/// Accounts without any activity are omitted.
/// </summary>
public sealed record DashboardRecentActivityOutput(
    int RowsPerAccount,
    IReadOnlyList<DashboardAccountRecentActivity> Accounts
);

public sealed record DashboardAccountRecentActivity(
    AccountId AccountId,
    IReadOnlyList<DashboardRecentActivityRow> Rows
);

public sealed record DashboardRecentActivityRow(
    JournalEntryId JournalEntryId,
    JournalLineId JournalLineId,
    DateOnly Date,
    string? EntryDescription,
    string? LineDescription,
    string? CounterpartyName,
    Money Amount
);
