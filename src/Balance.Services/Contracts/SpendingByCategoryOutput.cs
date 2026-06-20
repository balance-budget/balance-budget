using Balance.Data.Entities.Ids;

namespace Balance.Services.Contracts;

/// <summary>
/// This-month spending broken down by leaf Expense account for the dashboard widget (ADR-0030):
/// the top <c>n</c> categories by spend, an <see cref="OtherAmount"/> bucket for the tail, and the
/// <see cref="TotalAmount"/>. Amounts are positive minor units (magnitude of spend); a category
/// whose refunds outweigh its spend for the month is omitted.
/// </summary>
public sealed record SpendingByCategoryOutput(
    IReadOnlyList<SpendingCategorySlice> Slices,
    long OtherAmount,
    long TotalAmount,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    CurrencyCode CurrencyCode
);

public sealed record SpendingCategorySlice(AccountId AccountId, string AccountName, long Amount);
