using Balance.Data;
using Balance.Data.Entities;
using Balance.Data.Entities.Enums;
using Balance.Data.Entities.Ids;
using Balance.Services.Accounts;
using Balance.Services.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Balance.Services.Reports;

/// <summary>
/// Server-shaped projections for the Insights reports (CONTEXT.md). Both reports build on one cheap
/// aggregate — each account's raw <c>SUM(JournalLine.Amount)</c> over the period — then apply the
/// sign convention (ADR-0012) and the chart-of-accounts rollup in memory. See ADR-0023 for the
/// Money flow model.
/// </summary>
internal sealed class ReportsService : IReportsService
{
    private const string HubId = "hub";

    private readonly BalanceDbContext _dbContext;

    public ReportsService(BalanceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<DistributionOutput>> GetDistributionAsync(
        DistributionType type,
        AccountId? parentAccountId,
        DateOnly fromDate,
        DateOnly toDate,
        CurrencyCode currencyCode,
        CancellationToken cancellationToken
    )
    {
        if (fromDate > toDate)
            return InvalidRange();

        var accountType =
            type == DistributionType.Income ? AccountType.Income : AccountType.Expense;

        // A subtree shares one AccountType and Currency (the homogeneity rule), so filtering to the
        // requested type + currency captures whole trees with no parent dangling outside the set.
        var accounts = await _dbContext
            .Accounts.AsNoTracking()
            .Where(a => a.AccountType == accountType && a.CurrencyCode == currencyCode)
            .Select(a => new AccountNode(a.Id, a.Name, a.Code, a.AccountType, a.ParentAccountId))
            .ToListAsync(cancellationToken);

        var byId = accounts.ToDictionary(a => a.Id);
        var childrenByParent = BuildChildrenMap(accounts);

        if (parentAccountId is { } parentId && !byId.ContainsKey(parentId))
            return new NotFoundError(nameof(Account), parentId.Value.ToString());

        var ownRaw = await ComputeRawSumsAsync(byId.Keys, fromDate, toDate, cancellationToken);
        var subtreeRaw = RollUp(byId, ownRaw);

        var levelIds = parentAccountId is { } pid
            ? childrenByParent.GetValueOrDefault(pid, [])
            : accounts.Where(a => a.ParentAccountId is null).Select(a => a.Id).ToList();

        var slices = levelIds
            .Select(id => byId[id])
            .Select(a => new DistributionSlice(
                a.Id,
                a.Name,
                a.Code,
                AccountSignConvention.ToBalance(
                    accountType,
                    subtreeRaw.GetValueOrDefault(a.Id),
                    currencyCode
                ),
                childrenByParent.ContainsKey(a.Id)
            ))
            // Largest first; net-negative slices fall to the end by their (negative) amount.
            .OrderByDescending(s => s.Amount.Amount)
            .ToList();

        var totalRaw = levelIds.Sum(id => subtreeRaw.GetValueOrDefault(id));
        var total = AccountSignConvention.ToBalance(accountType, totalRaw, currencyCode);

        return new DistributionOutput(
            type,
            fromDate,
            toDate,
            currencyCode,
            parentAccountId,
            total,
            slices
        );
    }

    public async Task<Result<MoneyFlowOutput>> GetMoneyFlowAsync(
        DateOnly fromDate,
        DateOnly toDate,
        CurrencyCode currencyCode,
        IReadOnlySet<AccountId> expandedAccountIds,
        CancellationToken cancellationToken
    )
    {
        if (fromDate > toDate)
            return InvalidRange();

        var accounts = await _dbContext
            .Accounts.AsNoTracking()
            .Where(a => a.CurrencyCode == currencyCode)
            .Select(a => new AccountNode(a.Id, a.Name, a.Code, a.AccountType, a.ParentAccountId))
            .ToListAsync(cancellationToken);

        var byId = accounts.ToDictionary(a => a.Id);
        var childrenByParent = BuildChildrenMap(accounts);

        var ownRaw = await ComputeRawSumsAsync(byId.Keys, fromDate, toDate, cancellationToken);
        var subtreeRaw = RollUp(byId, ownRaw);

        // Drive everything off the raw signed line sum. Within one currency the signed sums net to
        // zero across all accounts (double-entry), so sources and exits balance for free:
        //   rawSum < 0 (net credit) → source, links node → hub
        //   rawSum > 0 (net debit)  → exit,   links hub → node
        var builder = new FlowBuilder(
            byId,
            childrenByParent,
            ownRaw,
            subtreeRaw,
            currencyCode,
            expandedAccountIds
        );

        // Income / Expense: chart-of-accounts hierarchy, subtrees become intermediate nodes. Every
        // root renders one hop from the hub; the recursion descends into a node only when it is in
        // the expanded set, otherwise the node collapses (its whole subtree folds into it).
        foreach (
            var root in accounts.Where(a =>
                IsIncomeOrExpense(a.AccountType) && a.ParentAccountId is null
            )
        )
        {
            var drawn = builder.BuildSubtree(root.Id);
            builder.Link(HubId, root.Id, drawn);
        }

        // Asset / Liability / Equity: top-level only (v1). Roll the whole subtree into the root node
        // and emit one hub link by sign — a grown asset / paid-down liability is an exit, a drained
        // asset / new borrowing is a source.
        foreach (
            var root in accounts.Where(a =>
                IsBalanceSheet(a.AccountType) && a.ParentAccountId is null
            )
        )
        {
            builder.Link(HubId, root.Id, subtreeRaw.GetValueOrDefault(root.Id));
        }

        var (nodes, links) = builder.Build();
        return new MoneyFlowOutput(fromDate, toDate, currencyCode, nodes, links);
    }

    // Per-account raw SUM(Amount) over the period, restricted to the supplied account set (already
    // currency/type filtered by the caller). Aggregated in SQL; one row per account with activity.
    private async Task<Dictionary<AccountId, long>> ComputeRawSumsAsync(
        IEnumerable<AccountId> accountIds,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken
    )
    {
        var ids = accountIds.ToHashSet();
        if (ids.Count == 0)
            return [];

        var rows = await _dbContext
            .JournalLines.AsNoTracking()
            .Where(l => ids.Contains(l.AccountId))
            .Join(
                _dbContext.JournalEntries.AsNoTracking(),
                l => l.JournalEntryId,
                e => e.Id,
                (l, e) =>
                    new
                    {
                        l.AccountId,
                        e.Date,
                        l.Amount,
                    }
            )
            .Where(x => x.Date >= from && x.Date <= to)
            .GroupBy(x => x.AccountId)
            .Select(g => new { AccountId = g.Key, Sum = g.Sum(x => (long?)x.Amount) ?? 0L })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.AccountId, r => r.Sum);
    }

    private static Dictionary<AccountId, List<AccountId>> BuildChildrenMap(
        IReadOnlyList<AccountNode> accounts
    )
    {
        var map = new Dictionary<AccountId, List<AccountId>>();
        foreach (var a in accounts)
        {
            if (a.ParentAccountId is { } parent)
            {
                if (!map.TryGetValue(parent, out var children))
                    map[parent] = children = [];
                children.Add(a.Id);
            }
        }
        return map;
    }

    // Subtree raw sum per account = own + all descendants. Each account's own sum is added to itself
    // and every ancestor by walking up the parent chain — cheap for a small chart of accounts.
    private static Dictionary<AccountId, long> RollUp(
        IReadOnlyDictionary<AccountId, AccountNode> byId,
        IReadOnlyDictionary<AccountId, long> ownRaw
    )
    {
        var subtree = new Dictionary<AccountId, long>();
        foreach (var (id, raw) in ownRaw)
        {
            AccountId? cursor = id;
            while (cursor is { } c && byId.TryGetValue(c, out var node))
            {
                subtree[c] = subtree.GetValueOrDefault(c) + raw;
                cursor = node.ParentAccountId;
            }
        }
        return subtree;
    }

    private static bool IsIncomeOrExpense(AccountType type) =>
        type is AccountType.Income or AccountType.Expense;

    private static bool IsBalanceSheet(AccountType type) =>
        type is AccountType.Asset or AccountType.Liability or AccountType.Equity;

    private static ValidationError InvalidRange() =>
        new(new Dictionary<string, string[]> { ["to"] = ["'to' must be on or after 'from'."] });

    private sealed record AccountNode(
        AccountId Id,
        string Name,
        string Code,
        AccountType AccountType,
        AccountId? ParentAccountId
    );

    /// <summary>
    /// Accumulates the Money flow's nodes and links. The Income/Expense hierarchy is walked
    /// top-down: a child whose subtree sits on the same side as its parent nests under it; a child
    /// on the opposite side (a refund-heavy expense behaving like income, say) escapes straight to
    /// the hub. Either way each account's own line sum reaches the hub exactly once, so the
    /// double-entry balance is preserved. Nodes are derived from the links that actually appear, so
    /// no empty or orphaned node is emitted.
    /// </summary>
    private sealed class FlowBuilder
    {
        private readonly IReadOnlyDictionary<AccountId, AccountNode> _byId;
        private readonly IReadOnlyDictionary<AccountId, List<AccountId>> _childrenByParent;
        private readonly IReadOnlyDictionary<AccountId, long> _ownRaw;
        private readonly IReadOnlyDictionary<AccountId, long> _subtreeRaw;
        private readonly CurrencyCode _currencyCode;
        private readonly IReadOnlySet<AccountId> _expanded;
        private readonly List<MoneyFlowLink> _links = [];

        public FlowBuilder(
            IReadOnlyDictionary<AccountId, AccountNode> byId,
            IReadOnlyDictionary<AccountId, List<AccountId>> childrenByParent,
            IReadOnlyDictionary<AccountId, long> ownRaw,
            IReadOnlyDictionary<AccountId, long> subtreeRaw,
            CurrencyCode currencyCode,
            IReadOnlySet<AccountId> expanded
        )
        {
            _byId = byId;
            _childrenByParent = childrenByParent;
            _ownRaw = ownRaw;
            _subtreeRaw = subtreeRaw;
            _currencyCode = currencyCode;
            _expanded = expanded;
        }

        /// <summary>
        /// Emits the links for an account's children and returns the signed value drawn up to the
        /// account itself (own line sum plus same-side children). The caller emits the account's own
        /// up-link with the returned value. A node that is not in the expanded set becomes a leaf —
        /// its whole subtree folds into the returned value and no child links are emitted.
        /// </summary>
        public long BuildSubtree(AccountId nodeId)
        {
            // Not expanded: collapse the entire subtree into this node. The signed subtree sum
            // already includes every descendant, so the value reaching the hub is unchanged — only
            // the intermediate nodes disappear.
            if (!_expanded.Contains(nodeId))
                return _subtreeRaw.GetValueOrDefault(nodeId);

            var side = Math.Sign(_subtreeRaw.GetValueOrDefault(nodeId));
            var drawn = _ownRaw.GetValueOrDefault(nodeId);

            foreach (var childId in _childrenByParent.GetValueOrDefault(nodeId, []))
            {
                if (_subtreeRaw.GetValueOrDefault(childId) == 0)
                    continue; // dormant subtree this period — render nothing

                var childDrawn = BuildSubtree(childId);
                var childSide = Math.Sign(_subtreeRaw.GetValueOrDefault(childId));

                if (side != 0 && childSide == side)
                {
                    // Same side: nest under this node and fold into its drawn value.
                    Link(nodeId.Value.ToString(), childId, childDrawn);
                    drawn += childDrawn;
                }
                else
                {
                    // Opposite side (or this node is a wash): the child escapes to the hub.
                    Link(HubId, childId, childDrawn);
                }
            }

            return drawn;
        }

        public void Link(string upId, AccountId nodeId, long drawn)
        {
            if (drawn == 0)
                return;

            var value = new Money(Math.Abs(drawn), _currencyCode);
            var id = nodeId.Value.ToString();
            // drawn > 0 → exit (debit, money out): up flows toward the node.
            // drawn < 0 → source (credit, money in): the node flows toward up.
            _links.Add(
                drawn > 0 ? new MoneyFlowLink(upId, id, value) : new MoneyFlowLink(id, upId, value)
            );
        }

        public (IReadOnlyList<MoneyFlowNode> Nodes, IReadOnlyList<MoneyFlowLink> Links) Build()
        {
            var metaById = _byId.Values.ToDictionary(a => a.Id.Value.ToString());
            var nodeIds = _links.SelectMany(l => new[] { l.Source, l.Target }).ToHashSet();
            var nodes = nodeIds
                .Select(id =>
                    id == HubId
                        ? new MoneyFlowNode(HubId, "Total", MoneyFlowNodeKind.Hub, null, false)
                        : new MoneyFlowNode(
                            id,
                            metaById[id].Name,
                            KindFor(metaById[id].AccountType),
                            metaById[id].ParentAccountId?.Value.ToString(),
                            HasRenderableChildren(metaById[id].Id)
                        )
                )
                .ToList();
            return (nodes, _links);
        }

        // A node is worth an expand affordance only when descending would reveal something: at least
        // one child whose subtree moved this period (mirrors the dormant-subtree skip above).
        private bool HasRenderableChildren(AccountId id) =>
            _childrenByParent
                .GetValueOrDefault(id, [])
                .Any(childId => _subtreeRaw.GetValueOrDefault(childId) != 0);

        private static MoneyFlowNodeKind KindFor(AccountType type) =>
            type switch
            {
                AccountType.Income => MoneyFlowNodeKind.Income,
                AccountType.Expense => MoneyFlowNodeKind.Expense,
                AccountType.Asset => MoneyFlowNodeKind.Asset,
                AccountType.Liability => MoneyFlowNodeKind.Liability,
                _ => MoneyFlowNodeKind.Equity,
            };
    }
}
