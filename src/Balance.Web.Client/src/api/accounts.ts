import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { compare } from 'fast-json-patch';
import type { components } from '../lib/api-types';
import { type AccountId, type AccountType, asAccountId } from '../lib/domain';
import { deleteRequest, getJson, patchJson, postJson } from '../lib/http';
import { toMoney, type Money } from '../lib/money';

type WireAccount = components['schemas']['AccountOutput'];
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
    type: AccountType;
    currencyCode: string;
    balance: Money;
    bankAccount: BankAccountSummary | null;
};

export const accountsKeys = {
    all: ['accounts'] as const,
    list: () => [...accountsKeys.all, 'list'] as const,
    detail: (id: AccountId) => [...accountsKeys.all, 'detail', id] as const,
};

function fetchAccounts(signal: AbortSignal): Promise<WireAccount[]> {
    return getJson<WireAccount[]>('/api/accounts', signal, 'load accounts');
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

export function useAccount(id: AccountId) {
    return useQuery({
        queryKey: accountsKeys.detail(id),
        queryFn: async ({ signal }) => {
            const wire = await getJson<WireAccount>(`/api/accounts/${id}`, signal, 'load account');
            return toAccount(wire);
        },
    });
}

export function useCreateAccount() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (input: WireCreateRequest) => {
            const wire = await postJson<WireAccount>(
                '/api/accounts',
                input,
                new AbortController().signal,
                'create account',
            );
            return toAccount(wire);
        },
        onSuccess: async () => {
            await queryClient.invalidateQueries({ queryKey: accountsKeys.all });
        },
    });
}

export function useUpdateAccount() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (args: {
            id: AccountId;
            original: WireUpdateInput;
            edited: WireUpdateInput;
        }) => {
            const patch = compare(args.original, args.edited);
            const wire = await patchJson<WireAccount>(
                `/api/accounts/${args.id}`,
                patch,
                new AbortController().signal,
                'update account',
            );
            return toAccount(wire);
        },
        onSuccess: async (_data, vars) => {
            await queryClient.invalidateQueries({ queryKey: accountsKeys.all });
            await queryClient.invalidateQueries({ queryKey: accountsKeys.detail(vars.id) });
        },
    });
}

export function useDeleteAccount() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (id: AccountId) => {
            await deleteRequest(
                `/api/accounts/${id}`,
                new AbortController().signal,
                'delete account',
            );
        },
        onSuccess: async () => {
            await queryClient.invalidateQueries({ queryKey: accountsKeys.all });
        },
    });
}

/** Linked bank identifier for an Account row — IBAN if present, otherwise the
 *  account number, or null when nothing's linked. Returned verbatim; consumers
 *  rely on CSS `truncate` to fit constrained layouts (e.g. the sidebar). */
export function accountIdentifier(account: Account): string | null {
    return account.bankAccount?.iban ?? account.bankAccount?.accountNumber ?? null;
}
