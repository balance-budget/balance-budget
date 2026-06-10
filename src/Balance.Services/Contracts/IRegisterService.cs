using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public interface IRegisterService
{
    Task<Result<PagedOutput<RegisterRowOutput>>> ListAsync(
        AccountId accountId,
        int skip,
        int take,
        RegisterListFilter filter,
        CancellationToken cancellationToken
    );

    Task<Result<RegisterSummaryOutput>> SummarizeAsync(
        AccountId accountId,
        DateOnly fromDate,
        DateOnly toDate,
        RegisterSummaryBucket bucket,
        CancellationToken cancellationToken
    );
}
