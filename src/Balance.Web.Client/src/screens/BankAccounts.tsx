import { useState } from 'react';
import { Link } from '@tanstack/react-router';
import { Trans, useLingui } from '@lingui/react/macro';
import { t } from '@lingui/core/macro';
import {
    BANK_ACCOUNT_OWNER_FILTERS,
    bankAccountTypeIcon,
    formatBankAccountLabel,
    formatBankAccountSubline,
    useBankAccount,
    useBankAccountsPage,
    useDeleteBankAccount,
    type BankAccount,
    type BankAccountOwnerFilter,
} from '../api/bankAccounts';
import { useAccounts } from '../api/accounts';
import { useCounterparties } from '../api/counterparties';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { ErrorState } from '../components/ErrorState';
import { Icon } from '../components/Icon';
import { Pagination } from '../components/Pagination';
import { Panel, SectionHead } from '../components/Panel';
import { Skeleton } from '../components/Skeleton';
import { useToast } from '../components/ui/Toast';
import { SearchField } from '../components/ui/SearchField';
import { cx } from '../lib/cx';
import type { BankAccountId } from '../lib/domain';
import { handleActionError } from '../lib/formErrors';
import { useDebouncedValue } from '../lib/useDebouncedValue';
import { BankAccountFormModal } from './BankAccountForm';

const PAGE_SIZE = 50;

function ownerFilterLabel(o: BankAccountOwnerFilter, t: ReturnType<typeof useLingui>['t']): string {
    return o === 'Mine' ? t`Mine` : t`Others`;
}

type Props = {
    owner: BankAccountOwnerFilter;
    page: number;
    q: string;
    onOwnerChange: (owner: BankAccountOwnerFilter) => void;
    onPageChange: (page: number) => void;
    onSearchChange: (q: string) => void;
};

export function BankAccounts({
    owner,
    page,
    q,
    onOwnerChange,
    onPageChange,
    onSearchChange,
}: Props) {
    const { t } = useLingui();
    const [creating, setCreating] = useState(false);

    return (
        <>
            <Panel>
                <SectionHead
                    title={t`Bank accounts`}
                    subtitle={t`The real-world bank accounts behind your ledger accounts and counterparties.`}
                    action={
                        <button
                            type="button"
                            onClick={() => {
                                setCreating(true);
                            }}
                            className="inline-flex items-center gap-2 px-3 py-[7px] rounded-lg bg-brand-primary text-white text-sm font-medium hover:bg-brand-primary-dark"
                        >
                            <Icon name="plus" size={14} strokeWidth={2} />
                            <Trans>New bank account</Trans>
                        </button>
                    }
                />
                <OwnerFilterChips value={owner} onChange={onOwnerChange} />
                <div className="mb-4">
                    <SearchField
                        aria-label={t`Search bank accounts`}
                        value={q}
                        onChange={onSearchChange}
                        placeholder={t`Search bank accounts…`}
                    />
                </div>
                <BankAccountList owner={owner} page={page} q={q} onPageChange={onPageChange} />
            </Panel>

            {creating && (
                <BankAccountFormModal
                    mode="create"
                    onClose={() => {
                        setCreating(false);
                    }}
                />
            )}
        </>
    );
}

function OwnerFilterChips({
    value,
    onChange,
}: {
    value: BankAccountOwnerFilter;
    onChange: (owner: BankAccountOwnerFilter) => void;
}) {
    const { t } = useLingui();
    return (
        <div className="flex items-center gap-2 mb-4" role="tablist" aria-label={t`Owner filter`}>
            {BANK_ACCOUNT_OWNER_FILTERS.map(o => {
                const active = o === value;
                return (
                    <button
                        key={o}
                        type="button"
                        role="tab"
                        aria-selected={active}
                        onClick={() => {
                            onChange(o);
                        }}
                        className={cx(
                            'px-3 py-1 rounded-lg text-xs font-medium select-none transition-colors',
                            active
                                ? 'bg-brand-primary-soft text-brand-primary'
                                : 'text-fg-2 hover:bg-surface-2 hover:text-fg-1',
                        )}
                    >
                        {ownerFilterLabel(o, t)}
                    </button>
                );
            })}
        </div>
    );
}

function BankAccountList({
    owner,
    page,
    q,
    onPageChange,
}: {
    owner: BankAccountOwnerFilter;
    page: number;
    q: string;
    onPageChange: (page: number) => void;
}) {
    const { t } = useLingui();
    const skip = (page - 1) * PAGE_SIZE;
    const debouncedQ = useDebouncedValue(q, 200);
    const query = useBankAccountsPage(skip, PAGE_SIZE, debouncedQ, owner);
    const accounts = useAccounts();
    const counterparties = useCounterparties();

    if (query.isPending) {
        return (
            <div className="flex flex-col gap-3">
                <Skeleton className="h-12 w-full" />
                <Skeleton className="h-12 w-full" />
                <Skeleton className="h-12 w-full" />
            </div>
        );
    }

    if (query.isError) {
        return (
            <ErrorState
                message={t`Couldn't load bank accounts.`}
                onRetry={() => void query.refetch()}
            />
        );
    }

    const items = query.data.items;

    if (items.length === 0 && debouncedQ !== '') {
        return (
            <div className="py-8 text-center text-sm text-fg-2">
                <Trans>No matches for “{debouncedQ}”.</Trans>
            </div>
        );
    }

    if (items.length === 0 && page === 1) {
        const title =
            owner === 'Mine'
                ? t`No bank accounts of your own yet.`
                : t`No counterparty bank accounts.`;
        const hint =
            owner === 'Mine'
                ? t`Add one to attach to a ledger account.`
                : t`Counterparty bank accounts appear as you categorise imported transactions.`;
        return (
            <div className="py-8 flex flex-col items-center gap-2 text-center">
                <span className="text-sm text-fg-2">{title}</span>
                <span className="text-xs text-fg-3">{hint}</span>
            </div>
        );
    }

    const accountsById = new Map((accounts.data ?? []).map(a => [a.id, a]));
    const counterpartiesById = new Map((counterparties.data ?? []).map(c => [c.id, c]));

    return (
        <div>
            {items.map(ba => (
                <BankAccountRow
                    key={ba.id}
                    bankAccount={ba}
                    ownerLabel={resolveOwnerLabel(ba, accountsById, counterpartiesById)}
                />
            ))}
            <Pagination
                page={page}
                pageSize={PAGE_SIZE}
                totalCount={query.data.totalCount}
                onPageChange={onPageChange}
            />
        </div>
    );
}

function resolveOwnerLabel(
    ba: BankAccount,
    accountsById: Map<string, { name: string }>,
    counterpartiesById: Map<string, { name: string }>,
): string {
    if (ba.accountId) {
        return accountsById.get(ba.accountId)?.name ?? t`Unknown account`;
    }
    if (ba.counterpartyId) {
        return counterpartiesById.get(ba.counterpartyId)?.name ?? t`Unknown counterparty`;
    }
    return '—';
}

function BankAccountRow({
    bankAccount,
    ownerLabel,
}: {
    bankAccount: BankAccount;
    ownerLabel: string;
}) {
    const { t } = useLingui();
    const ownerKind = bankAccount.accountId ? t`Account` : t`Counterparty`;

    return (
        <Link
            to="/settings/bank-accounts/$id"
            params={{ id: bankAccount.id }}
            className="py-3 first:pt-0 last:pb-0 flex items-center gap-3 border-b border-border-soft last:border-b-0 hover:bg-surface-2 px-1 -mx-1 rounded-lg"
        >
            <span className="shrink-0 inline-flex items-center justify-center w-9 h-9 rounded-xl bg-brand-primary-soft text-brand-primary">
                <Icon name={bankAccountTypeIcon(bankAccount.type)} size={16} strokeWidth={2} />
            </span>
            <div className="flex-1 min-w-0 flex flex-col leading-tight">
                <span className="text-sm font-medium text-fg-1 truncate">
                    {formatBankAccountLabel(bankAccount)}
                </span>
                <span className="text-xs text-fg-3 tabular-nums truncate">
                    {formatBankAccountSubline(bankAccount)}
                </span>
            </div>
            <div className="shrink-0 flex flex-col items-end leading-tight">
                <span className="text-xs text-fg-3 uppercase tracking-wider">{ownerKind}</span>
                <span className="text-xs text-fg-2 truncate max-w-[160px]">{ownerLabel}</span>
            </div>
            <Icon name="chevron-right" size={14} className="text-fg-3" />
        </Link>
    );
}

export function BankAccountDetail({ id }: { id: BankAccountId }) {
    const { t } = useLingui();
    const query = useBankAccount(id);
    const [editing, setEditing] = useState(false);
    const [deleting, setDeleting] = useState(false);

    if (query.isPending) {
        return (
            <Panel>
                <Skeleton className="h-6 w-1/3 mb-3" />
                <Skeleton className="h-4 w-1/2" />
            </Panel>
        );
    }

    if (query.isError) {
        return (
            <Panel>
                <ErrorState
                    message={t`Couldn't load bank account.`}
                    onRetry={() => void query.refetch()}
                />
            </Panel>
        );
    }

    const ba = query.data;

    return (
        <>
            <Panel>
                <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between mb-4">
                    <div className="flex flex-col gap-[2px] min-w-0">
                        <Link
                            to="/settings/bank-accounts"
                            search={{ owner: 'Mine', page: 1, q: '' }}
                            className="text-xs text-fg-3 hover:text-fg-1"
                        >
                            ← <Trans>Bank accounts</Trans>
                        </Link>
                        <h1 className="text-xl font-medium text-fg-1 truncate">
                            {formatBankAccountLabel(ba)}
                        </h1>
                    </div>
                    <div className="flex items-center gap-2 lg:shrink-0">
                        <button
                            type="button"
                            onClick={() => {
                                setEditing(true);
                            }}
                            className="inline-flex items-center gap-2 px-3 py-[7px] rounded-lg text-sm font-medium text-fg-2 hover:text-fg-1 hover:bg-surface-2"
                        >
                            <Icon name="pencil" size={14} strokeWidth={2} />
                            <Trans>Edit</Trans>
                        </button>
                        <button
                            type="button"
                            onClick={() => {
                                setDeleting(true);
                            }}
                            className="inline-flex items-center gap-2 px-3 py-[7px] rounded-lg text-sm font-medium text-fg-2 hover:text-danger hover:bg-surface-2"
                        >
                            <Icon name="trash" size={14} strokeWidth={2} />
                            <Trans>Delete</Trans>
                        </button>
                    </div>
                </div>
                <BankAccountDetails bankAccount={ba} />
            </Panel>

            {editing && (
                <BankAccountFormModal
                    mode="edit"
                    bankAccount={ba}
                    onClose={() => {
                        setEditing(false);
                    }}
                />
            )}
            {deleting && (
                <DeleteBankAccountDialog
                    bankAccount={ba}
                    onClose={() => {
                        setDeleting(false);
                    }}
                />
            )}
        </>
    );
}

function BankAccountDetails({ bankAccount }: { bankAccount: BankAccount }) {
    const { t } = useLingui();
    return (
        <dl className="grid grid-cols-1 sm:grid-cols-2 gap-x-6 gap-y-3 text-sm">
            <Field label={t`Type`} value={bankAccount.type} />
            <Field label={t`IBAN`} value={bankAccount.iban} />
            <Field label={t`Account number`} value={bankAccount.accountNumber} />
            <Field label={t`Card identifier`} value={bankAccount.cardIdentifier} />
            <Field label={t`BIC`} value={bankAccount.bic} />
            <Field label={t`Bank name`} value={bankAccount.bankName} />
            <Field label={t`Account holder`} value={bankAccount.accountHolderName} />
            <Field label={t`Currency`} value={bankAccount.currencyCode} />
            <Field label={t`Importer`} value={bankAccount.importerKey} />
        </dl>
    );
}

function Field({ label, value }: { label: string; value: string | null }) {
    return (
        <div className="flex flex-col gap-[2px]">
            <dt className="text-xs text-fg-3 uppercase tracking-wider">{label}</dt>
            <dd className="text-fg-1 tabular-nums">{value ?? '—'}</dd>
        </div>
    );
}

function DeleteBankAccountDialog({
    bankAccount,
    onClose,
}: {
    bankAccount: BankAccount;
    onClose: () => void;
}) {
    const { t } = useLingui();
    const del = useDeleteBankAccount();
    const toast = useToast();
    const [error, setError] = useState<string | null>(null);

    async function onConfirm() {
        setError(null);
        try {
            await del.mutateAsync(bankAccount.id);
            toast.success(t`Bank account deleted.`);
            onClose();
        } catch (err) {
            handleActionError(err, { setError, toast: toast.error });
        }
    }

    return (
        <ConfirmDialog
            open
            onClose={onClose}
            onConfirm={() => void onConfirm()}
            title={t`Delete this bank account?`}
            message={t`This can't be undone.`}
            confirmLabel={t`Delete`}
            variant="destructive"
            busy={del.isPending}
            error={error}
        />
    );
}
