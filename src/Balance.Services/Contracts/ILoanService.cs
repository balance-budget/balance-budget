using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

/// <summary>
/// CRUD over the Loan domain layered on the ledger (ADR-0025). Creating a loan creates its
/// non-postable Liability parent account and one postable child account per part — fresh (with
/// an opening-balance entry) or adopted (an existing postable Liability leaf re-parented with
/// history intact). The part's account balance is the outstanding principal; the service never
/// stores a competing figure.
/// </summary>
public interface ILoanService
{
    Task<IReadOnlyList<LoanOutput>> ListAsync(CancellationToken cancellationToken);

    Task<Result<LoanDetailOutput>> GetAsync(LoanId id, CancellationToken cancellationToken);

    Task<Result<UpdateLoanInput>> GetSnapshotAsync(LoanId id, CancellationToken cancellationToken);

    Task<Result<LoanDetailOutput>> CreateAsync(
        CreateLoanInput input,
        CancellationToken cancellationToken
    );

    Task<Result<LoanDetailOutput>> UpdateAsync(
        LoanId id,
        UpdateLoanInput input,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Deletes the loan definition (parts and rate history included). The accounts and their
    /// posted history stand untouched — they become ordinary Liability accounts again; line
    /// attributions are cleared by the database (SET NULL).
    /// </summary>
    Task<Result> DeleteAsync(LoanId id, CancellationToken cancellationToken);

    /// <summary>Adds a part to an existing loan — opknippen or borrowing extra (ADR-0025).</summary>
    Task<Result<LoanDetailOutput>> AddPartAsync(
        LoanId id,
        CreateLoanPartInput input,
        CancellationToken cancellationToken
    );

    /// <summary>Appends an effective-dated rate period; history is never overwritten.</summary>
    Task<Result<LoanDetailOutput>> AddRatePeriodAsync(
        LoanId id,
        LoanPartId partId,
        CreateLoanRatePeriodInput input,
        CancellationToken cancellationToken
    );
}

public sealed record CreateLoanInput(
    string Name,
    CounterpartyId LenderCounterpartyId,
    AccountId InterestExpenseAccountId,
    CurrencyCode CurrencyCode,
    string ParentAccountName,
    string ParentAccountCode,
    IReadOnlyList<CreateLoanPartInput> Parts
);

/// <summary>
/// Exactly one of <see cref="AdoptAccountId"/> (existing postable Liability leaf, history kept)
/// or <see cref="NewAccount"/> (fresh leaf plus opening-balance entry) must be set.
/// </summary>
public sealed record CreateLoanPartInput(
    string Label,
    LoanRepaymentType RepaymentType,
    DateOnly StartDate,
    DateOnly EndDate,
    AccountId? AdoptAccountId,
    NewLoanPartAccountInput? NewAccount,
    IReadOnlyList<CreateLoanRatePeriodInput> RatePeriods
);

/// <summary>
/// <see cref="OpeningBalance"/> is the outstanding principal in minor units (positive); the
/// opening entry pairs the part account against the seeded Opening Balances equity account,
/// both legs Reconciled, dated <see cref="OpeningDate"/>.
/// </summary>
public sealed record NewLoanPartAccountInput(
    string Name,
    string Code,
    long OpeningBalance,
    DateOnly OpeningDate
);

public sealed record CreateLoanRatePeriodInput(
    DateOnly EffectiveDate,
    decimal AnnualRatePercent,
    DateOnly? FixedUntil
);

public sealed record UpdateLoanInput
{
    public required string Name { get; set; }
    public required CounterpartyId LenderCounterpartyId { get; set; }
    public required AccountId InterestExpenseAccountId { get; set; }
}
