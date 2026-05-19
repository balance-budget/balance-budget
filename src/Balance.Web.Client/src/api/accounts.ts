import { useQuery } from '@tanstack/react-query';
import type { components } from '../lib/api-types';
import { type AccountId, type AccountType, asAccountId } from '../lib/domain';

type WireAccount = components['schemas']['AccountOutput'];
type WireMoney = components['schemas']['Money'];
type WireBankAccountSummary = components['schemas']['BankAccountSummary'];

export type Money = {
    amount: number;
    currencyCode: string;
};

export type BankAccountSummary = {
    iban: string | null;
    accountNumber: string | null;
    bic: string | null;
    bankName: string | null;
};

export type Account = {
    id: AccountId;
    name: string;
    type: AccountType;
    currencyCode: string;
    balance: Money;
    bankAccount: BankAccountSummary | null;
};

export const accountsKeys = {
    all: ['accounts'] as const,
    list: () => [...accountsKeys.all, 'list'] as const,
};

async function fetchAccounts(signal: AbortSignal): Promise<WireAccount[]> {
    const response = await fetch('/api/accounts', { signal });
    if (!response.ok) {
        throw new Error(`Failed to load accounts (${response.status})`);
    }
    return (await response.json()) as WireAccount[];
}

function toMoney(wire: WireMoney, fallbackCurrencyCode: string): Money {
    // Money is serialised as { amount, currencyCode } but openapi-typescript marks both
    // as optional (System.Text.Json on a record struct). For ledger amounts that fit in
    // number safely, coerce to number; fall back to the account's currency if absent.
    const raw = wire.amount;
    const amount = typeof raw === 'string' ? Number(raw) : (raw ?? 0);
    return {
        amount,
        currencyCode: wire.currencyCode ?? fallbackCurrencyCode,
    };
}

function toBankAccountSummary(
    wire: WireBankAccountSummary | null,
): BankAccountSummary | null {
    if (wire === null) {
        return null;
    }
    return {
        iban: wire.iban,
        accountNumber: wire.accountNumber,
        bic: wire.bic,
        bankName: wire.bankName,
    };
}

function toAccount(wire: WireAccount): Account {
    return {
        id: asAccountId(wire.id),
        name: wire.name,
        type: wire.accountType,
        currencyCode: wire.currencyCode,
        balance: toMoney(wire.balance, wire.currencyCode),
        bankAccount: toBankAccountSummary(wire.bankAccount),
    };
}

export function useAccounts() {
    return useQuery({
        queryKey: accountsKeys.list(),
        queryFn: async ({ signal }) => {
            const wire = await fetchAccounts(signal);
            return wire.map(toAccount);
        },
    });
}
