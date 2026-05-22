import { useQuery } from '@tanstack/react-query';
import type { components } from '../lib/api-types';
import { type AccountId, type AccountType, asAccountId } from '../lib/domain';
import { toMoney, type Money } from '../lib/money';

type WireAccount = components['schemas']['AccountOutput'];
type WireBankAccountSummary = components['schemas']['BankAccountSummary'];

export type { Money };

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
