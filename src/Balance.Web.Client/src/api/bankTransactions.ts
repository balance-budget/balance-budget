import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { components } from '../lib/api-types';
import {
    asBankAccountId,
    asBankTransactionId,
    asJournalEntryId,
    type BankAccountId,
    type BankTransactionId,
    type JournalEntryId,
} from '../lib/domain';
import { getJson, postJson } from '../lib/http';
import { toMoney, type Money } from '../lib/money';

type WireBankTransaction = components['schemas']['BankTransactionOutput'];

// Mirrors the BankTransactionListFilter enum on the server. The wire type
// allows null (openapi-typescript marks query-string enums as nullable), so we
// re-state it as a non-nullable view-model.
export type BankTransactionFilter = 'Inbox' | 'Matched' | 'Dismissed' | 'All';

export const BANK_TRANSACTION_FILTERS: readonly BankTransactionFilter[] = [
    'Inbox',
    'Matched',
    'Dismissed',
    'All',
] as const;

export type BankTransaction = {
    id: BankTransactionId;
    bankAccountId: BankAccountId;
    bookingDate: string;
    money: Money;
    description: string;
    counterpartyName: string | null;
    counterpartyAccountNumber: string | null;
    journalEntryId: JournalEntryId | null;
    dismissedAt: string | null;
    dismissedReason: string | null;
};

export const bankTransactionsKeys = {
    all: ['bank-transactions'] as const,
    list: (skip: number, take: number, filter: BankTransactionFilter) =>
        [...bankTransactionsKeys.all, 'list', { skip, take, filter }] as const,
};

function toBankTransaction(wire: WireBankTransaction): BankTransaction {
    return {
        id: asBankTransactionId(wire.id),
        bankAccountId: asBankAccountId(wire.bankAccountId),
        bookingDate: wire.bookingDate,
        money: toMoney(wire.money),
        description: wire.description,
        counterpartyName: wire.counterpartyName,
        counterpartyAccountNumber: wire.counterpartyAccountNumber,
        journalEntryId: wire.journalEntryId ? asJournalEntryId(wire.journalEntryId) : null,
        dismissedAt: wire.dismissedAt,
        dismissedReason: wire.dismissedReason,
    };
}

export function useBankTransactions(skip: number, take: number, filter: BankTransactionFilter) {
    return useQuery({
        queryKey: bankTransactionsKeys.list(skip, take, filter),
        queryFn: async ({ signal }) => {
            const wire = await getJson<WireBankTransaction[]>(
                `/api/bank-transactions?skip=${skip}&take=${take}&filter=${filter}`,
                signal,
                'load bank transactions',
            );
            return wire.map(toBankTransaction);
        },
    });
}

export function useDismissBankTransaction() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (args: { id: BankTransactionId; reason: string }) => {
            const wire = await postJson<WireBankTransaction>(
                `/api/bank-transactions/${args.id}/dismiss`,
                { reason: args.reason },
                new AbortController().signal,
                'dismiss bank transaction',
            );
            return toBankTransaction(wire);
        },
        onSuccess: async () => {
            await queryClient.invalidateQueries({ queryKey: bankTransactionsKeys.all });
        },
    });
}

export function useUndismissBankTransaction() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (id: BankTransactionId) => {
            const wire = await postJson<WireBankTransaction>(
                `/api/bank-transactions/${id}/undismiss`,
                {},
                new AbortController().signal,
                'undismiss bank transaction',
            );
            return toBankTransaction(wire);
        },
        onSuccess: async () => {
            await queryClient.invalidateQueries({ queryKey: bankTransactionsKeys.all });
        },
    });
}
