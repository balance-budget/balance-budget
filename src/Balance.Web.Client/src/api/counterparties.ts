import { useQuery } from '@tanstack/react-query';
import type { components } from '../lib/api-types.gen';
import { asAccountId, asCounterpartyId, type AccountId, type CounterpartyId } from '../lib/domain';
import { getJson } from '../lib/http';
import { toNumber } from '../lib/money';
import type { Page } from '../lib/paging';
import { createResourceCrud } from '../lib/resourceApi';

type WireCounterparty = components['schemas']['CounterpartyOutput'];
type WirePagedCounterparties = components['schemas']['PagedOutputOfCounterpartyOutput'];
type WireCreateRequest = components['schemas']['CreateCounterpartyRequest'];
type WireUpdateInput = components['schemas']['UpdateCounterpartyInput'];
type WireSuggestedCounterAccount = components['schemas']['SuggestedCounterAccountOutput'];

export type Counterparty = {
    id: CounterpartyId;
    name: string;
};

export type SuggestedCounterAccount = {
    accountId: AccountId;
    amount: number;
};

export const counterpartiesKeys = {
    all: ['counterparties'] as const,
    list: () => [...counterpartiesKeys.all, 'list'] as const,
    page: (skip: number, take: number, q: string) =>
        [...counterpartiesKeys.all, 'page', { skip, take, q }] as const,
    detail: (id: CounterpartyId) => [...counterpartiesKeys.all, 'detail', id] as const,
    suggestedAccounts: (id: CounterpartyId) =>
        [...counterpartiesKeys.all, 'suggested-accounts', id] as const,
};

function toCounterparty(wire: WireCounterparty): Counterparty {
    return { id: asCounterpartyId(wire.id), name: wire.name };
}

/**
 * Full, unpaginated list of counterparties (sorted by name) for picker/dropdown use.
 * The list screen uses {@link useCounterpartiesPage} for server-side search + paging.
 */
export function useCounterparties() {
    return useQuery({
        queryKey: counterpartiesKeys.list(),
        queryFn: async ({ signal }) => {
            const wire = await getJson<WirePagedCounterparties>(
                '/api/counterparties',
                signal,
                'load counterparties',
            );
            return wire.items.map(toCounterparty);
        },
    });
}

export function useCounterpartiesPage(skip: number, take: number, q: string) {
    return useQuery({
        queryKey: counterpartiesKeys.page(skip, take, q),
        queryFn: async ({ signal }): Promise<Page<Counterparty>> => {
            const params = new URLSearchParams({ skip: String(skip), take: String(take) });
            if (q !== '') {
                params.set('q', q);
            }
            const wire = await getJson<WirePagedCounterparties>(
                `/api/counterparties?${params.toString()}`,
                signal,
                'load counterparties',
            );
            return {
                items: wire.items.map(toCounterparty),
                totalCount: Number(wire.totalCount),
            };
        },
    });
}

const crud = createResourceCrud<
    WireCounterparty,
    Counterparty,
    WireCreateRequest,
    WireUpdateInput,
    CounterpartyId
>({
    basePath: '/api/counterparties',
    label: 'counterparty',
    allKey: counterpartiesKeys.all,
    detailKey: counterpartiesKeys.detail,
    toView: toCounterparty,
});

export const useCounterparty = crud.useDetail;
export const useCreateCounterparty = crud.useCreate;
export const useUpdateCounterparty = crud.useUpdate;
export const useDeleteCounterparty = crud.useDelete;

function toSuggestedCounterAccount(wire: WireSuggestedCounterAccount): SuggestedCounterAccount {
    const amount = toNumber(wire.amount);
    return { accountId: asAccountId(wire.accountId), amount };
}

export function useSuggestedCounterAccounts(id: CounterpartyId | null) {
    return useQuery({
        queryKey: id ? counterpartiesKeys.suggestedAccounts(id) : ['counterparties', 'noop'],
        queryFn: async ({ signal }) => {
            if (id === null) return [] as SuggestedCounterAccount[];
            const wire = await getJson<WireSuggestedCounterAccount[]>(
                `/api/counterparties/${id}/suggested-accounts`,
                signal,
                'load suggested accounts',
            );
            return wire.map(toSuggestedCounterAccount);
        },
        enabled: id !== null,
    });
}
