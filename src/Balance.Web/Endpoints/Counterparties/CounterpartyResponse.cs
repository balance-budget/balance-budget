using Balance.Data.Entities;
using Balance.Data.Entities.Ids;

namespace Balance.Web.Endpoints.Counterparties;

internal sealed record CounterpartyResponse(
    CounterpartyId Id,
    string Name,
    DateTime CreatedAt,
    DateTime UpdatedAt
)
{
    public static CounterpartyResponse From(Counterparty counterparty) =>
        new(counterparty.Id, counterparty.Name, counterparty.CreatedAt, counterparty.UpdatedAt);
}
