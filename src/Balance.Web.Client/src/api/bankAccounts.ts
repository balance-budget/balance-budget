import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useLingui } from '@lingui/react/macro';
import type { components } from '../lib/api-types.gen';
import {
    asAccountId,
    asBankAccountId,
    asCounterpartyId,
    type AccountId,
    type BankAccountId,
    type CounterpartyId,
} from '../lib/domain';
import { getJson, postFormData } from '../lib/http';
import { toNumber } from '../lib/money';
import type { Page } from '../lib/paging';
import { createResourceCrud } from '../lib/resourceApi';

type WireBankAccount = components['schemas']['BankAccountOutput'];
type WirePagedBankAccounts = components['schemas']['PagedOutputOfBankAccountOutput'];
type WireBankAccountImporter = components['schemas']['BankAccountImporterOutput'];
type WireCreateRequest = components['schemas']['CreateBankAccountRequest'];
type WireUpdateInput = components['schemas']['UpdateBankAccountInput'];
type WireImportResult = components['schemas']['ImportResult'];
type WireDetectedImportOutcome = components['schemas']['DetectedImportOutcome'];

export type BankAccountType = 'Current' | 'Savings' | 'Card';

/** Owner facet for the bank-accounts list: your own BankAccounts vs. counterparties'.
 *  PascalCase to bind directly to the server's BankAccountOwnerFilter enum query param. */
export type BankAccountOwnerFilter = 'Mine' | 'Others';

export const BANK_ACCOUNT_OWNER_FILTERS: readonly BankAccountOwnerFilter[] = ['Mine', 'Others'];

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
    bankName: string;
    supportedType: BankAccountType;
};

export type ImportResult = {
    imported: number;
    skippedAsDuplicate: number;
};

export type ImportFileStatus = WireDetectedImportOutcome['status'];

export type DetectedImportOutcome = {
    fileName: string;
    status: ImportFileStatus;
    bankAccountId: BankAccountId | null;
    accountAnchor: string | null;
    imported: number;
    skippedAsDuplicate: number;
    detail: string | null;
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

/**
 * Composes a friendly importer label — the bank's proper noun plus the translated account-type
 * word (e.g. "ING · Current account") — instead of exposing the raw ImporterKey. The type word
 * goes through Lingui so it stays translatable (ADR-0022/0034); the bank name is a proper noun
 * supplied by the backend and is never translated.
 */
export function useImporterLabel(): (importer: {
    bankName: string;
    supportedType: BankAccountType;
}) => string {
    const { t } = useLingui();
    return ({ bankName, supportedType }) => {
        const typeWord =
            supportedType === 'Savings'
                ? t`Savings account`
                : supportedType === 'Card'
                  ? t`Credit card`
                  : t`Current account`;
        return `${bankName} · ${typeWord}`;
    };
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
    page: (skip: number, take: number, q: string, owner: BankAccountOwnerFilter) =>
        [...bankAccountsKeys.all, 'page', { skip, take, q, owner }] as const,
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
    return { key: wire.key, bankName: wire.bankName, supportedType: wire.supportedType };
}

function toImportResult(wire: WireImportResult): ImportResult {
    return {
        imported: toNumber(wire.imported),
        skippedAsDuplicate: toNumber(wire.skippedAsDuplicate),
    };
}

function toDetectedImportOutcome(wire: WireDetectedImportOutcome): DetectedImportOutcome {
    return {
        fileName: wire.fileName,
        status: wire.status,
        bankAccountId: wire.bankAccountId ? asBankAccountId(wire.bankAccountId) : null,
        accountAnchor: wire.accountAnchor ?? null,
        imported: toNumber(wire.imported),
        skippedAsDuplicate: toNumber(wire.skippedAsDuplicate),
        detail: wire.detail ?? null,
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
 * Server-side paged + searchable bank accounts for the overview screen. Search
 * matches the account's own identifiers plus the linked owner's name; `owner`
 * facets to your own accounts vs. counterparties'. The unpaginated
 * {@link useBankAccounts} remains for dropdowns and linked-account sections.
 */
export function useBankAccountsPage(
    skip: number,
    take: number,
    q: string,
    owner: BankAccountOwnerFilter,
) {
    return useQuery({
        queryKey: bankAccountsKeys.page(skip, take, q, owner),
        queryFn: async ({ signal }): Promise<Page<BankAccount>> => {
            const params = new URLSearchParams({
                skip: String(skip),
                take: String(take),
                owner,
            });
            if (q !== '') {
                params.set('q', q);
            }
            const wire = await getJson<WirePagedBankAccounts>(
                `/api/bank-accounts?${params.toString()}`,
                signal,
                'load bank accounts',
            );
            return {
                items: wire.items.map(toBankAccount),
                totalCount: Number(wire.totalCount),
            };
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

const crud = createResourceCrud<
    WireBankAccount,
    BankAccount,
    WireCreateRequest,
    WireUpdateInput,
    BankAccountId
>({
    basePath: '/api/bank-accounts',
    label: 'bank account',
    allKey: bankAccountsKeys.all,
    detailKey: bankAccountsKeys.detail,
    toView: toBankAccount,
});

export const useBankAccount = crud.useDetail;
export const useCreateBankAccount = crud.useCreate;
export const useUpdateBankAccount = crud.useUpdate;
export const useDeleteBankAccount = crud.useDelete;

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

/**
 * Drop-and-detect upload (ADR-0034): posts N files to /api/imports with no chosen
 * account. The server detects each file's target and imports the unambiguous ones,
 * returning a per-file outcome; unresolved files are resolved manually via
 * `useImportStatement` against a user-picked account.
 */
export function useDetectAndImportStatements() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (files: File[]) => {
            const formData = new FormData();
            for (const file of files) formData.append('files', file);
            const wire = await postFormData<WireDetectedImportOutcome[]>(
                '/api/imports',
                formData,
                'detect and import statements',
            );
            return wire.map(toDetectedImportOutcome);
        },
        onSuccess: async () => {
            await queryClient.invalidateQueries({ queryKey: bankAccountsKeys.all });
        },
    });
}
