import type { Account } from '../api/accounts';
import type { AccountId, AccountType } from './domain';

/** An id→Account lookup over the whole chart of accounts. */
export type AccountIndex = ReadonlyMap<AccountId, Account>;

const INDEX_CACHE = new WeakMap<readonly Account[], AccountIndex>();

/**
 * The id→Account index for a chart of accounts, memoized on the array's identity.
 * `AccountLabel` renders in every row of every collection, so a hundred labels in one
 * table must share one index instead of each building its own (ADR-0039); react-query
 * hands out the same array until the query refetches, which is what makes the cache hit.
 */
export function accountIndexFor(accounts: readonly Account[]): AccountIndex {
    const cached = INDEX_CACHE.get(accounts);
    if (cached) return cached;

    const index = new Map(accounts.map(a => [a.id, a]));
    INDEX_CACHE.set(accounts, index);
    return index;
}

/**
 * Sibling order for the chart of accounts: by code (numeric-aware), then by
 * name. Shared so the Accounts screen and the sidebar tree order identically.
 */
export const sortSiblings = (a: Account, b: Account): number =>
    a.code.localeCompare(b.code, undefined, { numeric: true }) || a.name.localeCompare(b.name);

/**
 * Maps a parent id to its children, each bucket sorted by {@link sortSiblings};
 * the `null` key holds the roots. Single tested source for the tree structure
 * shared by the Accounts screen and the sidebar (ADR-0035).
 */
export function buildChildrenMap(accounts: readonly Account[]): Map<AccountId | null, Account[]> {
    const map = new Map<AccountId | null, Account[]>();
    for (const a of accounts) {
        const bucket = map.get(a.parentId) ?? [];
        bucket.push(a);
        map.set(a.parentId, bucket);
    }
    for (const bucket of map.values()) bucket.sort(sortSiblings);
    return map;
}

/** Groups root accounts (no parent) by their {@link AccountType}, each bucket
 *  sorted by {@link sortSiblings}. */
export function groupRootsByType(accounts: readonly Account[]): Map<AccountType, Account[]> {
    const map = new Map<AccountType, Account[]>();
    for (const a of accounts) {
        if (a.parentId !== null) continue;
        const bucket = map.get(a.type) ?? [];
        bucket.push(a);
        map.set(a.type, bucket);
    }
    for (const bucket of map.values()) bucket.sort(sortSiblings);
    return map;
}

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
 * The accounts from the root down to `id` inclusive, root first. Empty when `id` is
 * unknown. Guarded against a cycle the server rejects but the cache could still hold
 * mid-reparent.
 */
export function accountChain(byId: AccountIndex, id: AccountId): Account[] {
    const chain: Account[] = [];
    const guard = new Set<AccountId>();
    let current = byId.get(id);
    while (current && !guard.has(current.id)) {
        guard.add(current.id);
        chain.unshift(current);
        current = current.parentId === null ? undefined : byId.get(current.parentId);
    }
    return chain;
}

/** `id`'s ancestors, root first, excluding the account itself — the breadcrumb trail
 *  above an account's own page. */
export function accountAncestors(byId: AccountIndex, id: AccountId): Account[] {
    return accountChain(byId, id).slice(0, -1);
}

/**
 * The chain of account names from the root down to `id`, e.g. `['Car', 'Tax']`.
 * A root account returns just its own name. Showing the full path makes nested
 * leaves unambiguous (Car › Tax vs Home › Tax) — see ADR-0019.
 */
export function accountPathSegments(byId: AccountIndex, id: AccountId): string[] {
    return accountChain(byId, id).map(a => a.name);
}

/** The path as plain text, or `fallback` when the account is unknown — for the places
 *  that need a string rather than an `AccountLabel` (an aria-label, a toast, a filter). */
export function accountPathText(byId: AccountIndex, id: AccountId, fallback: string): string {
    const segments = accountPathSegments(byId, id);
    return segments.length > 0 ? segments.join(ACCOUNT_PATH_SEPARATOR) : fallback;
}

/**
 * Plain "5110  Car › Tax" label (code + full path) for read-only account
 * displays — e.g. a frozen journal line that shows where it posted without an
 * editable picker. Returns null when the account is unknown.
 */
export function accountPathLabel(byId: AccountIndex, id: AccountId): string | null {
    const account = byId.get(id);
    if (!account) return null;
    return `${account.code}  ${accountPathSegments(byId, id).join(ACCOUNT_PATH_SEPARATOR)}`;
}
