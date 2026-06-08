using Balance.Data;
using Balance.Data.Entities;
using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Balance.Services.Loans;

internal sealed class LoanProjectionService : ILoanProjectionService
{
    private readonly BalanceDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public LoanProjectionService(BalanceDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<Result<LoanPaymentProposalOutput>> GetPaymentProposalAsync(
        LoanId id,
        DateOnly month,
        CancellationToken cancellationToken
    )
    {
        var graphResult = await LoadAsync(id, cancellationToken);
        if (graphResult.IsFailure)
            return graphResult.Error;
        var graph = graphResult.Value!;

        // Categorising a row from the current (or a recent) month means its payment has not
        // reduced the balance yet, so anchoring the engine at that month on today's balance is
        // exactly the proposal. For a future month, project forward from today instead.
        var requested = FirstOfMonth(month);
        var anchor = requested < graph.CurrentMonth ? requested : graph.CurrentMonth;
        var rows = AmortizationEngine.Project(graph.Specs, anchor);

        var lines = new List<LoanPaymentProposalLineOutput>();
        foreach (var part in graph.Parts)
        {
            var row = rows.FirstOrDefault(r => r.PartId == part.Part.Id && r.Period == requested);
            if (row is null)
                continue;

            lines.Add(
                new LoanPaymentProposalLineOutput(
                    part.Part.Id,
                    part.Part.Label,
                    part.Part.AccountId,
                    row.Interest,
                    row.Principal,
                    row.Payment
                )
            );
        }

        // Deposit-interest offset (ADR-0026): deposit balance × monthly rate, capped at the gross
        // interest, credited to the income account so the entry's net matches the bank's debit.
        LoanDepositOffsetOutput? depositOffset = null;
        if (
            graph is { DepositIncomeAccountId: { } incomeAccountId, DepositRate: { } depositRate }
            && graph.DepositBalance > 0
            && depositRate > 0m
        )
        {
            var grossInterest = lines.Sum(l => l.Interest);
            var monthly = (long)
                Math.Round(
                    graph.DepositBalance * depositRate / 100m / 12m,
                    0,
                    MidpointRounding.AwayFromZero
                );
            var amount = Math.Min(Math.Max(0L, monthly), grossInterest);
            if (amount > 0)
                depositOffset = new LoanDepositOffsetOutput(incomeAccountId, amount);
        }

        return new LoanPaymentProposalOutput(
            graph.Loan.Id,
            requested,
            graph.CurrencyCode,
            graph.Loan.InterestExpenseAccountId,
            lines,
            lines.Sum(l => l.Payment),
            depositOffset
        );
    }

    public async Task<Result<LoanProjectionOutput>> GetProjectionAsync(
        LoanId id,
        LoanScenarioInput? scenario,
        CancellationToken cancellationToken
    )
    {
        var graphResult = await LoadAsync(id, cancellationToken);
        if (graphResult.IsFailure)
            return graphResult.Error;
        var graph = graphResult.Value!;

        if (scenario is not null)
        {
            var partIds = graph.Parts.Select(p => p.Part.Id).ToHashSet();
            foreach (var extra in scenario.ExtraRepayments)
            {
                if (!partIds.Contains(extra.LoanPartId))
                    return new NotFoundError("LoanPart", extra.LoanPartId.Value.ToString());
            }
        }

        var actuals = await LoadActualsAsync(graph, cancellationToken);
        var baseline = AmortizationEngine.Project(graph.Specs, graph.CurrentMonth);

        IReadOnlyList<AmortizationPeriodRow>? scenarioRows = null;
        LoanScenarioTotalsOutput? totals = null;
        if (scenario is not null)
        {
            var engineScenario = new AmortizationScenario(
                [
                    .. scenario.ExtraRepayments.Select(e => new AmortizationExtraRepayment(
                        e.LoanPartId,
                        e.Date,
                        e.Amount
                    )),
                ],
                scenario.Policy,
                scenario.AssumedAnnualRatePercent
            );
            scenarioRows = AmortizationEngine.Project(
                graph.Specs,
                graph.CurrentMonth,
                engineScenario
            );
            totals = ComputeTotals(baseline, scenarioRows, scenario);
        }

        return new LoanProjectionOutput(
            graph.Loan.Id,
            graph.CurrencyCode,
            graph.CurrentMonth,
            [
                .. graph.Parts.Select(p => new LoanProjectionPartOutput(
                    p.Part.Id,
                    p.Part.Label,
                    p.Part.AccountId,
                    FixationBoundary(p, graph.CurrentMonth)
                )),
            ],
            actuals,
            [.. baseline.Select(ToRowOutput)],
            scenarioRows is null ? null : [.. scenarioRows.Select(ToRowOutput)],
            totals
        );
    }

    private static LoanPeriodRowOutput ToRowOutput(AmortizationPeriodRow row) =>
        new(
            row.Period,
            row.PartId,
            row.Interest,
            row.Principal,
            row.ExtraRepayment,
            row.Payment,
            row.EndBalance
        );

    private static LoanScenarioTotalsOutput ComputeTotals(
        IReadOnlyList<AmortizationPeriodRow> baseline,
        IReadOnlyList<AmortizationPeriodRow> scenarioRows,
        LoanScenarioInput scenario
    )
    {
        var interestSaved = baseline.Sum(r => r.Interest) - scenarioRows.Sum(r => r.Interest);

        var nextPaymentDelta = 0L;
        if (scenario.ExtraRepayments.Count > 0)
        {
            var firstExtraMonth = scenario.ExtraRepayments.Select(e => FirstOfMonth(e.Date)).Min();
            var compareMonth = firstExtraMonth.AddMonths(1);
            nextPaymentDelta =
                scenarioRows.Where(r => r.Period == compareMonth).Sum(r => r.Payment)
                - baseline.Where(r => r.Period == compareMonth).Sum(r => r.Payment);
        }

        return new LoanScenarioTotalsOutput(
            interestSaved,
            nextPaymentDelta,
            LastActiveMonth(baseline),
            LastActiveMonth(scenarioRows)
        );
    }

    private static DateOnly? LastActiveMonth(IReadOnlyList<AmortizationPeriodRow> rows) =>
        rows.Count == 0 ? null : rows.Max(r => r.Period);

    private static DateOnly? FixationBoundary(LoanPartGraph part, DateOnly anchorMonth)
    {
        // The fixation boundary of the rate in force at the anchor — beyond it the projection
        // is an assumption, not a contract. A boundary already in the past marks nothing.
        var inForce = part
            .RatePeriods.Where(r => r.EffectiveDate <= anchorMonth)
            .OrderByDescending(r => r.EffectiveDate)
            .FirstOrDefault();
        if (inForce?.FixedUntil is not { } fixedUntil || fixedUntil < anchorMonth)
            return null;

        return fixedUntil;
    }

    /// <summary>
    /// Per-part monthly actuals straight from the ledger: principal is the month's net movement
    /// on the part account (debits repay), interest the month's attributed lines elsewhere (the
    /// interest expense account, ADR-0025). Months without postings carry the balance so the
    /// graph is continuous; the range starts where ledger data starts.
    /// </summary>
    private async Task<IReadOnlyList<LoanPeriodRowOutput>> LoadActualsAsync(
        LoanGraph graph,
        CancellationToken cancellationToken
    )
    {
        var partAccountIds = graph.Parts.Select(p => p.Part.AccountId).ToList();
        var partIds = graph.Parts.Select(p => p.Part.Id).ToList();
        var partByAccount = graph.Parts.ToDictionary(p => p.Part.AccountId, p => p.Part.Id);

        var principalLines = await _dbContext
            .JournalLines.AsNoTracking()
            .Where(l => partAccountIds.Contains(l.AccountId))
            .Join(
                _dbContext.JournalEntries.AsNoTracking(),
                l => l.JournalEntryId,
                e => e.Id,
                (l, e) =>
                    new
                    {
                        l.AccountId,
                        l.Amount,
                        e.Date,
                    }
            )
            .ToListAsync(cancellationToken);

        var interestLines = await _dbContext
            .JournalLines.AsNoTracking()
            .Where(l =>
                l.LoanPartId != null
                && partIds.Contains(l.LoanPartId.Value)
                && !partAccountIds.Contains(l.AccountId)
            )
            .Join(
                _dbContext.JournalEntries.AsNoTracking(),
                l => l.JournalEntryId,
                e => e.Id,
                (l, e) =>
                    new
                    {
                        LoanPartId = l.LoanPartId!.Value,
                        l.Amount,
                        e.Date,
                    }
            )
            .ToListAsync(cancellationToken);

        var rows = new List<LoanPeriodRowOutput>();
        foreach (var part in graph.Parts)
        {
            var partId = part.Part.Id;
            var principalByMonth = principalLines
                .Where(l => partByAccount[l.AccountId] == partId)
                .GroupBy(l => FirstOfMonth(l.Date))
                .ToDictionary(g => g.Key, g => g.Sum(l => l.Amount));
            if (principalByMonth.Count == 0)
                continue;

            var interestByMonth = interestLines
                .Where(l => l.LoanPartId == partId)
                .GroupBy(l => FirstOfMonth(l.Date))
                .ToDictionary(g => g.Key, g => g.Sum(l => l.Amount));

            var firstMonth = principalByMonth.Keys.Min();
            var rawBalance = 0L;
            for (var month = firstMonth; month <= graph.CurrentMonth; month = month.AddMonths(1))
            {
                principalByMonth.TryGetValue(month, out var movement);
                interestByMonth.TryGetValue(month, out var interest);
                rawBalance += movement;

                // A debit-positive movement repays principal; the opening/borrowing month shows
                // up as a negative repayment, which is exactly what happened.
                rows.Add(
                    new LoanPeriodRowOutput(
                        month,
                        partId,
                        interest,
                        movement,
                        0L,
                        interest + movement,
                        Math.Max(0L, -rawBalance)
                    )
                );
            }
        }

        return [.. rows.OrderBy(r => r.Period)];
    }

    // ---- shared graph loading -----------------------------------------------------------------

    private sealed record LoanGraph(
        Loan Loan,
        CurrencyCode CurrencyCode,
        IReadOnlyList<LoanPartGraph> Parts,
        IReadOnlyList<AmortizationPartSpec> Specs,
        DateOnly CurrentMonth,
        AccountId? DepositIncomeAccountId,
        decimal? DepositRate,
        long DepositBalance
    );

    private sealed record LoanPartGraph(
        LoanPart Part,
        long OutstandingBalance,
        IReadOnlyList<LoanPartRatePeriod> RatePeriods
    );

    private async Task<Result<LoanGraph>> LoadAsync(LoanId id, CancellationToken cancellationToken)
    {
        var loan = await _dbContext
            .Loans.AsNoTracking()
            .Include(l => l.Parts)
                .ThenInclude(p => p.RatePeriods)
            .AsSplitQuery()
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (loan is null)
            return new NotFoundError("Loan", id.Value.ToString());

        var accountIds = loan.Parts.Select(p => p.AccountId).Append(loan.ParentAccountId).ToList();
        if (loan.ConstructionDepositAccountId is { } depositAccountId)
            accountIds.Add(depositAccountId);
        var accounts = await _dbContext
            .Accounts.AsNoTracking()
            .Where(a => accountIds.Contains(a.Id))
            .Select(a => new
            {
                a.Id,
                a.CurrencyCode,
                Balance = _dbContext
                    .JournalLines.Where(l => l.AccountId == a.Id)
                    .Sum(l => (long?)l.Amount)
                    ?? 0L,
            })
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        var parts = loan
            .Parts.OrderBy(p => p.StartDate)
            .ThenBy(p => p.Label)
            .Select(p => new LoanPartGraph(
                p,
                Math.Max(0L, -accounts[p.AccountId].Balance),
                [.. p.RatePeriods.OrderBy(r => r.EffectiveDate)]
            ))
            .ToList();

        IReadOnlyList<AmortizationPartSpec> specs =
        [
            .. parts.Select(p => new AmortizationPartSpec(
                p.Part.Id,
                p.Part.RepaymentType,
                p.Part.EndDate,
                p.OutstandingBalance,
                [
                    .. p.RatePeriods.Select(r => new AmortizationRatePeriodSpec(
                        r.EffectiveDate,
                        r.AnnualRatePercent,
                        r.FixedUntil
                    )),
                ]
            )),
        ];

        var depositBalance = loan.ConstructionDepositAccountId is { } depId
            ? Math.Max(0L, accounts[depId].Balance)
            : 0L;

        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        return new LoanGraph(
            loan,
            accounts[loan.ParentAccountId].CurrencyCode,
            parts,
            specs,
            FirstOfMonth(today),
            loan.ConstructionDepositInterestIncomeAccountId,
            loan.ConstructionDepositAnnualRatePercent,
            depositBalance
        );
    }

    private static DateOnly FirstOfMonth(DateOnly date) => new(date.Year, date.Month, 1);
}
