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

    public async Task<DashboardSummaryOutput> GetSummaryAsync(
        CurrencyCode currencyCode,
        CancellationToken cancellationToken
    )
    {
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        var periodStart = today.GetMonthStart();
        var periodEnd = today.GetMonthEnd();

        // SPLM (same-period-last-month) window. `DateOnly.AddMonths(-1)` clamps to the
        // prior month's last day when today doesn't exist there (Mar 31 → Feb 28/29),
        // which is the end-of-month edge behaviour user story 15 requires.
        var priorPeriodEnd = today.AddMonths(-1);
        var priorPeriodStart = priorPeriodEnd.GetMonthStart();

        var netWorth = await ComputeNetWorthAsync(currencyCode, cancellationToken);
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
            income,
            expenses,
            incomePrior,
            expensesPrior,
            periodStart,
            periodEnd,
            currencyCode
        );
    }

    public async Task<AccountBalanceTrendOutput> GetAccountBalanceTrendAsync(
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
            _ => throw new ArgumentOutOfRangeException(nameof(range), range, "Unknown TrendRange."),
        };

        var assets = await _dbContext
            .Accounts.AsNoTracking()
            .Where(a => a.AccountType == AccountType.Asset && a.CurrencyCode == currencyCode)
            .Select(a => new { a.Id, a.Name })
            .ToListAsync(cancellationToken);

        if (assets.Count == 0)
        {
            return new AccountBalanceTrendOutput(
                Array.Empty<AccountTrendSeries>(),
                periodStart,
                today,
                range,
                currencyCode
            );
        }

        var assetIds = assets.Select(a => a.Id).ToHashSet();

        var rows = await _dbContext
            .JournalLines.AsNoTracking()
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
            .Where(r => assetIds.Contains(r.AccountId) && r.Date <= today)
            .ToListAsync(cancellationToken);

        var byAccount = rows.GroupBy(r => r.AccountId).ToDictionary(g => g.Key, g => g.ToList());

        var series = new List<AccountTrendSeries>();
        foreach (var asset in assets)
        {
            var hasRows = byAccount.TryGetValue(asset.Id, out var accountRows);
            var opening = hasRows
                ? accountRows!.Where(r => r.Date < periodStart).Sum(r => r.Amount)
                : 0L;
            var inWindowByDate = hasRows
                ? accountRows!
                    .Where(r => r.Date >= periodStart)
                    .GroupBy(r => r.Date)
                    .ToDictionary(g => g.Key, g => g.Sum(r => r.Amount))
                : new Dictionary<DateOnly, long>();

            // Asset accounts with zero opening balance *and* no in-window activity are
            // omitted from the series — flat-zero lines add nothing to the chart.
            if (opening == 0L && inWindowByDate.Count == 0)
            {
                continue;
            }

            var points = new List<TrendPoint>();
            var running = opening;
            for (var d = periodStart; d <= today; d = d.AddDays(1))
            {
                if (inWindowByDate.TryGetValue(d, out var delta))
                {
                    running = checked(running + delta);
                }
                points.Add(new TrendPoint(d, new Money(running, currencyCode)));
            }
            series.Add(new AccountTrendSeries(asset.Id, asset.Name, points));
        }

        return new AccountBalanceTrendOutput(series, periodStart, today, range, currencyCode);
    }

    // Net Worth = sum(Asset balances) - sum(Liability balances), restricted to the
    // requested currency. Asset balances are +SUM(Amount); Liability balances are
    // -SUM(Amount). Subtracting the latter collapses to a single signed sum over
    // both account types' lines.
    private async Task<Money> ComputeNetWorthAsync(
        CurrencyCode currencyCode,
        CancellationToken cancellationToken
    )
    {
        var raw =
            await _dbContext
                .JournalLines.AsNoTracking()
                .Where(l =>
                    _dbContext.Accounts.Any(a =>
                        a.Id == l.AccountId
                        && a.CurrencyCode == currencyCode
                        && (
                            a.AccountType == AccountType.Asset
                            || a.AccountType == AccountType.Liability
                        )
                    )
                )
                .SumAsync(l => (long?)l.Amount, cancellationToken)
            ?? 0L;
        return new Money(raw, currencyCode);
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

        // Sign flip to the focal-user perspective (per ADR-0008): positive = money in,
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
