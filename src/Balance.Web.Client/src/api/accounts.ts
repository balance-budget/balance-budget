import { useQuery } from '@tanstack/react-query';
import type { components } from '../lib/api-types';
import { type AccountId, type AccountType, asAccountId } from '../lib/domain';
import { getJson } from '../lib/http';
import { toMoney, type Money } from '../lib/money';
import { createResourceCrud } from '../lib/resourceApi';

type WireAccount = components['schemas']['AccountOutput'];
type WirePagedAccounts = components['schemas']['PagedOutputOfAccountOutput'];
type WireBankAccountSummary = components['schemas']['BankAccountSummary'];
type WireCreateRequest = components['schemas']['CreateAccountRequest'];
type WireUpdateInput = components['schemas']['UpdateAccountInput'];

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
    code: string;
    type: AccountType;
    currencyCode: string;
    /** A leaf that journal lines may reference directly; false for a non-postable roll-up account. */
    isPostable: boolean;
    /** Parent in the chart-of-accounts tree, or null for a root account (ADR-0019). */
    parentId: AccountId | null;
    balance: Money;
    bankAccount: BankAccountSummary | null;
};

export const accountsKeys = {
    all: ['accounts'] as const,
    list: () => [...accountsKeys.all, 'list'] as const,
    detail: (id: AccountId) => [...accountsKeys.all, 'detail', id] as const,
};

async function fetchAccounts(signal: AbortSignal): Promise<WireAccount[]> {
    const wire = await getJson<WirePagedAccounts>('/api/accounts', signal, 'load accounts');
    return wire.items;
}

function toBankAccountSummary(wire: WireBankAccountSummary | null): BankAccountSummary | null {
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
        code: wire.code,
        type: wire.accountType,
        currencyCode: wire.currencyCode,
        isPostable: wire.isPostable,
        parentId: wire.parentAccountId === null ? null : asAccountId(wire.parentAccountId),
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

const crud = createResourceCrud<
    WireAccount,
    Account,
    WireCreateRequest,
    WireUpdateInput,
    AccountId
>({
    basePath: '/api/accounts',
    label: 'account',
    allKey: accountsKeys.all,
    detailKey: accountsKeys.detail,
    toView: toAccount,
});

export const useAccount = crud.useDetail;
export const useCreateAccount = crud.useCreate;
export const useUpdateAccount = crud.useUpdate;
export const useDeleteAccount = crud.useDelete;

/** Linked bank identifier for an Account row — IBAN if present, otherwise the
 *  account number, or null when nothing's linked. Returned verbatim; consumers
 *  rely on CSS `truncate` to fit constrained layouts (e.g. the sidebar). */
export function accountIdentifier(account: Account): string | null {
    return account.bankAccount?.iban ?? account.bankAccount?.accountNumber ?? null;
}
