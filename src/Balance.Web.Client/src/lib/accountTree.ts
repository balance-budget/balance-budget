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

/** Separator between path segments in account labels, e.g. "Car › Tax". */
export const ACCOUNT_PATH_SEPARATOR = ' › ';

/**
 * The chain of account names from the root down to `id`, e.g. `['Car', 'Tax']`.
 * A root account returns just its own name. Showing the full path makes nested
 * leaves unambiguous (Car › Tax vs Home › Tax) — see ADR-0019.
 */
export function accountPathSegments(
    byId: ReadonlyMap<AccountId, Account>,
    id: AccountId,
): string[] {
    const segments: string[] = [];
    const guard = new Set<AccountId>();
    let current = byId.get(id);
    while (current && !guard.has(current.id)) {
        guard.add(current.id);
        segments.unshift(current.name);
        current = current.parentId === null ? undefined : byId.get(current.parentId);
    }
    return segments;
}

/**
 * Plain "5110  Car › Tax" label (code + full path) for read-only account
 * displays — e.g. a frozen journal line that shows where it posted without an
 * editable picker. Returns null when the account is unknown.
 */
export function accountPathLabel(
    byId: ReadonlyMap<AccountId, Account>,
    id: AccountId,
): string | null {
    const account = byId.get(id);
    if (!account) return null;
    return `${account.code}  ${accountPathSegments(byId, id).join(ACCOUNT_PATH_SEPARATOR)}`;
}
