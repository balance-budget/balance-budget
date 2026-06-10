using Balance.Data;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Microsoft.EntityFrameworkCore;

namespace Balance.Services.Outlook;

/// <summary>
/// Shared ledger reads for the Outlook feature: pulls the <em>profit-and-loss-touching</em>
/// activity on a set of liquid accounts over a window — the raw material for both <c>Typical
/// spend</c> (the trailing baseline) and <c>Occurrence matching</c> / detection. Entries with no
/// Income or Expense leg (self-transfers) are excluded: they are not spend, and recurring ones are
/// modeled as their own templates (ADR-0027). All reads are batched, never per-account fan-out.
/// </summary>
internal static class OutlookLedger
{
    public static async Task<IReadOnlyList<LedgerOccurrence>> LoadOccurrencesAsync(
        BalanceDbContext dbContext,
        IReadOnlyCollection<AccountId> liquidAccountIds,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken
    )
    {
        if (liquidAccountIds.Count == 0)
            return [];

        // Lines on the liquid (bank-side) accounts within the window, with their entry header.
        var liquidLines = await dbContext
            .JournalLines.AsNoTracking()
            .Where(l => liquidAccountIds.Contains(l.AccountId))
            .Join(
                dbContext.JournalEntries.AsNoTracking().Where(e => e.Date >= from && e.Date <= to),
                l => l.JournalEntryId,
                e => e.Id,
                (l, e) =>
                    new
                    {
                        e.Id,
                        e.Date,
                        e.CounterpartyId,
                        l.AccountId,
                        l.Amount,
                    }
            )
            .ToListAsync(cancellationToken);
        if (liquidLines.Count == 0)
            return [];

        var entryIds = liquidLines.Select(l => l.Id).Distinct().ToList();

        // Every counter-leg of those entries, with its account type — to find the P&L leg.
        var counterLegs = await dbContext
            .JournalLines.AsNoTracking()
            .Where(l => entryIds.Contains(l.JournalEntryId))
            .Join(
                dbContext.Accounts.AsNoTracking(),
                l => l.AccountId,
                a => a.Id,
                (l, a) =>
                    new
                    {
                        l.JournalEntryId,
                        l.AccountId,
                        a.AccountType,
                    }
            )
            .ToListAsync(cancellationToken);

        // The SEPA matching key travels on the source BankTransaction, not the entry.
        var sepaByEntry = await dbContext
            .BankTransactions.AsNoTracking()
            .Where(b => b.JournalEntryId != null && entryIds.Contains(b.JournalEntryId.Value))
            .Select(b => new
            {
                EntryId = b.JournalEntryId!.Value,
                b.MandateId,
                b.SepaCreditorId,
            })
            .ToListAsync(cancellationToken);

        var pnlLegByEntry = counterLegs
            .Where(l => l.AccountType is AccountType.Income or AccountType.Expense)
            .GroupBy(l => l.JournalEntryId)
            .ToDictionary(g => g.Key, g => g.First().AccountId);
        var sepaLookup = sepaByEntry
            .GroupBy(s => s.EntryId)
            .ToDictionary(g => g.Key, g => g.First());

        var occurrences = new List<LedgerOccurrence>(liquidLines.Count);
        foreach (var line in liquidLines)
        {
            // No P&L leg ⇒ self-transfer ⇒ not Typical spend, not a detectable bill.
            if (!pnlLegByEntry.TryGetValue(line.Id, out var counterAccountId))
                continue;

            sepaLookup.TryGetValue(line.Id, out var sepa);
            occurrences.Add(
                new LedgerOccurrence(
                    line.AccountId,
                    line.Amount,
                    line.Date,
                    line.CounterpartyId,
                    counterAccountId,
                    sepa?.MandateId,
                    sepa?.SepaCreditorId
                )
            );
        }

        return occurrences;
    }
}

/// <summary>
/// One P&L-touching line on a liquid account, with the signals the layered matching key uses
/// (ADR-0027): the SEPA mandate/creditor when present, else the counterparty and the P&L
/// counter-account. <see cref="Amount"/> is the raw ledger amount on the liquid account.
/// </summary>
internal sealed record LedgerOccurrence(
    AccountId AccountId,
    long Amount,
    DateOnly Date,
    CounterpartyId? CounterpartyId,
    AccountId? CounterAccountId,
    string? MandateId,
    string? SepaCreditorId
);
