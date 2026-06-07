import { useQuery } from '@tanstack/react-query';
import type { components } from '../lib/api-types.gen';
import {
    type AccountId,
    type CounterpartyId,
    type JournalEntryId,
    type JournalLineId,
    asAccountId,
    asCounterpartyId,
    asJournalEntryId,
    asJournalLineId,
} from '../lib/domain';
import { getJson } from '../lib/http';
import { toMoney, type Money } from '../lib/money';
import type { Page } from '../lib/paging';
import { accountsKeys } from './accounts';

type WireRegisterRow = components['schemas']['RegisterRowOutput'];
type WirePagedRegisterRows = components['schemas']['PagedOutputOfRegisterRowOutput'];
type WireCounterLeg = components['schemas']['RegisterRowCounterLeg'];

export type ReconciliationStatus = 'Uncleared' | 'Cleared' | 'Reconciled';

export type RegisterCounterLeg = {
    accountId: AccountId;
    accountName: string;
    amount: Money;
};

export type RegisterRow = {
    journalEntryId: JournalEntryId;
    journalLineId: JournalLineId;
    /** The posted account — the (descendant) leaf the focal line actually sits on. */
    accountId: AccountId;
    accountName: string;
    date: string;
    entryDescription: string | null;
    counterpartyId: CounterpartyId | null;
    counterpartyName: string | null;
    lineDescription: string | null;
    reconciliationStatus: ReconciliationStatus;
    amount: Money;
    counter: RegisterCounterLeg[];
};

/** '' means "any status". */
export type RegisterStatusFilter = '' | ReconciliationStatus;

/** Optional register narrowing on top of the `q` search; all AND-combined.
 *  Non-postable accounts in either picker mean "the whole subtree" (ADR-0019).
 *  Dates are `yyyy-MM-dd` entry-date bounds, inclusive; '' means unbounded. */
export type RegisterFilters = {
    q: string;
    posted: AccountId | null;
    counter: AccountId | null;
    from: string;
    to: string;
    status: RegisterStatusFilter;
};

export const registerKeys = {
    all: [...accountsKeys.all, 'register'] as const,
    list: (accountId: AccountId, skip: number, take: number, filters: RegisterFilters) =>
        [...registerKeys.all, accountId, { skip, take, ...filters }] as const,
};

function fetchRegister(
    accountId: AccountId,
    skip: number,
    take: number,
    filters: RegisterFilters,
    signal: AbortSignal,
): Promise<WirePagedRegisterRows> {
    const params = new URLSearchParams({ skip: String(skip), take: String(take) });
    if (filters.q !== '') {
        params.set('q', filters.q);
    }
    if (filters.posted !== null) {
        params.set('postedAccountId', filters.posted);
    }
    if (filters.counter !== null) {
        params.set('counterAccountId', filters.counter);
    }
    if (filters.from !== '') {
        params.set('from', filters.from);
    }
    if (filters.to !== '') {
        params.set('to', filters.to);
    }
    if (filters.status !== '') {
        params.set('status', filters.status);
    }
    return getJson<WirePagedRegisterRows>(
        `/api/accounts/${accountId}/register?${params.toString()}`,
        signal,
        'load register',
    );
}

function toCounterLeg(wire: WireCounterLeg): RegisterCounterLeg {
    return {
        accountId: asAccountId(wire.accountId),
        accountName: wire.accountName,
        amount: toMoney(wire.amount),
    };
}

function toRegisterRow(wire: WireRegisterRow): RegisterRow {
    const focal = toMoney(wire.amount);
    return {
        journalEntryId: asJournalEntryId(wire.journalEntryId),
        journalLineId: asJournalLineId(wire.journalLineId),
        accountId: asAccountId(wire.accountId),
        accountName: wire.accountName,
        date: wire.date,
        entryDescription: wire.entryDescription,
        counterpartyId: wire.counterpartyId ? asCounterpartyId(wire.counterpartyId) : null,
        counterpartyName: wire.counterpartyName,
        lineDescription: wire.lineDescription,
        reconciliationStatus: wire.reconciliationStatus,
        amount: focal,
        counter: wire.counter.map(toCounterLeg),
    };
}

export function useAccountRegister(
    accountId: AccountId,
    skip: number,
    take: number,
    filters: RegisterFilters,
) {
    return useQuery({
        queryKey: registerKeys.list(accountId, skip, take, filters),
        queryFn: async ({ signal }): Promise<Page<RegisterRow>> => {
            const wire = await fetchRegister(accountId, skip, take, filters, signal);
            return {
                items: wire.items.map(toRegisterRow),
                totalCount: Number(wire.totalCount),
            };
        },
    });
}
