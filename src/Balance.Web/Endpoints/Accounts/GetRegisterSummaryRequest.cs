using Balance.Services.Contracts;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Balance.Web.Endpoints.Accounts;

internal sealed record GetRegisterSummaryRequest(
    [FromQuery] DateOnly? From,
    [FromQuery] DateOnly? To,
    [FromQuery] RegisterSummaryBucket? Bucket
)
{
    /// <summary>
    /// Upper bound on the number of buckets a single request may span — the client picks the
    /// bucket size to match the range (Register summary, CONTEXT.md), so anything past this is a
    /// mismatched range/bucket pair rather than a legitimate chart.
    /// </summary>
    public const int MaxBuckets = 400;
}

internal sealed class GetRegisterSummaryRequestValidator
    : AbstractValidator<GetRegisterSummaryRequest>
{
    public GetRegisterSummaryRequestValidator()
    {
        RuleFor(x => x.From).NotNull();
        RuleFor(x => x.To).NotNull();
        RuleFor(x => x.Bucket).NotNull();
        RuleFor(x => x.To!.Value)
            .GreaterThanOrEqualTo(x => x.From!.Value)
            .When(x => x.From is not null && x.To is not null);
        RuleFor(x => x)
            .Must(x => BucketCount(x.From!.Value, x.To!.Value, x.Bucket!.Value) <= MaxBuckets)
            .WithName(nameof(GetRegisterSummaryRequest.Bucket))
            .WithMessage(
                $"The date range spans more than {MaxBuckets} buckets; use a coarser bucket."
            )
            .When(x => x.From is not null && x.To is not null && x.Bucket is not null);
    }

    private const int MaxBuckets = GetRegisterSummaryRequest.MaxBuckets;

    private static int BucketCount(DateOnly from, DateOnly to, RegisterSummaryBucket bucket)
    {
        var days = to.DayNumber - from.DayNumber + 1;
        return bucket switch
        {
            RegisterSummaryBucket.Day => days,
            RegisterSummaryBucket.Week => (days + 6) / 7 + 1,
            RegisterSummaryBucket.Month => (to.Year - from.Year) * 12 + to.Month - from.Month + 1,
            _ => int.MaxValue,
        };
    }
}
