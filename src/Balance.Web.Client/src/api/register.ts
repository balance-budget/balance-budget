import { useQuery } from '@tanstack/react-query';
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
import { getJson } from '../lib/http';
import { toMoney, type Money } from '../lib/money';
import { accountsKeys } from './accounts';

type WireRegisterRow = components['schemas']['RegisterRowOutput'];
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
    date: string;
    entryDescription: string | null;
    counterpartyId: CounterpartyId | null;
    counterpartyName: string | null;
    lineDescription: string | null;
    reconciliationStatus: ReconciliationStatus;
    amount: Money;
    counter: RegisterCounterLeg[];
};

export const registerKeys = {
    all: [...accountsKeys.all, 'register'] as const,
    list: (accountId: AccountId, skip: number, take: number) =>
        [...registerKeys.all, accountId, { skip, take }] as const,
};

function fetchRegister(
    accountId: AccountId,
    skip: number,
    take: number,
    signal: AbortSignal,
): Promise<WireRegisterRow[]> {
    return getJson<WireRegisterRow[]>(
        `/api/accounts/${accountId}/register?skip=${skip}&take=${take}`,
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
        date: wire.date,
        entryDescription: wire.entryDescription,
        // CounterpartyId is typed as `unknown` by openapi-typescript (the OpenAPI
        // schema emits the typed-id as a bare `unknown` until the transformer
        // covers nullable references). Coerce here at the boundary.
        counterpartyId: wire.counterpartyId
            ? asCounterpartyId(wire.counterpartyId as string)
            : null,
        counterpartyName: wire.counterpartyName,
        lineDescription: wire.lineDescription,
        reconciliationStatus: wire.reconciliationStatus,
        amount: focal,
        counter: wire.counter.map(toCounterLeg),
    };
}

export function useAccountRegister(accountId: AccountId, skip: number, take: number) {
    return useQuery({
        queryKey: registerKeys.list(accountId, skip, take),
        queryFn: async ({ signal }) => {
            const wire = await fetchRegister(accountId, skip, take, signal);
            return wire.map(toRegisterRow);
        },
    });
}
