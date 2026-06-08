using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using FluentValidation;

namespace Balance.Web.Endpoints.Loans;

internal sealed record CreateLoanRequest(
    string Name,
    CounterpartyId LenderCounterpartyId,
    AccountId InterestExpenseAccountId,
    CurrencyCode CurrencyCode,
    string ParentAccountName,
    string ParentAccountCode,
    IReadOnlyList<CreateLoanPartRequest> Parts,
    AccountId? ConstructionDepositAccountId = null,
    AccountId? ConstructionDepositInterestIncomeAccountId = null,
    decimal? ConstructionDepositAnnualRatePercent = null
);

internal sealed record CreateLoanPartRequest(
    string Label,
    LoanRepaymentType RepaymentType,
    DateOnly StartDate,
    DateOnly EndDate,
    AccountId? AdoptAccountId,
    NewLoanPartAccountRequest? NewAccount,
    IReadOnlyList<LoanRatePeriodRequest> RatePeriods
);

internal sealed record NewLoanPartAccountRequest(
    string Name,
    string Code,
    long OpeningBalance,
    DateOnly OpeningDate
);

internal sealed record LoanRatePeriodRequest(
    DateOnly EffectiveDate,
    decimal AnnualRatePercent,
    DateOnly? FixedUntil
);

internal sealed record UpdateLoanPartRequest(
    string Label,
    LoanRepaymentType RepaymentType,
    DateOnly StartDate,
    DateOnly EndDate
);

internal sealed class UpdateLoanPartRequestValidator : AbstractValidator<UpdateLoanPartRequest>
{
    public UpdateLoanPartRequestValidator()
    {
        RuleFor(x => x.Label).NotEmpty().MaximumLength(64);
        RuleFor(x => x.RepaymentType).IsInEnum();
        RuleFor(x => x.StartDate).NotEqual(default(DateOnly));
        RuleFor(x => x.EndDate)
            .GreaterThan(x => x.StartDate)
            .WithMessage("EndDate must be after StartDate.");
    }
}

internal sealed class CreateLoanRequestValidator : AbstractValidator<CreateLoanRequest>
{
    public CreateLoanRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(128);
        RuleFor(x => x.LenderCounterpartyId.Value).NotEqual(Guid.Empty);
        RuleFor(x => x.InterestExpenseAccountId.Value).NotEqual(Guid.Empty);
        RuleFor(x => x.CurrencyCode.Value).NotEmpty().MaximumLength(8);
        RuleFor(x => x.ParentAccountName).NotEmpty().MaximumLength(128);
        RuleFor(x => x.ParentAccountCode).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Parts).NotNull().Must(p => p is { Count: >= 1 });
        RuleForEach(x => x.Parts).SetValidator(new CreateLoanPartRequestValidator());
    }
}

internal sealed class CreateLoanPartRequestValidator : AbstractValidator<CreateLoanPartRequest>
{
    public CreateLoanPartRequestValidator()
    {
        RuleFor(x => x.Label).NotEmpty().MaximumLength(64);
        RuleFor(x => x.RepaymentType).IsInEnum();
        RuleFor(x => x.StartDate).NotEqual(default(DateOnly));
        RuleFor(x => x.EndDate)
            .GreaterThan(x => x.StartDate)
            .WithMessage("EndDate must be after StartDate.");
        RuleFor(x => x)
            .Must(x => x.AdoptAccountId.HasValue != (x.NewAccount is not null))
            .WithMessage("Provide exactly one of AdoptAccountId or NewAccount.")
            .OverridePropertyName(nameof(CreateLoanPartRequest.AdoptAccountId));
        When(
            x => x.NewAccount is not null,
            () =>
            {
                RuleFor(x => x.NewAccount!.Name).NotEmpty().MaximumLength(128);
                RuleFor(x => x.NewAccount!.Code).NotEmpty().MaximumLength(32);
                RuleFor(x => x.NewAccount!.OpeningBalance).GreaterThanOrEqualTo(0L);
                RuleFor(x => x.NewAccount!.OpeningDate).NotEqual(default(DateOnly));
            }
        );
        RuleFor(x => x.RatePeriods).NotNull().Must(r => r is { Count: >= 1 });
        RuleForEach(x => x.RatePeriods).SetValidator(new LoanRatePeriodRequestValidator());
    }
}

internal sealed class LoanRatePeriodRequestValidator : AbstractValidator<LoanRatePeriodRequest>
{
    public LoanRatePeriodRequestValidator()
    {
        RuleFor(x => x.EffectiveDate).NotEqual(default(DateOnly));
        RuleFor(x => x.AnnualRatePercent).InclusiveBetween(0m, 100m);
        RuleFor(x => x.FixedUntil)
            .GreaterThan(x => x.EffectiveDate)
            .When(x => x.FixedUntil.HasValue);
    }
}

internal static class LoanRequestMappers
{
    public static CreateLoanInput ToInput(this CreateLoanRequest request) =>
        new(
            request.Name,
            request.LenderCounterpartyId,
            request.InterestExpenseAccountId,
            request.CurrencyCode,
            request.ParentAccountName,
            request.ParentAccountCode,
            [.. request.Parts.Select(p => p.ToInput())],
            request.ConstructionDepositAccountId,
            request.ConstructionDepositInterestIncomeAccountId,
            request.ConstructionDepositAnnualRatePercent
        );

    public static CreateLoanPartInput ToInput(this CreateLoanPartRequest request) =>
        new(
            request.Label,
            request.RepaymentType,
            request.StartDate,
            request.EndDate,
            request.AdoptAccountId,
            request.NewAccount is null
                ? null
                : new NewLoanPartAccountInput(
                    request.NewAccount.Name,
                    request.NewAccount.Code,
                    request.NewAccount.OpeningBalance,
                    request.NewAccount.OpeningDate
                ),
            [
                .. request.RatePeriods.Select(r => new CreateLoanRatePeriodInput(
                    r.EffectiveDate,
                    r.AnnualRatePercent,
                    r.FixedUntil
                )),
            ]
        );
}
