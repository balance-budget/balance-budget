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
import { useToast } from '../components/ui/Toast';
import type { AccountType } from '../lib/domain';
import { handleActionError } from '../lib/formErrors';
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
                            className="inline-flex items-center gap-2 px-3 py-[7px] rounded-sm bg-brand-primary text-white text-13 font-medium hover:bg-brand-primary-dark"
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
                <span className="text-14 text-fg-2">No accounts yet.</span>
                <span className="text-12 text-fg-3">
                    Add your first ledger account to get started.
                </span>
            </div>
        );
    }

    const childrenByParent = buildChildrenMap(query.data);
    const rootsByType = groupRootsByType(query.data);

    return (
        <div className="flex flex-col gap-5">
            {TYPE_ORDER.map(type => {
                const roots = rootsByType.get(type);
                if (!roots || roots.length === 0) return null;
                return (
                    <div key={type} className="flex flex-col">
                        <h3 className="eyebrow pb-1 mb-1 border-b border-border-soft">
                            {TYPE_LABEL[type]}
                        </h3>
                        {roots.map(a => (
                            <AccountTreeRows
                                key={a.id}
                                account={a}
                                depth={0}
                                childrenByParent={childrenByParent}
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

const sortSiblings = (a: Account, b: Account) =>
    a.code.localeCompare(b.code, undefined, { numeric: true }) || a.name.localeCompare(b.name);

/** Maps a parent id to its sorted children; the `null` key holds the roots. */
function buildChildrenMap(accounts: Account[]): Map<string | null, Account[]> {
    const map = new Map<string | null, Account[]>();
    for (const a of accounts) {
        const key = a.parentId;
        const bucket = map.get(key) ?? [];
        bucket.push(a);
        map.set(key, bucket);
    }
    for (const bucket of map.values()) bucket.sort(sortSiblings);
    return map;
}

function groupRootsByType(accounts: Account[]): Map<AccountType, Account[]> {
    const map = new Map<AccountType, Account[]>();
    for (const a of accounts) {
        if (a.parentId !== null) continue;
        const bucket = map.get(a.type) ?? [];
        bucket.push(a);
        map.set(a.type, bucket);
    }
    for (const bucket of map.values()) bucket.sort(sortSiblings);
    return map;
}

/** Renders an account row followed by its descendant rows, indented by depth. */
function AccountTreeRows({
    account,
    depth,
    childrenByParent,
    onEdit,
    onDelete,
}: {
    account: Account;
    depth: number;
    childrenByParent: Map<string | null, Account[]>;
    onEdit: (a: Account) => void;
    onDelete: (a: Account) => void;
}) {
    const children = childrenByParent.get(account.id) ?? [];
    return (
        <>
            <AccountRow account={account} depth={depth} onEdit={onEdit} onDelete={onDelete} />
            {children.map(child => (
                <AccountTreeRows
                    key={child.id}
                    account={child}
                    depth={depth + 1}
                    childrenByParent={childrenByParent}
                    onEdit={onEdit}
                    onDelete={onDelete}
                />
            ))}
        </>
    );
}

function AccountRow({
    account,
    depth,
    onEdit,
    onDelete,
}: {
    account: Account;
    depth: number;
    onEdit: (a: Account) => void;
    onDelete: (a: Account) => void;
}) {
    const identifier = accountIdentifier(account);
    const isNegative = account.balance.amount < 0;
    // Nest children visually; the indent is applied to the row's leading edge.
    const indent = { paddingLeft: `${String(depth * 1.25)}rem` };
    return (
        <div
            className="py-3 first:pt-0 last:pb-0 flex items-center gap-3 border-b border-border-soft last:border-b-0"
            style={indent}
        >
            <Link
                to="/accounts/$id"
                params={{ id: account.id }}
                search={{
                    page: 1,
                    q: '',
                    posted: '',
                    counter: '',
                    from: '',
                    to: '',
                    status: '',
                }}
                className="flex items-center gap-3 flex-1 min-w-0 hover:text-brand-primary"
            >
                <AccountAvatar account={account} size="md" />
                <div className="flex flex-col gap-[2px] flex-1 min-w-0">
                    <span className="flex items-center gap-2 min-w-0">
                        {!account.isPostable ? (
                            <Icon
                                name="folder-tree"
                                size={14}
                                strokeWidth={1.75}
                                className="shrink-0 text-fg-3"
                                aria-label="Roll-up account"
                            />
                        ) : null}
                        <span
                            className={`text-14 truncate ${account.isPostable ? 'font-medium text-fg-1' : 'font-semibold text-fg-2'}`}
                        >
                            {account.name}
                        </span>
                    </span>
                    <span className="text-12 text-fg-3 truncate tabular">
                        {account.code}
                        {identifier ? ` · ${identifier}` : ''}
                    </span>
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
            handleActionError(err, { setError, toast: toast.error });
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
