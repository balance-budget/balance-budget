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

export type BankTransactionFilter = 'Inbox' | 'Matched' | 'Dismissed' | 'All';

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
    list: (filter: BankTransactionFilter, skip: number, take: number) =>
        [...bankTransactionsKeys.all, 'list', { filter, skip, take }] as const,
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

export function useBankTransactions(filter: BankTransactionFilter, skip: number, take: number) {
    return useQuery({
        queryKey: bankTransactionsKeys.list(filter, skip, take),
        queryFn: async ({ signal }) => {
            const params = new URLSearchParams({
                Filter: filter,
                Skip: String(skip),
                Take: String(take),
            });
            const wire = await getJson<WireBankTransaction[]>(
                `/api/bank-transactions?${params.toString()}`,
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
