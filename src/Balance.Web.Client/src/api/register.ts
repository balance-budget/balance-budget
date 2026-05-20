import { useQuery } from '@tanstack/react-query';
import type { components } from '../lib/api-types';
import {
    type AccountId,
    type JournalEntryId,
    type JournalLineId,
    asAccountId,
    asJournalEntryId,
    asJournalLineId,
} from '../lib/domain';
import type { Money } from './accounts';

type WireRegisterRow = components['schemas']['RegisterRowOutput'];
type WireCounterLeg = components['schemas']['RegisterRowCounterLeg'];
type WireMoney = components['schemas']['Money'];

export type ReconciliationStatus = 'Uncleared' | 'Cleared' | 'Reconciled';

export type RegisterCounterLeg = {
    accountId: AccountId;
    accountName: string;
    amount: Money;
};

export type RegisterRow = {
    journalEntryId: JournalEntryId;
    journalLineId: JournalLineId;
    date: string;
    entryDescription: string | null;
    counterpartyId: string | null;
    counterpartyName: string | null;
    lineDescription: string | null;
    reconciliationStatus: ReconciliationStatus;
    amount: Money;
    counter: RegisterCounterLeg[];
};

export const registerKeys = {
    all: ['accounts', 'register'] as const,
    list: (accountId: AccountId, skip: number, take: number) =>
        [...registerKeys.all, accountId, { skip, take }] as const,
};

async function fetchRegister(
    accountId: AccountId,
    skip: number,
    take: number,
    signal: AbortSignal,
): Promise<WireRegisterRow[]> {
    const url = `/api/accounts/${accountId}/register?skip=${skip}&take=${take}`;
    const response = await fetch(url, { signal });
    if (!response.ok) {
        throw new Error(`Failed to load register (${response.status})`);
    }
    return (await response.json()) as WireRegisterRow[];
}

function toMoney(wire: WireMoney, fallbackCurrencyCode: string): Money {
    const raw = wire.amount;
    const amount = typeof raw === 'string' ? Number(raw) : (raw ?? 0);
    return {
        amount,
        currencyCode: wire.currencyCode ?? fallbackCurrencyCode,
    };
}

function toCounterLeg(wire: WireCounterLeg): RegisterCounterLeg {
    return {
        accountId: asAccountId(wire.accountId),
        accountName: wire.accountName,
        amount: toMoney(wire.amount, ''),
    };
}

function toRegisterRow(wire: WireRegisterRow): RegisterRow {
    const focal = toMoney(wire.amount, '');
    return {
        journalEntryId: asJournalEntryId(wire.journalEntryId),
        journalLineId: asJournalLineId(wire.journalLineId),
        date: wire.date,
        entryDescription: wire.entryDescription,
        // CounterpartyId is typed as `unknown` by openapi-typescript (the OpenAPI
        // schema emits the typed-id as a bare `unknown` until the transformer
        // covers nullable references). Coerce here at the boundary.
        counterpartyId: (wire.counterpartyId as string | null) ?? null,
        counterpartyName: wire.counterpartyName,
        lineDescription: wire.lineDescription,
        reconciliationStatus: wire.reconciliationStatus,
        amount: focal,
        counter: wire.counter.map(toCounterLeg),
    };
}

export function useAccountRegister(accountId: AccountId, take: number) {
    return useQuery({
        queryKey: registerKeys.list(accountId, 0, take),
        queryFn: async ({ signal }) => {
            const wire = await fetchRegister(accountId, 0, take, signal);
            return wire.map(toRegisterRow);
        },
    });
}
