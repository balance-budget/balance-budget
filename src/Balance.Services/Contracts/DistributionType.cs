namespace Balance.Services.Contracts;

/// <summary>
/// Which <see cref="Balance.Data.Entities.Enums.AccountType"/> family a <c>Distribution</c> report
/// breaks down: <see cref="Income"/> ("where money came from") or <see cref="Expense"/> ("where money
/// went"). The two values map 1:1 onto the corresponding <c>AccountType</c>.
/// </summary>
public enum DistributionType
{
    Income,
    Expense,
}
