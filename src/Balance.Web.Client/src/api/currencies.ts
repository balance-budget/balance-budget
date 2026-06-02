import { useQuery } from '@tanstack/react-query';
import type { components } from '../lib/api-types';
import { getJson } from '../lib/http';
import { toNumber } from '../lib/money';

type WireCurrency = components['schemas']['CurrencyOutput'];

export type Currency = {
    code: string;
    name: string;
    minorUnitScale: number;
    symbol: string | null;
};

export type CurrencyCatalog = ReadonlyMap<string, Currency>;

const EMPTY_CATALOG: CurrencyCatalog = new Map();

export const currenciesKeys = {
    all: ['currencies'] as const,
    list: () => [...currenciesKeys.all, 'list'] as const,
};

function fetchCurrencies(signal: AbortSignal): Promise<WireCurrency[]> {
    return getJson<WireCurrency[]>('/api/currencies', signal, 'load currencies');
}

// minorUnitScale comes through as `number | string` for the same long-int
// reason as Money — normalise. Code/name/symbol are required on the wire.
function toCurrency(wire: WireCurrency): Currency {
    return {
        code: wire.code,
        name: wire.name,
        minorUnitScale: toNumber(wire.minorUnitScale),
        symbol: wire.symbol ?? null,
    };
}

/**
 * Catalog of currencies known to the backend. The list mutates rarely (admin
 * CRUD on /api/currencies), so we let it sit in the React Query cache and
 * survive across page navigations.
 */
export function useCurrencies() {
    return useQuery({
        queryKey: currenciesKeys.list(),
        queryFn: async ({ signal }) => {
            const wire = await fetchCurrencies(signal);
            return new Map<string, Currency>(wire.map(c => [c.code, toCurrency(c)]));
        },
        staleTime: 5 * 60 * 1000,
    });
}

/**
 * Catalog accessor for components that just need to format Money values. Falls
 * back to an empty map on first paint so callers can render with defensive
 * defaults (see lib/money.ts) instead of suspending.
 */
export function useCurrencyCatalog(): CurrencyCatalog {
    return useCurrencies().data ?? EMPTY_CATALOG;
}
