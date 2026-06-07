/*
 * Pure helpers behind the shared typeahead `<Combobox>` (issue #84). Kept
 * separately so the filter / grouping logic stays unit-testable without
 * spinning up a DOM. The component in `Combobox.tsx` binds these to React
 * Aria's ComboBox; option rendering, keyboard navigation, and overlay
 * positioning are React Aria's (ADR-0024).
 */

import type { ReactNode } from 'react';

export type ComboboxItem<T> = {
    /** Stable string identifier — used as React key and for selection. */
    key: string;
    /** Plain-text label shown in the collapsed input once selected, and the
     *  default substring target for the search query (see `searchText`). */
    label: string;
    /** Extra text the query is matched against instead of `label`. Lets a
     *  picker search over facets that aren't all shown in `label` — e.g. an
     *  account's code and full path segments. Falls back to `label` when unset. */
    searchText?: string;
    /** Rich node rendered for this option in the open list, when the row needs
     *  more than the plain `label` (e.g. a dimmed account path + muted code).
     *  Falls back to `label` when unset. */
    render?: ReactNode;
    /** Optional bucket name for grouped pickers (e.g. AccountType for the
     *  per-row account combobox). Bucket order is controlled by `groupOrder`
     *  on the component prop. */
    group?: string;
    /** The underlying domain value the consumer cares about (e.g. AccountId,
     *  CounterpartyId, or null for a sentinel option like "None"). */
    value: T;
};

/** Case-insensitive substring match. Returns true when `query` is empty. */
export function matchesQuery(label: string, query: string): boolean {
    const q = query.trim().toLowerCase();
    if (q.length === 0) return true;
    return label.toLowerCase().includes(q);
}

export type Bucket<T> = { group: string | undefined; items: ComboboxItem<T>[] };

/** Partition items into ordered group buckets. Groups listed in `groupOrder`
 *  come first; leftovers keep first-seen order, matching how `<optgroup>`
 *  would have rendered them. Ungrouped items keep their relative order. */
export function groupBuckets<T>(
    items: readonly ComboboxItem<T>[],
    groupOrder: readonly string[] | undefined,
): Bucket<T>[] {
    const buckets = new Map<string | undefined, ComboboxItem<T>[]>();
    for (const item of items) {
        const list = buckets.get(item.group) ?? [];
        list.push(item);
        buckets.set(item.group, list);
    }
    if (!groupOrder || groupOrder.length === 0) {
        return Array.from(buckets, ([group, list]) => ({ group, items: list }));
    }
    const seen = new Set<string | undefined>();
    const out: Bucket<T>[] = [];
    for (const g of groupOrder) {
        if (!buckets.has(g)) continue;
        out.push({ group: g, items: buckets.get(g) ?? [] });
        seen.add(g);
    }
    for (const [group, list] of buckets) {
        if (seen.has(group)) continue;
        out.push({ group, items: list });
    }
    return out;
}
