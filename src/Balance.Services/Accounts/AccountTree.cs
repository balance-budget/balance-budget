using Balance.Data.Entities.Ids;

namespace Balance.Services.Accounts;

/// <summary>
/// One node of the chart-of-accounts tree (ADR-0019) — the minimal shape needed to walk
/// parent/child relationships in memory. The accounts table is small, so descendant resolution
/// and roll-up are computed in process rather than via a provider-specific recursive CTE.
/// </summary>
internal readonly record struct AccountNode(AccountId Id, AccountId? ParentAccountId);

internal static class AccountTree
{
    /// <summary>
    /// Returns <paramref name="root"/> together with all of its transitive descendants.
    /// </summary>
    public static HashSet<AccountId> DescendantsAndSelf(
        IReadOnlyCollection<AccountNode> nodes,
        AccountId root
    )
    {
        ArgumentNullException.ThrowIfNull(nodes);

        var childrenByParent = BuildChildrenMap(nodes);

        var result = new HashSet<AccountId> { root };
        var stack = new Stack<AccountId>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!childrenByParent.TryGetValue(current, out var children))
                continue;
            foreach (var child in children)
            {
                if (result.Add(child))
                    stack.Push(child);
            }
        }

        return result;
    }

    /// <summary>
    /// <c>true</c> when re-parenting <paramref name="node"/> under <paramref name="newParent"/> would
    /// create a cycle — i.e. the proposed parent is the node itself or one of its descendants.
    /// </summary>
    public static bool WouldCreateCycle(
        IReadOnlyCollection<AccountNode> nodes,
        AccountId node,
        AccountId newParent
    ) => DescendantsAndSelf(nodes, node).Contains(newParent);

    /// <summary>
    /// Folds each node's own raw sum up the tree: the result for a node is its own sum plus the sum
    /// of every descendant. For a leaf this is just its own sum; for a non-postable account it rolls
    /// up.
    /// </summary>
    public static Dictionary<AccountId, long> SubtreeSums(
        IReadOnlyCollection<AccountNode> nodes,
        IReadOnlyDictionary<AccountId, long> ownSums
    )
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(ownSums);

        var childrenByParent = BuildChildrenMap(nodes);
        var memo = new Dictionary<AccountId, long>(nodes.Count);

        long Compute(AccountId id)
        {
            if (memo.TryGetValue(id, out var cached))
                return cached;

            var total = ownSums.GetValueOrDefault(id);
            if (childrenByParent.TryGetValue(id, out var children))
            {
                foreach (var child in children)
                    total = checked(total + Compute(child));
            }

            memo[id] = total;
            return total;
        }

        foreach (var node in nodes)
            Compute(node.Id);

        return memo;
    }

    private static Dictionary<AccountId, List<AccountId>> BuildChildrenMap(
        IReadOnlyCollection<AccountNode> nodes
    )
    {
        var map = new Dictionary<AccountId, List<AccountId>>();
        foreach (var node in nodes)
        {
            if (node.ParentAccountId is not { } parent)
                continue;
            if (!map.TryGetValue(parent, out var children))
            {
                children = [];
                map[parent] = children;
            }

            children.Add(node.Id);
        }

        return map;
    }
}
