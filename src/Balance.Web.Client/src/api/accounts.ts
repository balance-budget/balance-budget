import { useQuery } from '@tanstack/react-query';
import type { components } from '../lib/api-types';
import { type AccountId, type AccountType, asAccountId } from '../lib/domain';

type WireAccount = components['schemas']['AccountOutput'];

/**
 * Slice 1 view-model for an account in the sidebar list. Will grow with
 * `balance` and `bankAccount` fields when slice 2 lands the enriched
 * `AccountOutput` (see ADR-0008).
 */
export type Account = {
    id: AccountId;
    name: string;
    type: AccountType;
    currencyCode: string;
};

export const accountsKeys = {
    all: ['accounts'] as const,
    list: () => [...accountsKeys.all, 'list'] as const,
};

async function fetchAccounts(signal: AbortSignal): Promise<WireAccount[]> {
    const response = await fetch('/api/accounts', { signal });
    if (!response.ok) {
        throw new Error(`Failed to load accounts (${response.status})`);
    }
    return (await response.json()) as WireAccount[];
}

function toAccount(wire: WireAccount): Account {
    return {
        id: asAccountId(wire.id),
        name: wire.name,
        type: wire.accountType,
        currencyCode: wire.currencyCode,
    };
}

export function useAccounts() {
    return useQuery({
        queryKey: accountsKeys.list(),
        queryFn: async ({ signal }) => {
            const wire = await fetchAccounts(signal);
            return wire.map(toAccount);
        },
    });
}
