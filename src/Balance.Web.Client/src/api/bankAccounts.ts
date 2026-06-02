import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { compare } from 'fast-json-patch';
import type { components } from '../lib/api-types';
import {
    asAccountId,
    asBankAccountId,
    asCounterpartyId,
    type AccountId,
    type BankAccountId,
    type CounterpartyId,
} from '../lib/domain';
import { deleteRequest, getJson, patchJson, postFormData, postJson } from '../lib/http';

type WireBankAccount = components['schemas']['BankAccountOutput'];
type WirePagedBankAccounts = components['schemas']['PagedOutputOfBankAccountOutput'];
type WireBankAccountImporter = components['schemas']['BankAccountImporterOutput'];
type WireCreateRequest = components['schemas']['CreateBankAccountRequest'];
type WireUpdateInput = components['schemas']['UpdateBankAccountInput'];
type WireImportResult = components['schemas']['ImportResult'];

export type BankAccountType = 'Current' | 'Savings' | 'Card';

/** Owner facet for the bank-accounts list: your own BankAccounts vs. counterparties'. */
export type BankAccountOwnerFilter = 'mine' | 'others';

export const BANK_ACCOUNT_OWNER_FILTERS: readonly BankAccountOwnerFilter[] = ['mine', 'others'];

export type BankAccount = {
    id: BankAccountId;
    type: BankAccountType;
    iban: string | null;
    accountNumber: string | null;
    cardIdentifier: string | null;
    bic: string | null;
    bankName: string | null;
    accountHolderName: string | null;
    currencyCode: string | null;
    importerKey: string | null;
    accountId: AccountId | null;
    counterpartyId: CounterpartyId | null;
};

export type BankAccountImporter = {
    key: string;
    supportedType: BankAccountType;
};

export type ImportResult = {
    imported: number;
    skippedAsDuplicate: number;
};

const BANK_ACCOUNT_TYPE_LABEL: Record<BankAccountType, string> = {
    Current: 'Current',
    Savings: 'Savings',
    Card: 'Card',
};

export function formatBankAccountIdentifier(ba: BankAccount): string | null {
    return ba.iban ?? ba.accountNumber ?? ba.cardIdentifier;
}

export function formatBankAccountLabel(ba: BankAccount): string {
    return ba.bankName ?? formatBankAccountIdentifier(ba) ?? 'Bank account';
}

export function bankAccountTypeIcon(type: BankAccountType): string {
    switch (type) {
        case 'Savings':
            return 'piggy-bank';
        case 'Card':
            return 'credit-card';
        case 'Current':
            return 'landmark';
    }
}

export function formatBankAccountSubline(ba: BankAccount): string {
    const parts = [BANK_ACCOUNT_TYPE_LABEL[ba.type], formatBankAccountIdentifier(ba) ?? '—'];
    if (ba.currencyCode) parts.push(ba.currencyCode);
    return parts.join(' · ');
}

export const bankAccountsKeys = {
    all: ['bank-accounts'] as const,
    list: () => [...bankAccountsKeys.all, 'list'] as const,
    detail: (id: BankAccountId) => [...bankAccountsKeys.all, 'detail', id] as const,
    importers: () => [...bankAccountsKeys.all, 'importers'] as const,
};

async function fetchBankAccounts(signal: AbortSignal): Promise<WireBankAccount[]> {
    const wire = await getJson<WirePagedBankAccounts>(
        '/api/bank-accounts',
        signal,
        'load bank accounts',
    );
    return wire.items;
}

function toBankAccount(wire: WireBankAccount): BankAccount {
    return {
        id: asBankAccountId(wire.id),
        type: wire.type,
        iban: wire.iban,
        accountNumber: wire.accountNumber,
        cardIdentifier: wire.cardIdentifier,
        bic: wire.bic,
        bankName: wire.bankName,
        accountHolderName: wire.accountHolderName,
        currencyCode: wire.currencyCode,
        importerKey: wire.importerKey,
        accountId: wire.accountId ? asAccountId(wire.accountId) : null,
        counterpartyId: wire.counterpartyId ? asCounterpartyId(wire.counterpartyId) : null,
    };
}

function toBankAccountImporter(wire: WireBankAccountImporter): BankAccountImporter {
    return { key: wire.key, supportedType: wire.supportedType };
}

function toCount(raw: number | string): number {
    return typeof raw === 'string' ? Number(raw) : raw;
}

function toImportResult(wire: WireImportResult): ImportResult {
    return {
        imported: toCount(wire.imported),
        skippedAsDuplicate: toCount(wire.skippedAsDuplicate),
    };
}

export function useBankAccounts() {
    return useQuery({
        queryKey: bankAccountsKeys.list(),
        queryFn: async ({ signal }) => {
            const wire = await fetchBankAccounts(signal);
            return wire.map(toBankAccount);
        },
    });
}

export function useBankAccountImporters() {
    return useQuery({
        queryKey: bankAccountsKeys.importers(),
        queryFn: async ({ signal }) => {
            const wire = await getJson<WireBankAccountImporter[]>(
                '/api/bank-accounts/importers',
                signal,
                'load bank account importers',
            );
            return wire.map(toBankAccountImporter);
        },
        staleTime: 60_000,
    });
}

export function useBankAccount(id: BankAccountId) {
    return useQuery({
        queryKey: bankAccountsKeys.detail(id),
        queryFn: async ({ signal }) => {
            const wire = await getJson<WireBankAccount>(
                `/api/bank-accounts/${id}`,
                signal,
                'load bank account',
            );
            return toBankAccount(wire);
        },
    });
}

export function useCreateBankAccount() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (input: WireCreateRequest) => {
            const wire = await postJson<WireBankAccount>(
                '/api/bank-accounts',
                input,
                new AbortController().signal,
                'create bank account',
            );
            return toBankAccount(wire);
        },
        onSuccess: async () => {
            await queryClient.invalidateQueries({ queryKey: bankAccountsKeys.all });
        },
    });
}

export function useUpdateBankAccount() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (args: {
            id: BankAccountId;
            original: WireUpdateInput;
            edited: WireUpdateInput;
        }) => {
            const patch = compare(args.original, args.edited);
            const wire = await patchJson<WireBankAccount>(
                `/api/bank-accounts/${args.id}`,
                patch,
                new AbortController().signal,
                'update bank account',
            );
            return toBankAccount(wire);
        },
        onSuccess: async (_data, vars) => {
            await queryClient.invalidateQueries({ queryKey: bankAccountsKeys.all });
            await queryClient.invalidateQueries({
                queryKey: bankAccountsKeys.detail(vars.id),
            });
        },
    });
}

export function useDeleteBankAccount() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (id: BankAccountId) => {
            await deleteRequest(
                `/api/bank-accounts/${id}`,
                new AbortController().signal,
                'delete bank account',
            );
        },
        onSuccess: async () => {
            await queryClient.invalidateQueries({ queryKey: bankAccountsKeys.all });
        },
    });
}

/**
 * Multipart upload to the per-BankAccount statement importer. Returns the
 * `(Imported, SkippedAsDuplicate)` counts on success; throws ApiError carrying
 * the ProblemDetails message on 404/409/422/400.
 */
export function useImportStatement() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (args: { bankAccountId: BankAccountId; file: File }) => {
            const formData = new FormData();
            formData.append('file', args.file);
            const wire = await postFormData<WireImportResult>(
                `/api/bank-accounts/${args.bankAccountId}/imports`,
                formData,
                new AbortController().signal,
                'import statement',
            );
            return toImportResult(wire);
        },
        onSuccess: async (_result, vars) => {
            // BankTransactions for this BankAccount may have changed; no SPA surface
            // reads them yet, so invalidate broadly under the BankAccount key to
            // future-proof the next slice that lists them.
            await queryClient.invalidateQueries({
                queryKey: [...bankAccountsKeys.all, vars.bankAccountId],
            });
        },
    });
}
