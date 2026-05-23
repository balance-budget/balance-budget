using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public interface IRegisterService
{
    Task<Result<IReadOnlyList<RegisterRowOutput>>> ListAsync(
        AccountId accountId,
        int skip,
        int take,
        CancellationToken cancellationToken
    );
}
