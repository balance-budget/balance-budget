import { useQuery } from '@tanstack/react-query';
import type { components } from '../lib/api-types';
import {
    asAccountId,
    asBankAccountId,
    asCounterpartyId,
    asJournalEntryId,
    type AccountId,
    type AccountType,
    type BankAccountId,
    type CounterpartyId,
    type JournalEntryId,
} from '../lib/domain';
import { getJson } from '../lib/http';

type WireSearchOutput = components['schemas']['SearchOutput'];

export type BankAccountType = 'Current' | 'Savings' | 'Card';

export type AccountHit = {
    id: AccountId;
    name: string;
    accountType: AccountType;
};

export type CounterpartyHit = {
    id: CounterpartyId;
    name: string;
};

export type BankAccountHit = {
    id: BankAccountId;
    type: BankAccountType;
    iban: string | null;
    accountNumber: string | null;
    cardIdentifier: string | null;
    bankName: string | null;
    accountHolderName: string | null;
};

export type JournalEntryHit = {
    id: JournalEntryId;
    date: string;
    description: string | null;
};

export type PageHit = {
    label: string;
    route: string;
};

export type SearchSection<T> = {
    items: T[];
    totalCount: number;
};

export type SearchResult = {
    accounts: SearchSection<AccountHit>;
    counterparties: SearchSection<CounterpartyHit>;
    bankAccounts: SearchSection<BankAccountHit>;
    journalEntries: SearchSection<JournalEntryHit>;
    pages: SearchSection<PageHit>;
};

function toAccount(wire: WireSearchOutput['accounts']['items'][number]): AccountHit {
    return {
        id: asAccountId(wire.id),
        name: wire.name,
        accountType: wire.accountType,
    };
}

function toCounterparty(
    wire: WireSearchOutput['counterparties']['items'][number],
): CounterpartyHit {
    return { id: asCounterpartyId(wire.id), name: wire.name };
}

function toBankAccount(
    wire: WireSearchOutput['bankAccounts']['items'][number],
): BankAccountHit {
    return {
        id: asBankAccountId(wire.id),
        type: wire.type,
        iban: wire.iban,
        accountNumber: wire.accountNumber,
        cardIdentifier: wire.cardIdentifier,
        bankName: wire.bankName,
        accountHolderName: wire.accountHolderName,
    };
}

function toJournalEntry(
    wire: WireSearchOutput['journalEntries']['items'][number],
): JournalEntryHit {
    return { id: asJournalEntryId(wire.id), date: wire.date, description: wire.description };
}

function toPage(wire: WireSearchOutput['pages']['items'][number]): PageHit {
    return { label: wire.label, route: wire.route };
}

export const searchKeys = {
    all: ['search'] as const,
    query: (q: string) => [...searchKeys.all, q] as const,
};

/**
 * Launcher query. The minimum length is enforced in the hook itself (queries
 * shorter than 2 chars are disabled) so the SPA never fires a request the
 * server would reject with 400 anyway.
 */
export function useSearch(q: string) {
    const trimmed = q.trim();
    const enabled = trimmed.length >= 2;
    return useQuery({
        queryKey: searchKeys.query(trimmed),
        enabled,
        queryFn: async ({ signal }): Promise<SearchResult> => {
            const wire = await getJson<WireSearchOutput>(
                `/api/search?q=${encodeURIComponent(trimmed)}`,
                signal,
                'search',
            );
            return {
                accounts: {
                    items: wire.accounts.items.map(toAccount),
                    totalCount: Number(wire.accounts.totalCount),
                },
                counterparties: {
                    items: wire.counterparties.items.map(toCounterparty),
                    totalCount: Number(wire.counterparties.totalCount),
                },
                bankAccounts: {
                    items: wire.bankAccounts.items.map(toBankAccount),
                    totalCount: Number(wire.bankAccounts.totalCount),
                },
                journalEntries: {
                    items: wire.journalEntries.items.map(toJournalEntry),
                    totalCount: Number(wire.journalEntries.totalCount),
                },
                pages: {
                    items: wire.pages.items.map(toPage),
                    totalCount: Number(wire.pages.totalCount),
                },
            };
        },
    });
}
