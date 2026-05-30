import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { compare } from 'fast-json-patch';
import type { components } from '../lib/api-types';
import { asAccountId, asCounterpartyId, type AccountId, type CounterpartyId } from '../lib/domain';
import { deleteRequest, getJson, patchJson, postJson } from '../lib/http';
import type { Page } from '../lib/paging';

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

export function useCounterparty(id: CounterpartyId) {
    return useQuery({
        queryKey: counterpartiesKeys.detail(id),
        queryFn: async ({ signal }) => {
            const wire = await getJson<WireCounterparty>(
                `/api/counterparties/${id}`,
                signal,
                'load counterparty',
            );
            return toCounterparty(wire);
        },
    });
}

export function useCreateCounterparty() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (input: WireCreateRequest) => {
            const wire = await postJson<WireCounterparty>(
                '/api/counterparties',
                input,
                new AbortController().signal,
                'create counterparty',
            );
            return toCounterparty(wire);
        },
        onSuccess: async () => {
            await queryClient.invalidateQueries({ queryKey: counterpartiesKeys.all });
        },
    });
}

export function useUpdateCounterparty() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (args: {
            id: CounterpartyId;
            original: WireUpdateInput;
            edited: WireUpdateInput;
        }) => {
            const patch = compare(args.original, args.edited);
            const wire = await patchJson<WireCounterparty>(
                `/api/counterparties/${args.id}`,
                patch,
                new AbortController().signal,
                'update counterparty',
            );
            return toCounterparty(wire);
        },
        onSuccess: async (_data, vars) => {
            await queryClient.invalidateQueries({ queryKey: counterpartiesKeys.all });
            await queryClient.invalidateQueries({
                queryKey: counterpartiesKeys.detail(vars.id),
            });
        },
    });
}

function toSuggestedCounterAccount(wire: WireSuggestedCounterAccount): SuggestedCounterAccount {
    const amount = typeof wire.amount === 'string' ? Number(wire.amount) : wire.amount;
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

export function useDeleteCounterparty() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (id: CounterpartyId) => {
            await deleteRequest(
                `/api/counterparties/${id}`,
                new AbortController().signal,
                'delete counterparty',
            );
        },
        onSuccess: async () => {
            await queryClient.invalidateQueries({ queryKey: counterpartiesKeys.all });
        },
    });
}
