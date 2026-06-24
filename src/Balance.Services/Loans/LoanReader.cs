using Balance.Data;
using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Balance.Services.Loans;

/// <summary>
/// The loan read side: loads each loan together with its parts, rate periods,
/// account balances, and lender, then projects the graph into the list and
/// detail output DTOs (current payment, weighted rate, amortization summary).
/// Split out of <see cref="LoanService"/> so the mutating command flow and this
/// query/projection flow stay independently readable.
/// </summary>
internal sealed class LoanReader
{
    private readonly BalanceDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public LoanReader(BalanceDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<LoanOutput>> ListAsync(CancellationToken cancellationToken)
    {
        var graphs = await LoadGraphsAsync(loanId: null, cancellationToken);
        return [.. graphs.Select(ToListOutput)];
    }

    public async Task<LoanDetailOutput?> GetDetailAsync(
        LoanId id,
        CancellationToken cancellationToken
    )
    {
        var graphs = await LoadGraphsAsync(id, cancellationToken);
        return graphs.Count == 0 ? null : ToDetailOutput(graphs[0]);
    }

    private sealed record LoanGraph(
        Loan Loan,
        string LenderName,
        string InterestAccountName,
        CurrencyCode CurrencyCode,
        IReadOnlyList<LoanPartGraph> Parts,
        DateOnly Today,
        LoanConstructionDepositOutput? Deposit
    );

    private sealed record LoanPartGraph(
        LoanPart Part,
        string AccountName,
        long OutstandingBalance,
        IReadOnlyList<LoanPartRatePeriod> RatePeriods
    );

    private async Task<List<LoanGraph>> LoadGraphsAsync(
        LoanId? loanId,
        CancellationToken cancellationToken
    )
    {
        var loansQuery = _dbContext
            .Loans.AsNoTracking()
            .Include(l => l.Parts)
                .ThenInclude(p => p.RatePeriods)
            .AsSplitQuery();
        if (loanId is { } id)
            loansQuery = loansQuery.Where(l => l.Id == id);

        var loans = await loansQuery.OrderBy(l => l.Name).ToListAsync(cancellationToken);
        if (loans.Count == 0)
            return [];

        var accountIds = loans
            .SelectMany(l => l.Parts.Select(p => p.AccountId))
            .Concat(loans.Select(l => l.ParentAccountId))
            .Concat(loans.Select(l => l.InterestExpenseAccountId))
            .Concat(
                loans
                    .Where(l => l.ConstructionDepositAccountId is not null)
                    .Select(l => l.ConstructionDepositAccountId!.Value)
            )
            .Concat(
                loans
                    .Where(l => l.ConstructionDepositInterestIncomeAccountId is not null)
                    .Select(l => l.ConstructionDepositInterestIncomeAccountId!.Value)
            )
            .Distinct()
            .ToList();
        var accounts = await _dbContext
            .Accounts.AsNoTracking()
            .Where(a => accountIds.Contains(a.Id))
            .Select(a => new
            {
                a.Id,
                a.Name,
                a.CurrencyCode,
                Balance = _dbContext
                    .JournalLines.Where(l => l.AccountId == a.Id)
                    .Sum(l => (long?)l.Amount)
                    ?? 0L,
            })
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        var counterpartyIds = loans.Select(l => l.LenderCounterpartyId).Distinct().ToList();
        var lenders = await _dbContext
            .Counterparties.AsNoTracking()
            .Where(c => counterpartyIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        var graphs = new List<LoanGraph>(loans.Count);
        foreach (var loan in loans)
        {
            var parts = loan
                .Parts.OrderBy(p => p.StartDate)
                .ThenBy(p => p.Label)
                .Select(p => new LoanPartGraph(
                    p,
                    accounts[p.AccountId].Name,
                    // A liability's raw line sum is a credit (negative); outstanding principal
                    // is its negation, floored at zero for display sanity.
                    Math.Max(0L, -accounts[p.AccountId].Balance),
                    [.. p.RatePeriods.OrderBy(r => r.EffectiveDate)]
                ))
                .ToList();

            LoanConstructionDepositOutput? deposit = null;
            if (
                loan.ConstructionDepositAccountId is { } depositId
                && loan.ConstructionDepositInterestIncomeAccountId is { } incomeId
                && loan.ConstructionDepositAnnualRatePercent is { } depositRate
            )
            {
                deposit = new LoanConstructionDepositOutput(
                    depositId,
                    accounts[depositId].Name,
                    incomeId,
                    accounts[incomeId].Name,
                    depositRate,
                    // An Asset's raw line sum is its (debit-normal) balance; floor at zero.
                    Math.Max(0L, accounts[depositId].Balance)
                );
            }

            graphs.Add(
                new LoanGraph(
                    loan,
                    lenders[loan.LenderCounterpartyId],
                    accounts[loan.InterestExpenseAccountId].Name,
                    accounts[loan.ParentAccountId].CurrencyCode,
                    parts,
                    today,
                    deposit
                )
            );
        }

        return graphs;
    }

    // The deposit-interest offset for one month (ADR-0026): deposit balance × monthly rate,
    // capped at the month's gross interest so the proposal never goes negative.
    private static long DepositOffsetForMonth(
        LoanConstructionDepositOutput? deposit,
        long grossInterest
    )
    {
        if (deposit is null || deposit.Balance <= 0 || deposit.AnnualRatePercent <= 0m)
            return 0L;

        var monthly = (long)
            Math.Round(
                deposit.Balance * deposit.AnnualRatePercent / 100m / 12m,
                0,
                MidpointRounding.AwayFromZero
            );
        return Math.Min(Math.Max(0L, monthly), Math.Max(0L, grossInterest));
    }

    private static LoanOutput ToListOutput(LoanGraph graph)
    {
        var summary = Summarize(graph);
        return new LoanOutput(
            graph.Loan.Id,
            graph.Loan.Name,
            graph.Loan.LenderCounterpartyId,
            graph.LenderName,
            graph.Loan.InterestExpenseAccountId,
            graph.Loan.ParentAccountId,
            graph.CurrencyCode,
            summary.OutstandingBalance,
            summary.CurrentPayment,
            summary.WeightedAnnualRatePercent,
            summary.IsEnded,
            graph.Parts.Count
        );
    }

    private static LoanDetailOutput ToDetailOutput(LoanGraph graph)
    {
        var summary = Summarize(graph);
        return new LoanDetailOutput(
            graph.Loan.Id,
            graph.Loan.Name,
            graph.Loan.LenderCounterpartyId,
            graph.LenderName,
            graph.Loan.InterestExpenseAccountId,
            graph.InterestAccountName,
            graph.Loan.ParentAccountId,
            graph.CurrencyCode,
            summary.OutstandingBalance,
            summary.CurrentPayment,
            summary.WeightedAnnualRatePercent,
            summary.IsEnded,
            [
                .. graph.Parts.Select(p => new LoanPartOutput(
                    p.Part.Id,
                    p.Part.Label,
                    p.Part.RepaymentType,
                    p.Part.StartDate,
                    p.Part.EndDate,
                    p.Part.AccountId,
                    p.AccountName,
                    p.OutstandingBalance,
                    CurrentRate(p, graph.Today),
                    IsPartEnded(p, graph.Today),
                    [
                        .. p.RatePeriods.Select(r => new LoanRatePeriodOutput(
                            r.Id,
                            r.EffectiveDate,
                            r.AnnualRatePercent,
                            r.FixedUntil
                        )),
                    ]
                )),
            ],
            graph.Deposit
        );
    }

    private static (
        long OutstandingBalance,
        long CurrentPayment,
        decimal? WeightedAnnualRatePercent,
        bool IsEnded
    ) Summarize(LoanGraph graph)
    {
        var outstanding = graph.Parts.Sum(p => p.OutstandingBalance);

        var currentMonth = new DateOnly(graph.Today.Year, graph.Today.Month, 1);
        var projection = AmortizationEngine.Project(
            [.. graph.Parts.Select(p => ToSpec(p))],
            currentMonth
        );
        var currentRows = projection.Where(r => r.Period == currentMonth).ToList();
        // Net of the deposit-interest offset (ADR-0026): the headline payment matches the single
        // netted debit the lender collects during construction; gross once the deposit drains.
        var grossInterest = currentRows.Sum(r => r.Interest);
        var currentPayment =
            currentRows.Sum(r => r.Payment) - DepositOffsetForMonth(graph.Deposit, grossInterest);

        decimal? weightedRate = null;
        if (outstanding > 0)
        {
            var weighted = graph.Parts.Sum(p =>
                (CurrentRate(p, graph.Today) ?? 0m) * p.OutstandingBalance
            );
            weightedRate = Math.Round(weighted / outstanding, 4);
        }

        var isEnded = graph.Parts.All(p => IsPartEnded(p, graph.Today));
        return (outstanding, currentPayment, weightedRate, isEnded);
    }

    private static bool IsPartEnded(LoanPartGraph part, DateOnly today) =>
        part.OutstandingBalance == 0L || part.Part.EndDate < today;

    private static decimal? CurrentRate(LoanPartGraph part, DateOnly today)
    {
        var inForce = part
            .RatePeriods.Where(r => r.EffectiveDate <= today)
            .OrderByDescending(r => r.EffectiveDate)
            .FirstOrDefault();
        inForce ??= part.RatePeriods.Count > 0 ? part.RatePeriods[0] : null;
        return inForce?.AnnualRatePercent;
    }

    private static AmortizationPartSpec ToSpec(LoanPartGraph part) =>
        new(
            part.Part.Id,
            part.Part.RepaymentType,
            part.Part.EndDate,
            part.OutstandingBalance,
            [
                .. part.RatePeriods.Select(r => new AmortizationRatePeriodSpec(
                    r.EffectiveDate,
                    r.AnnualRatePercent,
                    r.FixedUntil
                )),
            ]
        );
}
