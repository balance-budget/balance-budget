import { useQuery } from '@tanstack/react-query';
import type { components } from '../lib/api-types';
import {
    asBankAccountId,
    asBankTransactionId,
    asJournalEntryId,
    type BankAccountId,
    type BankTransactionId,
    type JournalEntryId,
} from '../lib/domain';
import { getJson } from '../lib/http';
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
