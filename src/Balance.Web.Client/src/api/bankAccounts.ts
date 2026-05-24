import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { components } from '../lib/api-types';
import { asAccountId, asBankAccountId, type AccountId, type BankAccountId } from '../lib/domain';
import { getJson, postFormData } from '../lib/http';

type WireBankAccount = components['schemas']['BankAccountOutput'];
type WireImportResult = components['schemas']['ImportResult'];

export type BankAccount = {
    id: BankAccountId;
    iban: string | null;
    accountNumber: string | null;
    bic: string | null;
    bankName: string | null;
    accountHolderName: string | null;
    currencyCode: string | null;
    accountId: AccountId | null;
};

export type ImportResult = {
    imported: number;
    skippedAsDuplicate: number;
};

export const bankAccountsKeys = {
    all: ['bank-accounts'] as const,
    list: () => [...bankAccountsKeys.all, 'list'] as const,
};

function fetchBankAccounts(signal: AbortSignal): Promise<WireBankAccount[]> {
    return getJson<WireBankAccount[]>('/api/bank-accounts', signal, 'load bank accounts');
}

function toBankAccount(wire: WireBankAccount): BankAccount {
    return {
        id: asBankAccountId(wire.id),
        iban: wire.iban,
        accountNumber: wire.accountNumber,
        bic: wire.bic,
        bankName: wire.bankName,
        accountHolderName: wire.accountHolderName,
        currencyCode: wire.currencyCode,
        accountId: wire.accountId ? asAccountId(wire.accountId) : null,
    };
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
