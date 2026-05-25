import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { compare } from 'fast-json-patch';
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
import { deleteRequest, getJson, patchJson } from '../lib/http';
import { toMoney, type Money } from '../lib/money';

type WireUpdateInput = components['schemas']['UpdateJournalEntryInput'];

type WireRow = components['schemas']['JournalEntryRowOutput'];
type WireDetail = components['schemas']['JournalEntryOutput'];
type WireLeg = components['schemas']['JournalEntryLegSummary'];
type WireLine = components['schemas']['JournalLineOutput'];
type WireReconciliationStatus = components['schemas']['ReconciliationStatus'];

export type JournalEntryLeg = {
    accountId: AccountId;
    accountName: string;
};

export type JournalEntryRow = {
    id: JournalEntryId;
    date: string;
    description: string | null;
    bankTransactionId: BankTransactionId | null;
    counterpartyId: CounterpartyId | null;
    counterpartyName: string | null;
    lineCount: number;
    isTransfer: boolean;
    netWorthChange: Money;
    grossMagnitude: Money;
    isSimplifiable: boolean;
    fromLegs: JournalEntryLeg[];
    toLegs: JournalEntryLeg[];
};

export type JournalLine = {
    id: JournalLineId;
    accountId: AccountId;
    accountName: string;
    amount: number;
    reconciliationStatus: WireReconciliationStatus;
    description: string | null;
};

export type JournalEntry = JournalEntryRow & {
    lines: JournalLine[];
};

export const journalEntriesKeys = {
    all: ['journalEntries'] as const,
    list: (skip: number, take: number) =>
        [...journalEntriesKeys.all, 'list', { skip, take }] as const,
    detail: (id: JournalEntryId) => [...journalEntriesKeys.all, 'detail', id] as const,
};

function fetchList(skip: number, take: number, signal: AbortSignal): Promise<WireRow[]> {
    return getJson<WireRow[]>(
        `/api/journal-entries?skip=${skip}&take=${take}`,
        signal,
        'load journal entries',
    );
}

function toLeg(wire: WireLeg): JournalEntryLeg {
    return {
        accountId: asAccountId(wire.accountId),
        accountName: wire.accountName,
    };
}

function toRow(wire: WireRow): JournalEntryRow {
    const lineCount = typeof wire.lineCount === 'string' ? Number(wire.lineCount) : wire.lineCount;
    return {
        id: asJournalEntryId(wire.id),
        date: wire.date,
        description: wire.description,
        bankTransactionId: wire.bankTransactionId
            ? asBankTransactionId(wire.bankTransactionId)
            : null,
        counterpartyId: wire.counterpartyId ? asCounterpartyId(wire.counterpartyId) : null,
        counterpartyName: wire.counterpartyName,
        lineCount,
        isTransfer: wire.isTransfer,
        netWorthChange: toMoney(wire.netWorthChange),
        grossMagnitude: toMoney(wire.grossMagnitude),
        isSimplifiable: wire.isSimplifiable,
        fromLegs: wire.fromLegs.map(toLeg),
        toLegs: wire.toLegs.map(toLeg),
    };
}

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

function toEntry(wire: WireDetail): JournalEntry {
    return {
        ...toRow(wire),
        lines: wire.lines.map(toLine),
    };
}

export function useJournalEntries(skip: number, take: number) {
    return useQuery({
        queryKey: journalEntriesKeys.list(skip, take),
        queryFn: async ({ signal }) => {
            const wire = await fetchList(skip, take, signal);
            return wire.map(toRow);
        },
    });
}

export function useJournalEntry(id: JournalEntryId) {
    return useQuery({
        queryKey: journalEntriesKeys.detail(id),
        queryFn: async ({ signal }) => {
            const wire = await getJson<WireDetail>(
                `/api/journal-entries/${id}`,
                signal,
                'load journal entry',
            );
            return toEntry(wire);
        },
    });
}

/**
 * Builds the `UpdateJournalEntryInput` shape from a loaded entry. Lines are
 * keyed by line id (D-format Guid string, matching what the server expects)
 * so that `compare()` produces stable `/lines/{id}/description` paths.
 */
export function toUpdateInput(entry: JournalEntry): WireUpdateInput {
    return {
        date: entry.date,
        description: entry.description,
        counterpartyId: entry.counterpartyId,
        lines: Object.fromEntries(
            entry.lines.map(line => [line.id, { description: line.description }]),
        ),
    };
}

export function useUpdateJournalEntry() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (args: {
            id: JournalEntryId;
            original: WireUpdateInput;
            edited: WireUpdateInput;
        }) => {
            const patch = compare(args.original, args.edited);
            const wire = await patchJson<WireDetail>(
                `/api/journal-entries/${args.id}`,
                patch,
                new AbortController().signal,
                'update journal entry',
            );
            return toEntry(wire);
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
