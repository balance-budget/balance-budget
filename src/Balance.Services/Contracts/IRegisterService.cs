using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public interface IRegisterService
{
    Task<Result<PagedOutput<RegisterRowOutput>>> ListAsync(
        AccountId accountId,
        int skip,
        int take,
        string? search,
        CancellationToken cancellationToken
    );
}
