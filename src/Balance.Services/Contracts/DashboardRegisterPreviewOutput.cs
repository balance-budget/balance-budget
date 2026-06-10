using Balance.Data.Entities;
using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

/// <summary>
/// A Register preview for every postable account: the newest-N <see cref="RegisterPreviewRow"/>s
/// of each account's Register, batched into one payload so the dashboard issues a single request
/// instead of one register request per account. Accounts without any activity are omitted.
/// </summary>
public sealed record DashboardRegisterPreviewOutput(
    int RowsPerAccount,
    IReadOnlyList<AccountRegisterPreview> Accounts
);

public sealed record AccountRegisterPreview(
    AccountId AccountId,
    IReadOnlyList<RegisterPreviewRow> Rows
);

public sealed record RegisterPreviewRow(
    JournalEntryId JournalEntryId,
    JournalLineId JournalLineId,
    DateOnly Date,
    string? EntryDescription,
    string? LineDescription,
    string? CounterpartyName,
    Money Amount
);
