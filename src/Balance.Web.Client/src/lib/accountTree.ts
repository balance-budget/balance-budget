import type { Account } from '../api/accounts';
import type { AccountId } from './domain';

/**
 * The ids of `root` and all of its transitive descendants in the chart-of-accounts
 * tree (ADR-0019). Mirrors the server's `AccountTree.DescendantsAndSelf` — the
 * accounts list is small, so the walk happens in memory.
 */
export function descendantAndSelfIds(
    accounts: readonly Account[],
    root: AccountId,
): Set<AccountId> {
    const childrenByParent = new Map<AccountId, AccountId[]>();
    for (const account of accounts) {
        if (account.parentId === null) continue;
        const siblings = childrenByParent.get(account.parentId);
        if (siblings) {
            siblings.push(account.id);
        } else {
            childrenByParent.set(account.parentId, [account.id]);
        }
    }

    const result = new Set<AccountId>([root]);
    const stack: AccountId[] = [root];
    while (stack.length > 0) {
        const current = stack.pop();
        if (current === undefined) break;
        for (const child of childrenByParent.get(current) ?? []) {
            if (!result.has(child)) {
                result.add(child);
                stack.push(child);
            }
        }
    }
    return result;
}
