using System.Diagnostics;
using Balance.Data;
using Balance.Data.Entities;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Services.Accounts;
using Balance.Services.Contracts;
using Balance.Services.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Balance.Services.Dashboard;

internal sealed class DashboardService : IDashboardService
{
    private readonly BalanceDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public DashboardService(BalanceDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<Result<DashboardSummaryOutput>> GetSummaryAsync(
        CurrencyCode currencyCode,
        CancellationToken cancellationToken
    )
    {
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        var periodStart = today.GetMonthStart();
        var periodEnd = today.GetMonthEnd();

        // SPLM (same-period-last-month) window. `DateOnly.AddMonths(-1)` clamps to the
        // prior month's last day when today doesn't exist there (Mar 31 → Feb 28/29),
        // which is the end-of-month edge behavior user story 15 requires.
        var priorPeriodEnd = today.AddMonths(-1);
        var priorPeriodStart = priorPeriodEnd.GetMonthStart();

        var (netWorth, liquidNetWorth) = await ComputeNetWorthAsync(
            currencyCode,
            cancellationToken
        );
        var (income, expenses) = await ComputePeriodTotalsAsync(
            currencyCode,
            periodStart,
            periodEnd,
            cancellationToken
        );
        var (incomePrior, expensesPrior) = await ComputePeriodTotalsAsync(
            currencyCode,
            priorPeriodStart,
            priorPeriodEnd,
            cancellationToken
        );

        return new DashboardSummaryOutput(
            netWorth,
            liquidNetWorth,
            income,
            expenses,
            incomePrior,
            expensesPrior,
            periodStart,
            periodEnd,
            currencyCode
        );
    }

    public async Task<Result<AccountBalanceTrendOutput>> GetAccountBalanceTrendAsync(
        CurrencyCode currencyCode,
        TrendRange range,
        CancellationToken cancellationToken
    )
    {
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        var periodStart = range switch
        {
            TrendRange.OneMonth => today.AddMonths(-1),
            TrendRange.ThreeMonths => today.AddMonths(-3),
            TrendRange.SixMonths => today.AddMonths(-6),
            TrendRange.OneYear => today.AddYears(-1),
            _ => throw new UnreachableException($"Unknown TrendRange '{range}'."),
        };

        // Short- and Medium-term asset accounts only (ADR-0030): these feed the two stacked
        // dashboard charts, split by Horizon. Long-term holdings (a house) would flatten every
        // other series, so they live in the net-worth chart instead, never a per-account stack.
        var assets = await _dbContext
            .Accounts.AsNoTracking()
            .Where(a =>
                a.AccountType == AccountType.Asset
                && a.CurrencyCode == currencyCode
                && (a.Horizon == Horizon.ShortTerm || a.Horizon == Horizon.MediumTerm)
            )
            .Select(a => new
            {
                a.Id,
                a.Name,
                a.Horizon,
            })
            .ToListAsync(cancellationToken);

        if (assets.Count == 0)
        {
            return new AccountBalanceTrendOutput([], periodStart, today, range, currencyCode);
        }

        var assetIds = assets.Select(a => a.Id).ToHashSet();

        // Opening balance per account: one row per asset account, aggregated in SQL.
        var openings = await _dbContext
            .JournalLines.AsNoTracking()
            .Where(l => assetIds.Contains(l.AccountId))
            .Join(
                _dbContext.JournalEntries.AsNoTracking(),
                l => l.JournalEntryId,
                e => e.Id,
                (l, e) =>
                    new
                    {
                        l.AccountId,
                        e.Date,
                        l.Amount,
                    }
            )
            .Where(x => x.Date < periodStart)
            .GroupBy(x => x.AccountId)
            .Select(g => new { AccountId = g.Key, Sum = g.Sum(x => (long?)x.Amount) ?? 0L })
            .ToDictionaryAsync(g => g.AccountId, g => g.Sum, cancellationToken);

        // In-window daily deltas: one row per (account, active-day), aggregated in SQL.
        var deltaRows = await _dbContext
            .JournalLines.AsNoTracking()
            .Where(l => assetIds.Contains(l.AccountId))
            .Join(
                _dbContext.JournalEntries.AsNoTracking(),
                l => l.JournalEntryId,
                e => e.Id,
                (l, e) =>
                    new
                    {
                        l.AccountId,
                        e.Date,
                        l.Amount,
                    }
            )
            .Where(x => x.Date >= periodStart && x.Date <= today)
            .GroupBy(x => new { x.AccountId, x.Date })
            .Select(g => new
            {
                g.Key.AccountId,
                g.Key.Date,
                Sum = g.Sum(x => (long?)x.Amount) ?? 0L,
            })
            .ToListAsync(cancellationToken);

        var deltasByAccount = deltaRows
            .GroupBy(r => r.AccountId)
            .ToDictionary(
                g => g.Key,
                g =>
                    (IReadOnlyList<TrendDelta>)
                        [.. g.OrderBy(r => r.Date).Select(r => new TrendDelta(r.Date, r.Sum))]
            );

        // Skip flat-zero accounts (zero opening *and* no in-window activity). Forward-fill
        // to a daily series is done by the renderer — see the frontend's toAccountTrend.
        var series = assets
            .Select(a => new
            {
                a.Id,
                a.Name,
                a.Horizon,
                Opening = openings.GetValueOrDefault(a.Id),
                Deltas = deltasByAccount.GetValueOrDefault(a.Id, []),
            })
            .Where(a => a.Opening != 0L || a.Deltas.Count > 0)
            .Select(a => new AccountTrendSeries(a.Id, a.Name, a.Horizon, a.Opening, a.Deltas))
            .ToList();

        return new AccountBalanceTrendOutput(series, periodStart, today, range, currencyCode);
    }

    public async Task<Result<NetWorthTrendOutput>> GetNetWorthTrendAsync(
        CurrencyCode currencyCode,
        NetWorthRange range,
        CancellationToken cancellationToken
    )
    {
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        var currentMonthStart = today.GetMonthStart();

        // Asset + liability lines in this currency, dated via their entry. Net worth is the
        // single signed sum over both types' raw amounts (see ComputeNetWorthAsync): liability
        // credit balances are already negative, so summing collapses assets − liabilities.
        var lines = _dbContext
            .JournalLines.AsNoTracking()
            .Join(
                _dbContext.JournalEntries.AsNoTracking(),
                l => l.JournalEntryId,
                e => e.Id,
                (l, e) =>
                    new
                    {
                        l.Amount,
                        l.AccountId,
                        e.Date,
                    }
            )
            .Join(
                _dbContext.Accounts.AsNoTracking(),
                x => x.AccountId,
                a => a.Id,
                (x, a) =>
                    new
                    {
                        x.Amount,
                        x.Date,
                        a.CurrencyCode,
                        a.AccountType,
                        a.IsLiquid,
                    }
            )
            .Where(x =>
                x.CurrencyCode == currencyCode
                && (x.AccountType == AccountType.Asset || x.AccountType == AccountType.Liability)
            );

        var startMonth = range switch
        {
            NetWorthRange.OneYear => currentMonthStart.AddYears(-1),
            NetWorthRange.ThreeYears => currentMonthStart.AddYears(-3),
            NetWorthRange.All => (
                await lines.Select(x => (DateOnly?)x.Date).MinAsync(cancellationToken) ?? today
            ).GetMonthStart(),
            _ => throw new UnreachableException($"Unknown NetWorthRange '{range}'."),
        };

        // Opening net worth before the window, split by liquidity so we can seed both lines.
        var openingRows = await lines
            .Where(x => x.Date < startMonth)
            .GroupBy(x => x.IsLiquid)
            .Select(g => new { IsLiquid = g.Key, Sum = g.Sum(x => (long?)x.Amount) ?? 0L })
            .ToListAsync(cancellationToken);
        var cumLiquid = openingRows.FirstOrDefault(r => r.IsLiquid)?.Sum ?? 0L;
        var cumIlliquid = openingRows.FirstOrDefault(r => !r.IsLiquid)?.Sum ?? 0L;
        var cumAll = checked(cumLiquid + cumIlliquid);

        // In-window daily movement, split by liquidity; aggregated in SQL, walked in memory.
        var deltas = await lines
            .Where(x => x.Date >= startMonth && x.Date <= today)
            .GroupBy(x => new { x.Date, x.IsLiquid })
            .Select(g => new
            {
                g.Key.Date,
                g.Key.IsLiquid,
                Sum = g.Sum(x => (long?)x.Amount) ?? 0L,
            })
            .OrderBy(r => r.Date)
            .ToListAsync(cancellationToken);

        var points = new List<NetWorthPoint>();
        var idx = 0;
        for (var month = startMonth; month <= currentMonthStart; month = month.AddMonths(1))
        {
            // Sample each past month at its end; the current (partial) month as of today.
            var asOf = month == currentMonthStart ? today : month.GetMonthEnd();
            while (idx < deltas.Count && deltas[idx].Date <= asOf)
            {
                cumAll = checked(cumAll + deltas[idx].Sum);
                if (deltas[idx].IsLiquid)
                    cumLiquid = checked(cumLiquid + deltas[idx].Sum);
                idx++;
            }

            points.Add(new NetWorthPoint(asOf, cumAll, cumLiquid));
        }

        return new NetWorthTrendOutput(points, range, currencyCode);
    }

    public async Task<Result<DashboardRegisterPreviewOutput>> GetRegisterPreviewsAsync(
        int rowsPerAccount,
        CancellationToken cancellationToken
    )
    {
        // One correlated query for every postable account's newest lines: EF translates the
        // per-account OrderBy+Take into ROW_NUMBER() OVER (PARTITION BY AccountId), so the
        // dashboard's per-account Register previews cost a single round-trip instead of one
        // register request (and its count/page/sibling queries) per account. Ordering matches
        // the Register: Date DESC then JournalEntryId DESC (ADR-0007).
        var accounts = await _dbContext
            .Accounts.AsNoTracking()
            .Where(a => a.IsPostable)
            .Select(a => new
            {
                a.Id,
                a.AccountType,
                a.CurrencyCode,
                Rows = _dbContext
                    .JournalLines.AsNoTracking()
                    .Where(l => l.AccountId == a.Id)
                    .Join(
                        _dbContext.JournalEntries.AsNoTracking(),
                        l => l.JournalEntryId,
                        e => e.Id,
                        (l, e) => new { Line = l, Entry = e }
                    )
                    .OrderByDescending(x => x.Entry.Date)
                    .ThenByDescending(x => x.Entry.Id)
                    .Take(rowsPerAccount)
                    .Select(x => new
                    {
                        EntryId = x.Entry.Id,
                        LineId = x.Line.Id,
                        x.Entry.Date,
                        EntryDescription = x.Entry.Description,
                        LineDescription = x.Line.Description,
                        CounterpartyName = x.Entry.CounterpartyId == null
                            ? null
                            : _dbContext
                                .Counterparties.Where(c => c.Id == x.Entry.CounterpartyId)
                                .Select(c => c.Name)
                                .FirstOrDefault(),
                        x.Line.Amount,
                    })
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        var output = accounts
            .Where(a => a.Rows.Count > 0)
            .Select(a => new AccountRegisterPreview(
                a.Id,
                [
                    .. a.Rows.Select(r => new RegisterPreviewRow(
                        r.EntryId,
                        r.LineId,
                        r.Date,
                        r.EntryDescription,
                        r.LineDescription,
                        r.CounterpartyName,
                        AccountSignConvention.ToBalance(a.AccountType, r.Amount, a.CurrencyCode)
                    )),
                ]
            ))
            .ToList();

        return new DashboardRegisterPreviewOutput(rowsPerAccount, output);
    }

    // Net Worth = sum(Asset balances) - sum(Liability balances), restricted to the
    // requested currency. Asset balances are +SUM(Amount); Liability balances are
    // -SUM(Amount). Subtracting the latter collapses to a single signed sum over
    // both account types' lines. Liquid net worth is the same sum restricted to
    // liquid accounts, so one GROUP BY IsLiquid query yields both numbers.
    private async Task<(Money NetWorth, Money LiquidNetWorth)> ComputeNetWorthAsync(
        CurrencyCode currencyCode,
        CancellationToken cancellationToken
    )
    {
        var rows = await _dbContext
            .JournalLines.AsNoTracking()
            .Join(
                _dbContext.Accounts.AsNoTracking(),
                l => l.AccountId,
                a => a.Id,
                (l, a) => new { l.Amount, Account = a }
            )
            .Where(x =>
                x.Account.CurrencyCode == currencyCode
                && (
                    x.Account.AccountType == AccountType.Asset
                    || x.Account.AccountType == AccountType.Liability
                )
            )
            .GroupBy(x => x.Account.IsLiquid)
            .Select(g => new { IsLiquid = g.Key, Sum = g.Sum(x => (long?)x.Amount) ?? 0L })
            .ToListAsync(cancellationToken);

        var liquidRaw = rows.FirstOrDefault(r => r.IsLiquid)?.Sum ?? 0L;
        var illiquidRaw = rows.FirstOrDefault(r => !r.IsLiquid)?.Sum ?? 0L;
        return (
            new Money(checked(liquidRaw + illiquidRaw), currencyCode),
            new Money(liquidRaw, currencyCode)
        );
    }

    private async Task<(Money Income, Money Expenses)> ComputePeriodTotalsAsync(
        CurrencyCode currencyCode,
        DateOnly periodStart,
        DateOnly periodEnd,
        CancellationToken cancellationToken
    )
    {
        var rows = await _dbContext
            .JournalLines.AsNoTracking()
            .Join(
                _dbContext.JournalEntries.AsNoTracking(),
                l => l.JournalEntryId,
                e => e.Id,
                (l, e) => new { Line = l, Entry = e }
            )
            .Where(x => x.Entry.Date >= periodStart && x.Entry.Date <= periodEnd)
            .Join(
                _dbContext.Accounts.AsNoTracking(),
                x => x.Line.AccountId,
                a => a.Id,
                (x, a) => new { x.Line, Account = a }
            )
            .Where(x =>
                x.Account.CurrencyCode == currencyCode
                && (
                    x.Account.AccountType == AccountType.Income
                    || x.Account.AccountType == AccountType.Expense
                )
            )
            .GroupBy(x => x.Account.AccountType)
            .Select(g => new { AccountType = g.Key, Sum = g.Sum(x => (long?)x.Line.Amount) ?? 0L })
            .ToListAsync(cancellationToken);

        var incomeRaw = rows.FirstOrDefault(r => r.AccountType == AccountType.Income)?.Sum ?? 0L;
        var expensesRaw = rows.FirstOrDefault(r => r.AccountType == AccountType.Expense)?.Sum ?? 0L;

        // Sign flip to the focal-user perspective (per ADR-0007): positive = money in,
        // negative = money out. Income lines record credits (negative Amount) when money
        // comes in, so flip the sign to render income positive. Expense lines record
        // debits (positive Amount) when money goes out, so flip the sign to render
        // expenses negative.
        var income = new Money(
            AccountSignConvention.IsCreditNormal(AccountType.Income)
                ? checked(-incomeRaw)
                : incomeRaw,
            currencyCode
        );
        var expenses = new Money(checked(-expensesRaw), currencyCode);

        return (income, expenses);
    }
}
