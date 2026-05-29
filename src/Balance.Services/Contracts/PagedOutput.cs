namespace Balance.Services.Contracts;

public sealed record PagedOutput<T>(IReadOnlyList<T> Items, int TotalCount);
