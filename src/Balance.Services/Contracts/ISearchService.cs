namespace Balance.Services.Contracts;

public interface ISearchService
{
    Task<SearchOutput> SearchAsync(string query, CancellationToken cancellationToken);
}
