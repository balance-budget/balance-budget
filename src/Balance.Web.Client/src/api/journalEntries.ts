import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
    toBankTransactionDetail,
    type BankTransactionDetail,
} from './bankTransactions';
import type { components } from '../lib/api-types';
import {
    type AccountId,
    type BankTransactionId,
    type CounterpartyId,
    type JournalEntryId,
    type JournalLineId,
    asAccountId,
    asBankTransactionId,
    asCounterpartyId,
    asJournalEntryId,
    asJournalLineId,
} from '../lib/domain';
import { deleteRequest, getJson, postJson, putJson } from '../lib/http';

type WireCreateRequest = components['schemas']['CreateJournalEntryRequest'];
type WireReplaceRequest = components['schemas']['ReplaceJournalEntryRequest'];
type WireEntry = components['schemas']['JournalEntryOutput'];
type WireEntryDetail = components['schemas']['JournalEntryDetailOutput'];
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
    bankTransactionId: BankTransactionId | null;
    counterpartyId: CounterpartyId | null;
    counterpartyName: string | null;
    lines: JournalLine[];
};

export type JournalEntryDetail = JournalEntry & {
    bankTransaction: BankTransactionDetail | null;
};

export const journalEntriesKeys = {
    all: ['journalEntries'] as const,
    list: (skip: number, take: number) =>
        [...journalEntriesKeys.all, 'list', { skip, take }] as const,
    detail: (id: JournalEntryId) => [...journalEntriesKeys.all, 'detail', id] as const,
};

function toLine(wire: WireLine): JournalLine {
    const amount = typeof wire.amount === 'string' ? Number(wire.amount) : wire.amount;
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
        bankTransactionId: wire.bankTransactionId
            ? asBankTransactionId(wire.bankTransactionId)
            : null,
        counterpartyId: wire.counterpartyId ? asCounterpartyId(wire.counterpartyId) : null,
        counterpartyName: wire.counterpartyName,
        lines: wire.lines.map(toLine),
    };
}

function toEntryDetail(wire: WireEntryDetail): JournalEntryDetail {
    return {
        ...toEntry(wire),
        bankTransaction:
            wire.bankTransaction === null
                ? null
                : toBankTransactionDetail(wire.bankTransaction),
    };
}

export function useJournalEntries(skip: number, take: number) {
    return useQuery({
        queryKey: journalEntriesKeys.list(skip, take),
        queryFn: async ({ signal }) => {
            const wire = await getJson<WireEntry[]>(
                `/api/journal-entries?skip=${skip}&take=${take}`,
                signal,
                'load journal entries',
            );
            return wire.map(toEntry);
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
