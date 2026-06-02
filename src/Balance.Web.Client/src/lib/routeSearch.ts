/**
 * Shared coercion for the `?page=&q=` URL search params used by the paged list
 * routes (Activity, Register, Counterparties, …). Each route's `validateSearch`
 * composes these so the "page ≥ 1, default 1 / q is a string, default ''" rules
 * live in one place.
 */

/** The common search-param shape for a paged, searchable list route. */
export type PageQSearch = { page: number; q: string };

/** Coerce a raw URL search value into a 1-based page number (defaults to 1). */
export function parsePage(raw: unknown): number {
    const candidate = Number(raw);
    return Number.isInteger(candidate) && candidate >= 1 ? candidate : 1;
}

/** Coerce a raw URL search value into a query string (defaults to ''). */
export function parseQ(raw: unknown): string {
    return typeof raw === 'string' ? raw : '';
}
