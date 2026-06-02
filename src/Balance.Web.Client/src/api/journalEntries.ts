import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toBankTransactionDetail, type BankTransactionDetail } from './bankTransactions';
import type { components } from '../lib/api-types';
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
import { deleteRequest, getJson, postJson, putJson } from '../lib/http';
import { toNumber } from '../lib/money';
import type { Page } from '../lib/paging';

type WireCreateRequest = components['schemas']['CreateJournalEntryRequest'];
type WireReplaceRequest = components['schemas']['ReplaceJournalEntryRequest'];
type WireEntry = components['schemas']['JournalEntryOutput'];
type WireEntryDetail = components['schemas']['JournalEntryDetailOutput'];
type WirePagedEntries = components['schemas']['PagedOutputOfJournalEntryOutput'];
type WireLine = components['schemas']['JournalLineOutput'];
type WireReconciliationStatus = components['schemas']['ReconciliationStatus'];

export type JournalLine = {
    id: JournalLineId;
    accountId: AccountId;
    accountName: string;
    amount: number;
    reconciliationStatus: WireReconciliationStatus;
    description: string | null;
};

export type JournalEntry = {
    id: JournalEntryId;
    date: string;
    description: string | null;
    counterpartyId: CounterpartyId | null;
    counterpartyName: string | null;
    lines: JournalLine[];
    hasBankTransactions: boolean;
};

export type JournalEntryDetail = JournalEntry & {
    bankTransactions: BankTransactionDetail[];
};

export const journalEntriesKeys = {
    all: ['journalEntries'] as const,
    list: (skip: number, take: number, q: string, counterpartyId: CounterpartyId | null) =>
        [...journalEntriesKeys.all, 'list', { skip, take, q, counterpartyId }] as const,
    detail: (id: JournalEntryId) => [...journalEntriesKeys.all, 'detail', id] as const,
};

function toLine(wire: WireLine): JournalLine {
    const amount = toNumber(wire.amount);
    return {
        id: asJournalLineId(wire.id),
        accountId: asAccountId(wire.accountId),
        accountName: wire.accountName,
        amount,
        reconciliationStatus: wire.reconciliationStatus,
        description: wire.description,
    };
}

function toEntry(wire: WireEntry): JournalEntry {
    return {
        id: asJournalEntryId(wire.id),
        date: wire.date,
        description: wire.description,
        counterpartyId: wire.counterpartyId ? asCounterpartyId(wire.counterpartyId) : null,
        counterpartyName: wire.counterpartyName,
        lines: wire.lines.map(toLine),
        hasBankTransactions: wire.hasBankTransactions,
    };
}

function toEntryDetail(wire: WireEntryDetail): JournalEntryDetail {
    const bankTransactions = wire.bankTransactions.map(toBankTransactionDetail);
    return {
        id: asJournalEntryId(wire.id),
        date: wire.date,
        description: wire.description,
        counterpartyId: wire.counterpartyId ? asCounterpartyId(wire.counterpartyId) : null,
        counterpartyName: wire.counterpartyName,
        lines: wire.lines.map(toLine),
        hasBankTransactions: bankTransactions.length > 0,
        bankTransactions,
    };
}

export function useJournalEntries(
    skip: number,
    take: number,
    q: string,
    counterpartyId: CounterpartyId | null = null,
) {
    return useQuery({
        queryKey: journalEntriesKeys.list(skip, take, q, counterpartyId),
        queryFn: async ({ signal }): Promise<Page<JournalEntry>> => {
            const params = new URLSearchParams({ skip: String(skip), take: String(take) });
            if (q !== '') {
                params.set('q', q);
            }
            if (counterpartyId !== null) {
                params.set('counterpartyId', counterpartyId);
            }
            const wire = await getJson<WirePagedEntries>(
                `/api/journal-entries?${params.toString()}`,
                signal,
                'load journal entries',
            );
            return {
                items: wire.items.map(toEntry),
                totalCount: Number(wire.totalCount),
            };
        },
    });
}

export function useJournalEntry(id: JournalEntryId) {
    return useQuery({
        queryKey: journalEntriesKeys.detail(id),
        queryFn: async ({ signal }) => {
            const wire = await getJson<WireEntryDetail>(
                `/api/journal-entries/${id}`,
                signal,
                'load journal entry',
            );
            return toEntryDetail(wire);
        },
    });
}

export function useCreateJournalEntry() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (input: WireCreateRequest) => {
            const wire = await postJson<WireEntryDetail>(
                '/api/journal-entries',
                input,
                new AbortController().signal,
                'create journal entry',
            );
            return toEntryDetail(wire);
        },
        onSuccess: async () => {
            await queryClient.invalidateQueries({ queryKey: journalEntriesKeys.all });
        },
    });
}

export function useReplaceJournalEntry() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (args: { id: JournalEntryId; request: WireReplaceRequest }) => {
            const wire = await putJson<WireEntryDetail>(
                `/api/journal-entries/${args.id}`,
                args.request,
                new AbortController().signal,
                'update journal entry',
            );
            return toEntryDetail(wire);
        },
        onSuccess: async (_data, vars) => {
            await queryClient.invalidateQueries({ queryKey: journalEntriesKeys.all });
            await queryClient.invalidateQueries({
                queryKey: journalEntriesKeys.detail(vars.id),
            });
        },
    });
}

export function useDeleteJournalEntry() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (id: JournalEntryId) => {
            await deleteRequest(
                `/api/journal-entries/${id}`,
                new AbortController().signal,
                'delete journal entry',
            );
        },
        onSuccess: async () => {
            await queryClient.invalidateQueries({ queryKey: journalEntriesKeys.all });
        },
    });
}
