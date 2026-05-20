using Balance.Data;
using Balance.Data.Entities;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Services.Accounts;
using Balance.Services.Contracts;
using Balance.Services.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Balance.Services.Dashboard;

internal sealed class DashboardSummaryService : IDashboardSummaryService
{
    private readonly BalanceDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public DashboardSummaryService(BalanceDbContext dbContext, TimeProvider timeProvider)
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
