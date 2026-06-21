using Balance.Data.Entities;
using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public sealed record CurrencyOutput(
    CurrencyCode Code,
    string Name,
    int MinorUnitScale,
    string? Symbol
)
{
    /// <summary>
    /// Number of <see cref="Account" />s referencing this currency. Together with
    /// <see cref="BankAccountCount" /> this is the delete guard: a currency is deletable only when
    /// both are zero, in lockstep with the FK <c>RESTRICT</c> the database enforces. Populated by
    /// <c>ListAsync</c>; defaults to 0 on the single-row outputs (create/update), which the client
    /// refetches rather than trusting these fields.
    /// </summary>
    public int AccountCount { get; init; }

    /// <summary>
    /// Number of <see cref="BankAccount" />s referencing this currency. See
    /// <see cref="AccountCount" />.
    /// </summary>
    public int BankAccountCount { get; init; }

    public static CurrencyOutput FromEntity(Currency currency)
    {
        ArgumentNullException.ThrowIfNull(currency);
        return new CurrencyOutput(
            currency.Code,
            currency.Name,
            currency.MinorUnitScale,
            currency.Symbol
        );
    }
}

public sealed record CreateCurrencyInput(
    CurrencyCode Code,
    string Name,
    int MinorUnitScale,
    string? Symbol
);

public sealed record UpdateCurrencyInput
{
    public required string Name { get; set; }
    public string? Symbol { get; set; }
}
