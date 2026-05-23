namespace Balance.Services.Contracts;

public interface IBankTransactionImporter
{
    public Task ImportAsync(CancellationToken cancellationToken);
}
