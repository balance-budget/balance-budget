using Balance.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Balance.Services.Helpers;

internal static class DbContextExtensions
{
    public static Task<Result<int>> SaveChangesAndCatchAsync(
        this DbContext dbContext,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        return DbServiceErrorMapper.ExecuteAndCatch(dbContext.SaveChangesAsync, cancellationToken);
    }
}
