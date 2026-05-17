using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

public sealed record CounterpartyOutput(
    CounterpartyId Id,
    string Name,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
