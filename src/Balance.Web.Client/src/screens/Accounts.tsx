import { useState } from 'react';
import { useNavigate } from '@tanstack/react-router';
import { Trans, useLingui } from '@lingui/react/macro';
import { msg } from '@lingui/core/macro';
import type { MessageDescriptor } from '@lingui/core';
import { accountIdentifier, useAccounts, useDeleteAccount, type Account } from '../api/accounts';
import { AccountAvatar } from '../components/AccountAvatar';
import { AccountTreeSections, type AccountRowContext } from '../components/AccountTree';
import { TreeExpandButton } from '../components/ui/Tree';
import { Amount } from '../components/Amount';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { ErrorState } from '../components/ErrorState';
import { Icon } from '../components/Icon';
import { Panel, SectionHead } from '../components/Panel';
import { Skeleton } from '../components/Skeleton';
import { useToast } from '../components/ui/Toast';
import type { AccountType } from '../lib/domain';
import { handleActionError } from '../lib/formErrors';
import { AccountFormModal } from './AccountForm';

const TYPE_ORDER: AccountType[] = ['Asset', 'Liability', 'Equity', 'Income', 'Expense'];

const TYPE_LABELS: Record<AccountType, MessageDescriptor> = {
    Asset: msg`Assets`,
    Liability: msg`Liabilities`,
    Equity: msg`Equity`,
    Income: msg`Income`,
    Expense: msg`Expenses`,
};

export function Accounts() {
    const { t } = useLingui();
    const [creating, setCreating] = useState(false);
    const [editing, setEditing] = useState<Account | null>(null);
    const [deleting, setDeleting] = useState<Account | null>(null);

    return (
        <>
            <Panel>
                <SectionHead
                    subtitle={t`Ledger accounts in the double-entry sense: assets, liabilities, income, expenses, equity.`}
                    action={
                        <button
                            type="button"
                            onClick={() => {
                                setCreating(true);
                            }}
                            className="inline-flex items-center gap-2 px-3 py-[7px] rounded-lg bg-brand-primary text-white text-sm font-medium hover:bg-brand-primary-dark"
                        >
                            <Icon name="plus" size={14} strokeWidth={2} />
                            <Trans>New account</Trans>
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
    const { t } = useLingui();
    const navigate = useNavigate();
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
            <ErrorState message={t`Couldn't load accounts.`} onRetry={() => void query.refetch()} />
        );
    }

    if (query.data.length === 0) {
        return (
            <div className="py-8 flex flex-col items-center gap-2 text-center">
                <span className="text-sm text-fg-2">
                    <Trans>No accounts yet.</Trans>
                </span>
                <span className="text-xs text-fg-3">
                    <Trans>Add your first ledger account to get started.</Trans>
                </span>
            </div>
        );
    }

    return (
        <AccountTreeSections
            accounts={query.data}
            typeOrder={TYPE_ORDER}
            typeLabels={TYPE_LABELS}
            defaultExpandedKeys="all"
            onAction={key => {
                void navigate({
                    to: '/accounts/$id',
                    params: { id: key },
                    search: {
                        page: 1,
                        q: '',
                        posted: '',
                        counter: '',
                        from: '',
                        to: '',
                        status: '',
                    },
                });
            }}
            renderHeading={label => (
                <h3 className="text-xs font-medium text-fg-3 tracking-widest uppercase pb-1 mb-1 border-b border-border-soft">
                    {label}
                </h3>
            )}
            renderRow={(account, ctx) => (
                <AccountRow account={account} ctx={ctx} onEdit={onEdit} onDelete={onDelete} />
            )}
        />
    );
}

function AccountRow({
    account,
    ctx,
    onEdit,
    onDelete,
}: {
    account: Account;
    ctx: AccountRowContext;
    onEdit: (a: Account) => void;
    onDelete: (a: Account) => void;
}) {
    const { t } = useLingui();
    const identifier = accountIdentifier(account);
    const isNegative = account.balance.amount < 0;
    return (
        <div
            className="flex items-center gap-3 py-3 pr-1 border-b border-border-soft cursor-pointer rounded-lg group-data-[hovered]:bg-surface-2 group-data-[focus-visible]:bg-surface-2 transition-colors"
            style={{ paddingLeft: `${String((ctx.level - 1) * 1.25)}rem` }}
        >
            {ctx.hasChildren ? (
                <TreeExpandButton
                    ariaLabel={ctx.isExpanded ? t`Collapse` : t`Expand`}
                    isExpanded={ctx.isExpanded}
                />
            ) : (
                <span className="shrink-0 w-[22px]" aria-hidden="true" />
            )}
            <AccountAvatar account={account} size="md" />
            <div className="flex flex-col gap-[2px] flex-1 min-w-0">
                <span className="flex items-center gap-2 min-w-0">
                    {!account.isPostable ? (
                        <Icon
                            name="folder-tree"
                            size={14}
                            strokeWidth={1.75}
                            className="shrink-0 text-fg-3"
                            aria-label={t`Roll-up account`}
                        />
                    ) : null}
                    <span
                        className={`text-sm truncate ${account.isPostable ? 'font-medium text-fg-1' : 'font-semibold text-fg-2'}`}
                    >
                        {account.name}
                    </span>
                </span>
                <span className="text-xs text-fg-3 truncate tabular-nums">
                    {account.code}
                    {identifier ? ` · ${identifier}` : ''}
                </span>
            </div>
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
                aria-label={t`Edit`}
                className="p-2 rounded-lg text-fg-3 hover:text-fg-1 hover:bg-surface-2"
            >
                <Icon name="pencil" size={14} strokeWidth={2} />
            </button>
            <button
                type="button"
                onClick={() => {
                    onDelete(account);
                }}
                aria-label={t`Delete`}
                className="p-2 rounded-lg text-fg-3 hover:text-danger hover:bg-surface-2"
            >
                <Icon name="trash" size={14} strokeWidth={2} />
            </button>
        </div>
    );
}

function DeleteAccountDialog({ account, onClose }: { account: Account; onClose: () => void }) {
    const { t } = useLingui();
    const del = useDeleteAccount();
    const toast = useToast();
    const [error, setError] = useState<string | null>(null);

    async function onConfirm() {
        setError(null);
        try {
            await del.mutateAsync(account.id);
            toast.success(t`Deleted “${account.name}”.`);
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
            title={t`Delete “${account.name}”?`}
            message={t`This can't be undone. Accounts with journal entries can't be deleted until those are removed first.`}
            confirmLabel={t`Delete`}
            variant="destructive"
            busy={del.isPending}
            error={error}
        />
    );
}
