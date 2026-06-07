using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

/// <summary>
/// List shape for the Loans section: the debt position at a glance. All money values are minor
/// units of <see cref="CurrencyCode"/>; <see cref="OutstandingBalance"/> is the positive
/// principal owed (the negated raw Liability balance). <see cref="CurrentPayment"/> is the
/// engine's regular payment for the current month; <see cref="WeightedAnnualRatePercent"/> is
/// balance-weighted across parts and null when nothing is outstanding.
/// </summary>
public sealed record LoanOutput(
    LoanId Id,
    string Name,
    CounterpartyId LenderCounterpartyId,
    string LenderName,
    AccountId InterestExpenseAccountId,
    AccountId ParentAccountId,
    CurrencyCode CurrencyCode,
    long OutstandingBalance,
    long CurrentPayment,
    decimal? WeightedAnnualRatePercent,
    bool IsEnded,
    int PartCount
);

public sealed record LoanDetailOutput(
    LoanId Id,
    string Name,
    CounterpartyId LenderCounterpartyId,
    string LenderName,
    AccountId InterestExpenseAccountId,
    string InterestExpenseAccountName,
    AccountId ParentAccountId,
    CurrencyCode CurrencyCode,
    long OutstandingBalance,
    long CurrentPayment,
    decimal? WeightedAnnualRatePercent,
    bool IsEnded,
    IReadOnlyList<LoanPartOutput> Parts
);

public sealed record LoanPartOutput(
    LoanPartId Id,
    string Label,
    LoanRepaymentType RepaymentType,
    DateOnly StartDate,
    DateOnly EndDate,
    AccountId AccountId,
    string AccountName,
    long OutstandingBalance,
    decimal? CurrentAnnualRatePercent,
    bool IsEnded,
    IReadOnlyList<LoanRatePeriodOutput> RatePeriods
);

public sealed record LoanRatePeriodOutput(
    LoanPartRatePeriodId Id,
    DateOnly EffectiveDate,
    decimal AnnualRatePercent,
    DateOnly? FixedUntil
);
