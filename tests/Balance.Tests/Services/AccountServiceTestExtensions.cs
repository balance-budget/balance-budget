using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;

namespace Balance.Tests.Services;

/// <summary>
/// Convenience overload mirroring the pre-ADR-0019 <c>CreateAsync(name, type, currency, ct)</c>
/// shape for the many service tests that only care about a flat, postable account. Auto-assigns a
/// unique <c>Code</c> (now required and globally unique) so callers don't have to thread one
/// through. Tests that exercise the tree itself build a <see cref="CreateAccountInput"/> directly.
/// </summary>
internal static class AccountServiceTestExtensions
{
    public static Task<Result<AccountOutput>> CreateAsync(
        this IAccountService service,
        string name,
        AccountType accountType,
        CurrencyCode currencyCode,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(service);
        return service.CreateAsync(
            new CreateAccountInput
            {
                Name = name,
                Code = $"T{Guid.NewGuid():N}"[..16],
                AccountType = accountType,
                CurrencyCode = currencyCode,
            },
            cancellationToken
        );
    }
}
