using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public interface IAccountSuggestionService
{
    Task<Result<IReadOnlyList<SuggestedCounterAccountOutput>>> GetSuggestedCounterAccountsAsync(
        CounterpartyId counterpartyId,
        CancellationToken cancellationToken
    );
}

public sealed record SuggestedCounterAccountOutput(AccountId AccountId, long Amount);
