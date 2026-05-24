import { useQuery } from '@tanstack/react-query';
import type { components } from '../lib/api-types';
import {
    type AccountId,
    type BankTransactionId,
    type CounterpartyId,
    type JournalEntryId,
    asAccountId,
    asBankTransactionId,
    asCounterpartyId,
    asJournalEntryId,
} from '../lib/domain';
import { getJson } from '../lib/http';
import { toMoney, type Money } from '../lib/money';

type WireRow = components['schemas']['JournalEntryRowOutput'];
type WireLeg = components['schemas']['JournalEntryLegSummary'];

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

export function useJournalEntries(skip: number, take: number) {
    return useQuery({
        queryKey: journalEntriesKeys.list(skip, take),
        queryFn: async ({ signal }) => {
            const wire = await fetchList(skip, take, signal);
            return wire.map(toRow);
        },
    });
}
