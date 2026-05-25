/*
 * Pure helpers behind the shared typeahead `<Combobox>` (issue #84). Kept
 * separately so the filter / option-list / keyboard-navigation logic stays
 * unit-testable without spinning up a DOM. The component in `Combobox.tsx`
 * binds these to React state + JSX.
 */

export type ComboboxItem<T> = {
    /** Stable string identifier — used as React key and for selection. */
    key: string;
    /** Display label, also the substring matched against the search query. */
    label: string;
    /** Optional bucket name for grouped pickers (e.g. AccountType for the
     *  per-row account combobox). Bucket order is controlled by `groupOrder`
     *  on the component prop. */
    group?: string;
    /** The underlying domain value the consumer cares about (e.g. AccountId,
     *  CounterpartyId, or null for a sentinel option like "None"). */
    value: T;
};

export type ComboboxOption<T> =
    | { kind: 'item'; item: ComboboxItem<T>; group?: string }
    | { kind: 'create'; label: string; typed: string }
    | { kind: 'none'; label: string };

export type OptionListArgs<T> = {
    items: readonly ComboboxItem<T>[];
    query: string;
    /** When set, a "── None …" sentinel renders as the first option (unfiltered).
     *  Used for the Counterparty picker's self-transfer affordance. */
    noneLabel?: string;
    /** When set, and the query is non-empty with no exact label match, a
     *  "+ Create '<typed>'" sentinel renders as the last option. */
    createLabel?: (typed: string) => string;
    /** Optional preferred group order; groups not listed fall back to insertion
     *  order, matching how `<optgroup>` would have rendered them. */
    groupOrder?: readonly string[];
};

/** Case-insensitive substring match. Returns true when `query` is empty. */
export function matchesQuery(label: string, query: string): boolean {
    const q = query.trim().toLowerCase();
    if (q.length === 0) return true;
    return label.toLowerCase().includes(q);
}

/** Build the rendered option list — filter + optional `None` sentinel +
 *  optional `+ Create '<typed>'` sentinel. Group order is preserved per
 *  `groupOrder`, then by first-seen order for any leftover groups. */
export function buildOptionList<T>({
    items,
    query,
    noneLabel,
    createLabel,
    groupOrder,
}: OptionListArgs<T>): ComboboxOption<T>[] {
    const filtered = items.filter(item => matchesQuery(item.label, query));

    // Group ordering. Ungrouped items keep their relative order.
    const grouped = sortByGroup(filtered, groupOrder);

    const options: ComboboxOption<T>[] = [];
    if (noneLabel !== undefined) {
        options.push({ kind: 'none', label: noneLabel });
    }
    for (const g of grouped) {
        for (const item of g.items) {
            options.push({ kind: 'item', item, group: g.group });
        }
    }

    if (createLabel) {
        const trimmed = query.trim();
        const exactMatch = items.some(i => i.label.toLowerCase() === trimmed.toLowerCase());
        if (trimmed.length > 0 && !exactMatch) {
            options.push({ kind: 'create', label: createLabel(trimmed), typed: trimmed });
        }
    }

    return options;
}

type Bucket<T> = { group: string | undefined; items: ComboboxItem<T>[] };

function sortByGroup<T>(
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

/** Clamp `index` into the valid `[0, length - 1]` range, wrapping if it
 *  walks off either end. Used by the keyboard navigation reducer. */
export function clampIndex(index: number, length: number): number {
    if (length === 0) return -1;
    if (index < 0) return length - 1;
    if (index >= length) return 0;
    return index;
}

/** Compute the next active index given an arrow key press. -1 means "no
 *  active option" (e.g. the popup just opened). */
export function nextActiveIndex(active: number, length: number, direction: 1 | -1): number {
    if (length === 0) return -1;
    if (active < 0) return direction === 1 ? 0 : length - 1;
    return clampIndex(active + direction, length);
}
