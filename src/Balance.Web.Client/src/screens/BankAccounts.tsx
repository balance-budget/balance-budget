import { useState } from 'react';
import { Link } from '@tanstack/react-router';
import {
    BANK_ACCOUNT_OWNER_FILTERS,
    bankAccountTypeIcon,
    formatBankAccountLabel,
    formatBankAccountSubline,
    useBankAccount,
    useBankAccounts,
    useDeleteBankAccount,
    type BankAccount,
    type BankAccountOwnerFilter,
} from '../api/bankAccounts';
import { useAccounts } from '../api/accounts';
import { useCounterparties } from '../api/counterparties';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { ErrorState } from '../components/ErrorState';
import { Icon } from '../components/Icon';
import { Panel, SectionHead } from '../components/Panel';
import { Skeleton } from '../components/Skeleton';
import { useToast } from '../components/Toast';
import { cx } from '../lib/cx';
import type { BankAccountId } from '../lib/domain';
import { ApiError } from '../lib/http';
import { BankAccountFormModal } from './BankAccountForm';

const OWNER_FILTER_LABEL: Record<BankAccountOwnerFilter, string> = {
    mine: 'Mine',
    others: 'Others',
};

type Props = {
    owner: BankAccountOwnerFilter;
    onOwnerChange: (owner: BankAccountOwnerFilter) => void;
};

export function BankAccounts({ owner, onOwnerChange }: Props) {
    const [creating, setCreating] = useState(false);

    return (
        <>
            <Panel>
                <SectionHead
                    title="Bank accounts"
                    subtitle="The real-world bank accounts behind your ledger accounts and counterparties."
                    action={
                        <button
                            type="button"
                            onClick={() => {
                                setCreating(true);
                            }}
                            className="inline-flex items-center gap-2 px-3 py-[7px] rounded-sm bg-brand-primary text-white text-[13px] font-medium hover:bg-brand-primary-dark"
                        >
                            <Icon name="plus" size={14} strokeWidth={2} />
                            New bank account
                        </button>
                    }
                />
                <OwnerFilterChips value={owner} onChange={onOwnerChange} />
                <BankAccountList owner={owner} />
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
    return (
        <div className="flex items-center gap-2 mb-4" role="tablist" aria-label="Owner filter">
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
                            'px-3 py-1 rounded-sm text-[12px] font-medium select-none transition-colors',
                            active
                                ? 'bg-brand-primary-soft text-brand-primary'
                                : 'text-fg-2 hover:bg-surface-2 hover:text-fg-1',
                        )}
                    >
                        {OWNER_FILTER_LABEL[o]}
                    </button>
                );
            })}
        </div>
    );
}

function BankAccountList({ owner }: { owner: BankAccountOwnerFilter }) {
    const query = useBankAccounts();
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
                message="Couldn't load bank accounts."
                onRetry={() => void query.refetch()}
            />
        );
    }

    const filtered = query.data.filter(ba =>
        owner === 'mine' ? ba.accountId !== null : ba.counterpartyId !== null,
    );

    if (filtered.length === 0) {
        const title =
            owner === 'mine' ? 'No bank accounts of your own yet.' : 'No counterparty bank accounts.';
        const hint =
            owner === 'mine'
                ? 'Add one to attach to a ledger account.'
                : 'Counterparty bank accounts appear as you categorise imported transactions.';
        return (
            <div className="py-8 flex flex-col items-center gap-2 text-center">
                <span className="text-[14px] text-fg-2">{title}</span>
                <span className="text-[12px] text-fg-3">{hint}</span>
            </div>
        );
    }

    const accountsById = new Map((accounts.data ?? []).map(a => [a.id, a]));
    const counterpartiesById = new Map((counterparties.data ?? []).map(c => [c.id, c]));

    return (
        <div>
            {filtered.map(ba => (
                <BankAccountRow
                    key={ba.id}
                    bankAccount={ba}
                    ownerLabel={resolveOwnerLabel(ba, accountsById, counterpartiesById)}
                />
            ))}
        </div>
    );
}

function resolveOwnerLabel(
    ba: BankAccount,
    accountsById: Map<string, { name: string }>,
    counterpartiesById: Map<string, { name: string }>,
): string {
    if (ba.accountId) {
        return accountsById.get(ba.accountId)?.name ?? 'Unknown account';
    }
    if (ba.counterpartyId) {
        return counterpartiesById.get(ba.counterpartyId)?.name ?? 'Unknown counterparty';
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
    const ownerKind = bankAccount.accountId ? 'Account' : 'Counterparty';

    return (
        <Link
            to="/settings/bank-accounts/$id"
            params={{ id: bankAccount.id }}
            className="py-3 first:pt-0 last:pb-0 flex items-center gap-3 border-b border-border-soft last:border-b-0 hover:bg-surface-2 px-1 -mx-1 rounded-sm"
        >
            <span className="shrink-0 inline-flex items-center justify-center w-9 h-9 rounded-md bg-brand-primary-soft text-brand-primary">
                <Icon name={bankAccountTypeIcon(bankAccount.type)} size={16} strokeWidth={2} />
            </span>
            <div className="flex-1 min-w-0 flex flex-col leading-tight">
                <span className="text-14 font-medium text-fg-1 truncate">
                    {formatBankAccountLabel(bankAccount)}
                </span>
                <span className="text-[12px] text-fg-3 tabular truncate">
                    {formatBankAccountSubline(bankAccount)}
                </span>
            </div>
            <div className="shrink-0 flex flex-col items-end leading-tight">
                <span className="text-[11px] text-fg-3 uppercase tracking-wider">{ownerKind}</span>
                <span className="text-[12px] text-fg-2 truncate max-w-[160px]">{ownerLabel}</span>
            </div>
            <Icon name="chevron-right" size={14} className="text-fg-3" />
        </Link>
    );
}

export function BankAccountDetail({ id }: { id: BankAccountId }) {
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
                    message="Couldn't load bank account."
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
                            search={{ owner: 'mine' }}
                            className="text-[12px] text-fg-3 hover:text-fg-1"
                        >
                            ← Bank accounts
                        </Link>
                        <h1 className="text-[22px] font-medium text-fg-1 truncate">
                            {formatBankAccountLabel(ba)}
                        </h1>
                    </div>
                    <div className="flex items-center gap-2 lg:shrink-0">
                        <button
                            type="button"
                            onClick={() => {
                                setEditing(true);
                            }}
                            className="inline-flex items-center gap-2 px-3 py-[7px] rounded-sm text-[13px] font-medium text-fg-2 hover:text-fg-1 hover:bg-surface-2"
                        >
                            <Icon name="pencil" size={14} strokeWidth={2} />
                            Edit
                        </button>
                        <button
                            type="button"
                            onClick={() => {
                                setDeleting(true);
                            }}
                            className="inline-flex items-center gap-2 px-3 py-[7px] rounded-sm text-[13px] font-medium text-fg-2 hover:text-danger hover:bg-surface-2"
                        >
                            <Icon name="trash" size={14} strokeWidth={2} />
                            Delete
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
    return (
        <dl className="grid grid-cols-1 sm:grid-cols-2 gap-x-6 gap-y-3 text-[13px]">
            <Field label="Type" value={bankAccount.type} />
            <Field label="IBAN" value={bankAccount.iban} />
            <Field label="Account number" value={bankAccount.accountNumber} />
            <Field label="Card identifier" value={bankAccount.cardIdentifier} />
            <Field label="BIC" value={bankAccount.bic} />
            <Field label="Bank name" value={bankAccount.bankName} />
            <Field label="Account holder" value={bankAccount.accountHolderName} />
            <Field label="Currency" value={bankAccount.currencyCode} />
            <Field label="Importer" value={bankAccount.importerKey} />
        </dl>
    );
}

function Field({ label, value }: { label: string; value: string | null }) {
    return (
        <div className="flex flex-col gap-[2px]">
            <dt className="text-[11px] text-fg-3 uppercase tracking-wider">{label}</dt>
            <dd className="text-fg-1 tabular">{value ?? '—'}</dd>
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
    const del = useDeleteBankAccount();
    const toast = useToast();
    const [error, setError] = useState<string | null>(null);

    async function onConfirm() {
        setError(null);
        try {
            await del.mutateAsync(bankAccount.id);
            toast.success('Bank account deleted.');
            onClose();
        } catch (err) {
            if (err instanceof ApiError && err.status >= 400 && err.status < 500) {
                setError(err.message);
            } else if (err instanceof Error) {
                toast.error(err.message);
            }
        }
    }

    return (
        <ConfirmDialog
            open
            onClose={onClose}
            onConfirm={() => void onConfirm()}
            title="Delete this bank account?"
            message="This can't be undone."
            confirmLabel="Delete"
            variant="destructive"
            busy={del.isPending}
            error={error}
        />
    );
}
