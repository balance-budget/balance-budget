using Balance.Data;
using Balance.Data.Entities.Ids;
using Balance.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Balance.Services.Loans;

/// <summary>
/// The shared loan-managed invariant (ADR-0025): an account that belongs to a Loan Part (or is
/// a Loan's parent) is only a valid posting target inside loan-aware flows, so the part balance
/// always equals what the loan did. Status is derived from the Loan/LoanPart account linkage,
/// never a flag to keep in sync. Consumed by journal-entry create/edit, reassign, and plain
/// categorization.
/// </summary>
internal static class LoanManagedAccounts
{
    /// <summary>Returns the subset of <paramref name="accountIds"/> that is loan-managed.</summary>
    public static async Task<IReadOnlyCollection<AccountId>> FindLoanManagedAsync(
        BalanceDbContext dbContext,
        IReadOnlyCollection<AccountId> accountIds,
        CancellationToken cancellationToken
    )
    {
        if (accountIds.Count == 0)
            return [];

        var ids = accountIds.Distinct().ToList();
        var managed = await dbContext
            .LoanParts.AsNoTracking()
            .Where(p => ids.Contains(p.AccountId))
            .Select(p => p.AccountId)
            .Union(
                dbContext
                    .Loans.AsNoTracking()
                    .Where(l => ids.Contains(l.ParentAccountId))
                    .Select(l => l.ParentAccountId)
            )
            .ToListAsync(cancellationToken);
        return managed;
    }

    public static InvariantError Refusal(AccountId accountId) =>
        new(
            ErrorCodes.AccountLoanManaged,
            $"Account {accountId.Value} is loan-managed; only loan-aware flows can post to it."
        );

    /// <summary>
    /// Validates the loan rules for a draft line set: a line on a loan-managed account must be
    /// attributed to the part owning that account (that attribution is what makes the flow
    /// loan-aware); an attributed line may only target the part's account or the loan's interest
    /// Expense account; the loan's parent account is never postable through any flow.
    /// </summary>
    public static async Task<Result> ValidateLineAttributionsAsync(
        BalanceDbContext dbContext,
        IReadOnlyList<CreateJournalLineInput> lines,
        CancellationToken cancellationToken
    )
    {
        var accountIds = lines.Select(l => l.AccountId).Distinct().ToList();
        var referencedPartIds = lines
            .Where(l => l.LoanPartId.HasValue)
            .Select(l => l.LoanPartId!.Value)
            .Distinct()
            .ToList();

        if (referencedPartIds.Count == 0)
        {
            var managed = await FindLoanManagedAsync(dbContext, accountIds, cancellationToken);
            return managed.Count > 0 ? Refusal(managed.First()) : Result.Success;
        }

        var parts = await dbContext
            .LoanParts.AsNoTracking()
            .Where(p => referencedPartIds.Contains(p.Id) || accountIds.Contains(p.AccountId))
            .Join(
                dbContext.Loans.AsNoTracking(),
                p => p.LoanId,
                l => l.Id,
                (p, l) =>
                    new
                    {
                        p.Id,
                        p.AccountId,
                        l.InterestExpenseAccountId,
                    }
            )
            .ToListAsync(cancellationToken);
        var partsById = parts.ToDictionary(p => p.Id);
        var partsByAccount = parts.ToDictionary(p => p.AccountId);

        var managedParents = await dbContext
            .Loans.AsNoTracking()
            .Where(l => accountIds.Contains(l.ParentAccountId))
            .Select(l => l.ParentAccountId)
            .ToListAsync(cancellationToken);
        if (managedParents.Count > 0)
            return Refusal(managedParents[0]);

        foreach (var line in lines)
        {
            if (line.LoanPartId is not { } partId)
            {
                if (partsByAccount.ContainsKey(line.AccountId))
                    return Refusal(line.AccountId);

                continue;
            }

            if (!partsById.TryGetValue(partId, out var part))
                return new NotFoundError("LoanPart", partId.Value.ToString());

            // The only valid targets for an attributed line: the part's own account (principal,
            // also covering an attribution to a *different* part's account) or the loan's
            // interest Expense account (interest, prepayment penalty).
            if (line.AccountId != part.AccountId && line.AccountId != part.InterestExpenseAccountId)
            {
                return new InvariantError(
                    ErrorCodes.LoanPartAttributionInvalid,
                    $"JournalLine attributed to LoanPart {partId.Value} must post to that "
                        + "part's account or the loan's interest expense account."
                );
            }
        }

        return Result.Success;
    }
}
