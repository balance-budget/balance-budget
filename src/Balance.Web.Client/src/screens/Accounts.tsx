import { useState } from 'react';
import { Link } from '@tanstack/react-router';
import { accountIdentifier, useAccounts, useDeleteAccount, type Account } from '../api/accounts';
import { AccountAvatar } from '../components/AccountAvatar';
import { Amount } from '../components/Amount';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { ErrorState } from '../components/ErrorState';
import { Icon } from '../components/Icon';
import { Panel, SectionHead } from '../components/Panel';
import { Skeleton } from '../components/Skeleton';
import { useToast } from '../components/Toast';
import type { AccountType } from '../lib/domain';
import { ApiError } from '../lib/http';
import { AccountFormModal } from './AccountForm';

const TYPE_ORDER: AccountType[] = ['Asset', 'Liability', 'Equity', 'Income', 'Expense'];
const TYPE_LABEL: Record<AccountType, string> = {
    Asset: 'Assets',
    Liability: 'Liabilities',
    Equity: 'Equity',
    Income: 'Income',
    Expense: 'Expenses',
};

export function Accounts() {
    const [creating, setCreating] = useState(false);
    const [editing, setEditing] = useState<Account | null>(null);
    const [deleting, setDeleting] = useState<Account | null>(null);

    return (
        <>
            <Panel>
                <SectionHead
                    title="Accounts"
                    subtitle="Ledger accounts in the double-entry sense — assets, liabilities, income, expenses, equity."
                    action={
                        <button
                            type="button"
                            onClick={() => {
                                setCreating(true);
                            }}
                            className="inline-flex items-center gap-2 px-3 py-[7px] rounded-sm bg-brand-primary text-white text-[13px] font-medium hover:bg-brand-primary-dark"
                        >
                            <Icon name="plus" size={14} strokeWidth={2} />
                            New account
                        </button>
                    }
                />
                <AccountList onEdit={setEditing} onDelete={setDeleting} />
            </Panel>

            {creating && (
                <AccountFormModal
                    mode="create"
                    onClose={() => {
                        setCreating(false);
                    }}
                />
            )}
            {editing && (
                <AccountFormModal
                    mode="edit"
                    account={editing}
                    onClose={() => {
                        setEditing(null);
                    }}
                />
            )}
            {deleting && (
                <DeleteAccountDialog
                    account={deleting}
                    onClose={() => {
                        setDeleting(null);
                    }}
                />
            )}
        </>
    );
}

function AccountList({
    onEdit,
    onDelete,
}: {
    onEdit: (a: Account) => void;
    onDelete: (a: Account) => void;
}) {
    const query = useAccounts();

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
            <ErrorState message="Couldn't load accounts." onRetry={() => void query.refetch()} />
        );
    }

    if (query.data.length === 0) {
        return (
            <div className="py-8 flex flex-col items-center gap-2 text-center">
                <span className="text-[14px] text-fg-2">No accounts yet.</span>
                <span className="text-[12px] text-fg-3">
                    Add your first ledger account to get started.
                </span>
            </div>
        );
    }

    const groups = groupByType(query.data);

    return (
        <div className="flex flex-col gap-5">
            {TYPE_ORDER.map(type => {
                const items = groups.get(type);
                if (!items || items.length === 0) return null;
                return (
                    <div key={type} className="flex flex-col">
                        <h3 className="eyebrow pb-1 mb-1 border-b border-border-soft">
                            {TYPE_LABEL[type]}
                        </h3>
                        {items.map(a => (
                            <AccountRow
                                key={a.id}
                                account={a}
                                onEdit={onEdit}
                                onDelete={onDelete}
                            />
                        ))}
                    </div>
                );
            })}
        </div>
    );
}

function groupByType(accounts: Account[]): Map<AccountType, Account[]> {
    const map = new Map<AccountType, Account[]>();
    for (const a of accounts) {
        const bucket = map.get(a.type) ?? [];
        bucket.push(a);
        map.set(a.type, bucket);
    }
    for (const bucket of map.values()) {
        bucket.sort((x, y) => x.name.localeCompare(y.name));
    }
    return map;
}

function AccountRow({
    account,
    onEdit,
    onDelete,
}: {
    account: Account;
    onEdit: (a: Account) => void;
    onDelete: (a: Account) => void;
}) {
    const identifier = accountIdentifier(account);
    const isNegative = account.balance.amount < 0;
    return (
        <div className="py-3 first:pt-0 last:pb-0 flex items-center gap-3 border-b border-border-soft last:border-b-0">
            <Link
                to="/accounts/$id"
                params={{ id: account.id }}
                className="flex items-center gap-3 flex-1 min-w-0 hover:text-brand-primary"
            >
                <AccountAvatar account={account} size="md" />
                <div className="flex flex-col gap-[2px] flex-1 min-w-0">
                    <span className="text-14 font-medium text-fg-1 truncate">{account.name}</span>
                    {identifier ? (
                        <span className="text-[12px] text-fg-3 truncate tabular">{identifier}</span>
                    ) : null}
                </div>
            </Link>
            <Amount
                minor={account.balance.amount}
                currencyCode={account.balance.currencyCode}
                size="inline"
                decimals={false}
                className={isNegative ? 'text-danger' : ''}
            />
            <button
                type="button"
                onClick={() => {
                    onEdit(account);
                }}
                aria-label="Edit"
                className="p-2 rounded-sm text-fg-3 hover:text-fg-1 hover:bg-surface-2"
            >
                <Icon name="pencil" size={14} strokeWidth={2} />
            </button>
            <button
                type="button"
                onClick={() => {
                    onDelete(account);
                }}
                aria-label="Delete"
                className="p-2 rounded-sm text-fg-3 hover:text-danger hover:bg-surface-2"
            >
                <Icon name="trash" size={14} strokeWidth={2} />
            </button>
        </div>
    );
}

function DeleteAccountDialog({ account, onClose }: { account: Account; onClose: () => void }) {
    const del = useDeleteAccount();
    const toast = useToast();
    const [error, setError] = useState<string | null>(null);

    async function onConfirm() {
        setError(null);
        try {
            await del.mutateAsync(account.id);
            toast.success(`Deleted “${account.name}”.`);
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
            title={`Delete “${account.name}”?`}
            message="This can't be undone. Accounts with journal entries can't be deleted until those are removed first."
            confirmLabel="Delete"
            variant="destructive"
            busy={del.isPending}
            error={error}
        />
    );
}
