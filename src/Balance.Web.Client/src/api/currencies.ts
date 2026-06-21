import { useQuery } from '@tanstack/react-query';
import type { components } from '../lib/api-types.gen';
import { getJson } from '../lib/http';
import { toNumber } from '../lib/money';
import { createResourceCrud } from '../lib/resourceApi';

type WireCurrency = components['schemas']['CurrencyOutput'];
type WireCreateCurrency = components['schemas']['CreateCurrencyRequest'];
type WireUpdateCurrency = components['schemas']['UpdateCurrencyInput'];

export type Currency = {
    code: string;
    name: string;
    minorUnitScale: number;
    symbol: string | null;
    /** Accounts referencing this currency. Together with {@link bankAccountCount}
     *  this is the delete guard: a currency is deletable only when both are zero. */
    accountCount: number;
    /** Bank accounts referencing this currency. See {@link accountCount}. */
    bankAccountCount: number;
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

// minorUnitScale and the usage counts come through as `number | string` for the
// same long-int reason as Money — normalize. Code/name/symbol are required on the
// wire; the counts are 0 on single-row outputs (create/update) and only populated
// on the list, which is why mutations invalidate rather than write through.
function toCurrency(wire: WireCurrency): Currency {
    return {
        code: wire.code,
        name: wire.name,
        minorUnitScale: toNumber(wire.minorUnitScale),
        symbol: wire.symbol ?? null,
        accountCount: toNumber(wire.accountCount ?? 0),
        bankAccountCount: toNumber(wire.bankAccountCount ?? 0),
    };
}

/** True when the currency is referenced by any account or bank account, so its
 *  delete is blocked by the FK `RESTRICT` and the screen disables the action. */
export function isCurrencyInUse(currency: Currency): boolean {
    return currency.accountCount > 0 || currency.bankAccountCount > 0;
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

// Currency codes are the route id (e.g. /api/currencies/EUR). Update is a JSON-Patch
// PATCH carrying only name + symbol — code and minor-unit scale are immutable after
// creation (ADR-0032). Mutations invalidate the whole catalog so the usage counts and
// the formatting cache stay fresh.
const crud = createResourceCrud<
    WireCurrency,
    Currency,
    WireCreateCurrency,
    WireUpdateCurrency,
    string
>({
    basePath: '/api/currencies',
    label: 'currency',
    allKey: currenciesKeys.all,
    detailKey: (code: string) => [...currenciesKeys.all, 'detail', code],
    toView: toCurrency,
});

export const useCreateCurrency = crud.useCreate;
export const useUpdateCurrency = crud.useUpdate;
export const useDeleteCurrency = crud.useDelete;
