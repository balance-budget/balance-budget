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
import type { Page } from '../lib/paging';

type WireBankTransaction = components['schemas']['BankTransactionOutput'];
type WirePagedBankTransactions = components['schemas']['PagedOutputOfBankTransactionOutput'];
type WireBankTransactionDetail = components['schemas']['BankTransactionDetailOutput'];
type WireBankTransactionMetadataEntry = components['schemas']['BankTransactionMetadataEntryOutput'];
type WireCategorizeRequest = components['schemas']['CategorizeBankTransactionRequest'];
type WireJournalEntry = components['schemas']['JournalEntryOutput'];
type WireJournalEntryDetail = components['schemas']['JournalEntryDetailOutput'];
type WireAttachHint = components['schemas']['AttachHintOutput'];
type WireAttachCandidate = components['schemas']['AttachCandidateOutput'];

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
    valueDate: string | null;
    reference: string | null;
    mandateId: string | null;
    sepaCreditorId: string | null;
    foreignAmount: number | null;
    foreignCurrencyCode: string | null;
    exchangeRate: number | null;
    importerKey: string | null;
    journalEntryId: JournalEntryId | null;
    dismissedAt: string | null;
    dismissedReason: string | null;
    matchingJournalEntry: AttachHint | null;
};

export type AttachHint = {
    id: JournalEntryId;
    date: string;
    description: string | null;
    otherAccountName: string;
};

export type AttachCandidate = {
    id: JournalEntryId;
    date: string;
    description: string | null;
    otherAccountName: string;
    amount: number;
};

export type BankTransactionMetadataEntry = {
    key: string;
    stringValue: string | null;
    integerValue: number | null;
};

export type BankTransactionDetail = BankTransaction & {
    metadata: BankTransactionMetadataEntry[];
};

export const bankTransactionsKeys = {
    all: ['bank-transactions'] as const,
    list: (skip: number, take: number, filter: BankTransactionFilter, q: string) =>
        [...bankTransactionsKeys.all, 'list', { skip, take, filter, q }] as const,
    detail: (id: BankTransactionId) => [...bankTransactionsKeys.all, 'detail', id] as const,
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
        valueDate: wire.valueDate,
        reference: wire.reference,
        mandateId: wire.mandateId,
        sepaCreditorId: wire.sepaCreditorId,
        foreignAmount: wire.foreignAmount === null ? null : Number(wire.foreignAmount),
        foreignCurrencyCode: wire.foreignCurrencyCode,
        exchangeRate: wire.exchangeRate === null ? null : Number(wire.exchangeRate),
        importerKey: wire.importerKey,
        journalEntryId: wire.journalEntryId ? asJournalEntryId(wire.journalEntryId) : null,
        dismissedAt: wire.dismissedAt,
        dismissedReason: wire.dismissedReason,
        matchingJournalEntry: wire.matchingJournalEntry
            ? toAttachHint(wire.matchingJournalEntry)
            : null,
    };
}

function toAttachHint(wire: WireAttachHint): AttachHint {
    return {
        id: asJournalEntryId(wire.id),
        date: wire.date,
        description: wire.description,
        otherAccountName: wire.otherAccountName,
    };
}

function toAttachCandidate(wire: WireAttachCandidate): AttachCandidate {
    return {
        id: asJournalEntryId(wire.id),
        date: wire.date,
        description: wire.description,
        otherAccountName: wire.otherAccountName,
        amount: Number(wire.amount),
    };
}

function toBankTransactionMetadataEntry(
    wire: WireBankTransactionMetadataEntry,
): BankTransactionMetadataEntry {
    return {
        key: wire.key,
        stringValue: wire.stringValue,
        integerValue: wire.integerValue === null ? null : Number(wire.integerValue),
    };
}

export function toBankTransactionDetail(wire: WireBankTransactionDetail): BankTransactionDetail {
    return {
        ...toBankTransaction(wire),
        metadata: wire.metadata.map(toBankTransactionMetadataEntry),
    };
}

export function useBankTransactions(
    skip: number,
    take: number,
    filter: BankTransactionFilter,
    q: string,
) {
    return useQuery({
        queryKey: bankTransactionsKeys.list(skip, take, filter, q),
        queryFn: async ({ signal }): Promise<Page<BankTransaction>> => {
            const params = new URLSearchParams({
                skip: String(skip),
                take: String(take),
                filter,
            });
            if (q !== '') {
                params.set('q', q);
            }
            const wire = await getJson<WirePagedBankTransactions>(
                `/api/bank-transactions?${params.toString()}`,
                signal,
                'load bank transactions',
            );
            return {
                items: wire.items.map(toBankTransaction),
                totalCount: Number(wire.totalCount),
            };
        },
    });
}

export function useBankTransaction(id: BankTransactionId) {
    return useQuery({
        queryKey: bankTransactionsKeys.detail(id),
        queryFn: async ({ signal }) => {
            const wire = await getJson<WireBankTransactionDetail>(
                `/api/bank-transactions/${id}`,
                signal,
                'load bank transaction',
            );
            return toBankTransactionDetail(wire);
        },
    });
}

export function useCategorizeBankTransaction() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (args: { id: BankTransactionId; request: WireCategorizeRequest }) => {
            return await postJson<WireJournalEntry>(
                `/api/bank-transactions/${args.id}/categorize`,
                args.request,
                new AbortController().signal,
                'categorise bank transaction',
            );
        },
        onSuccess: async () => {
            await queryClient.invalidateQueries({ queryKey: bankTransactionsKeys.all });
            await queryClient.invalidateQueries({ queryKey: ['journalEntries'] });
            await queryClient.invalidateQueries({ queryKey: ['counterparties'] });
            await queryClient.invalidateQueries({ queryKey: ['bank-accounts'] });
            await queryClient.invalidateQueries({ queryKey: ['accounts'] });
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

export function useAttachBankTransaction() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (args: { id: BankTransactionId; journalEntryId: JournalEntryId }) => {
            return await postJson<WireJournalEntryDetail>(
                `/api/bank-transactions/${args.id}/attach`,
                { journalEntryId: args.journalEntryId },
                new AbortController().signal,
                'attach bank transaction',
            );
        },
        onSuccess: async () => {
            await queryClient.invalidateQueries({ queryKey: bankTransactionsKeys.all });
            await queryClient.invalidateQueries({ queryKey: ['journalEntries'] });
        },
    });
}

export function useDetachBankTransaction() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (id: BankTransactionId) => {
            return await postJson<WireJournalEntryDetail>(
                `/api/bank-transactions/${id}/detach`,
                {},
                new AbortController().signal,
                'detach bank transaction',
            );
        },
        onSuccess: async () => {
            await queryClient.invalidateQueries({ queryKey: bankTransactionsKeys.all });
            await queryClient.invalidateQueries({ queryKey: ['journalEntries'] });
        },
    });
}

export function useAttachCandidates(id: BankTransactionId | null, dateWindowDays: number) {
    return useQuery({
        queryKey: [...bankTransactionsKeys.all, 'attach-candidates', id, dateWindowDays] as const,
        enabled: id !== null,
        queryFn: async ({ signal }) => {
            const wire = await getJson<WireAttachCandidate[]>(
                `/api/bank-transactions/${id}/attach-candidates?dateWindowDays=${dateWindowDays}`,
                signal,
                'load attach candidates',
            );
            return wire.map(toAttachCandidate);
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
