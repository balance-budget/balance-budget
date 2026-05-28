import { useState } from 'react';
import { Link, useNavigate } from '@tanstack/react-router';
import { useAccount, useDeleteAccount, type Account } from '../api/accounts';
import { useCurrencyCatalog } from '../api/currencies';
import { useAccountRegister, type RegisterRow } from '../api/register';
import { AccountAvatar } from '../components/AccountAvatar';
import { Amount } from '../components/Amount';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { ErrorState } from '../components/ErrorState';
import { Icon } from '../components/Icon';
import { Panel, SectionHead } from '../components/Panel';
import { Skeleton } from '../components/Skeleton';
import { useToast } from '../components/Toast';
import { cx } from '../lib/cx';
import type { AccountId } from '../lib/domain';
import { ApiError } from '../lib/http';
import { formatMoney } from '../lib/money';
import { AccountFormModal } from './AccountForm';
import { LinkedBankAccountsSection } from './LinkedBankAccounts';

const REGISTER_PAGE_SIZE = 100;

export function AccountDetail({ id }: { id: AccountId }) {
    const query = useAccount(id);
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
                <ErrorState message="Couldn't load account." onRetry={() => void query.refetch()} />
            </Panel>
        );
    }

    const account = query.data;

    return (
        <>
            <Panel>
                <div className="flex items-start justify-between gap-3">
                    <div className="flex items-center gap-3 min-w-0">
                        <AccountAvatar account={account} size="md" />
                        <div className="flex flex-col gap-[2px] min-w-0">
                            <Link to="/accounts" className="text-[12px] text-fg-3 hover:text-fg-1">
                                ← Accounts
                            </Link>
                            <h1 className="text-[22px] font-medium text-fg-1 truncate">
                                {account.name}
                            </h1>
                            <span className="text-[12px] text-fg-3">
                                {account.type} · {account.currencyCode}
                            </span>
                        </div>
                    </div>
                    <div className="flex items-center gap-3 shrink-0">
                        <Amount
                            minor={account.balance.amount}
                            currencyCode={account.balance.currencyCode}
                            size="big"
                            className={account.balance.amount < 0 ? 'text-danger' : ''}
                        />
                        <div className="flex items-center gap-2">
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
                </div>
            </Panel>

            <Panel>
                <SectionHead title="Linked bank account" />
                <LinkedBankAccountsSection owner={{ kind: 'account', id: account.id }} />
            </Panel>

            <Panel>
                <SectionHead title="Register" subtitle="Chronological activity on this account." />
                <RegisterTable account={account} />
            </Panel>

            {editing && (
                <AccountFormModal
                    mode="edit"
                    account={account}
                    onClose={() => {
                        setEditing(false);
                    }}
                />
            )}
            {deleting && (
                <DeleteAccountDialog
                    account={account}
                    onClose={() => {
                        setDeleting(false);
                    }}
                />
            )}
        </>
    );
}

function RegisterTable({ account }: { account: Account }) {
    const register = useAccountRegister(account.id, 0, REGISTER_PAGE_SIZE);
    const catalog = useCurrencyCatalog();

    if (register.isPending) {
        return (
            <div className="flex flex-col gap-2">
                <Skeleton className="h-8 w-full" />
                <Skeleton className="h-8 w-full" />
                <Skeleton className="h-8 w-full" />
            </div>
        );
    }

    if (register.isError) {
        return (
            <ErrorState message="Couldn't load register." onRetry={() => void register.refetch()} />
        );
    }

    if (register.data.length === 0) {
        return (
            <div className="py-6 text-center text-[13px] text-fg-3">No journal entries yet.</div>
        );
    }

    return (
        <div className="flex flex-col">
            <div className="hidden md:grid grid-cols-[100px_1fr_180px_120px] gap-3 px-2 pb-2 text-[11px] text-fg-3 uppercase tracking-wider border-b border-border-soft">
                <span>Date</span>
                <span>Description</span>
                <span>Counter</span>
                <span className="text-right">Amount</span>
            </div>
            {register.data.map(row => (
                <RegisterRowView key={row.journalLineId} row={row} catalog={catalog} />
            ))}
            {register.data.length === REGISTER_PAGE_SIZE && (
                <p className="pt-3 text-center text-[12px] text-fg-3">
                    Showing the most recent {REGISTER_PAGE_SIZE} entries.
                </p>
            )}
        </div>
    );
}

function RegisterRowView({
    row,
    catalog,
}: {
    row: RegisterRow;
    catalog: ReturnType<typeof useCurrencyCatalog>;
}) {
    const counter = row.counter[0];
    const extra = row.counter.length - 1;
    const negative = row.amount.amount < 0;
    const heading = row.counterpartyName ?? row.entryDescription ?? '—';
    const amount = (
        <span
            className={cx(
                'font-mono text-[13px] tabular text-right',
                negative ? 'text-fg-1' : 'text-success',
            )}
        >
            {formatMoney(row.amount.amount, row.amount.currencyCode, catalog, { sign: true })}
        </span>
    );
    const counterLabel = (
        <span className="text-[12px] text-fg-2 truncate">
            {counter ? counter.accountName : '—'}
            {extra > 0 ? <span className="text-fg-3"> +{extra}</span> : null}
        </span>
    );
    return (
        <div className="border-b border-border-soft last:border-b-0 hover:bg-surface-2">
            <div className="hidden md:grid grid-cols-[100px_1fr_180px_120px] gap-3 items-center px-2 py-2">
                <span className="text-[12px] text-fg-3 tabular">{row.date}</span>
                <div className="flex flex-col min-w-0">
                    <span className="text-[13px] text-fg-1 truncate">{heading}</span>
                    {row.lineDescription ? (
                        <span className="text-[12px] text-fg-3 truncate">
                            {row.lineDescription}
                        </span>
                    ) : null}
                </div>
                {counterLabel}
                {amount}
            </div>
            <div className="md:hidden flex flex-col gap-1 px-2 py-3">
                <div className="flex items-center justify-between gap-3">
                    <span className="text-[12px] text-fg-3 tabular">{row.date}</span>
                    {amount}
                </div>
                <span className="text-[13px] text-fg-1 truncate">{heading}</span>
                {row.lineDescription ? (
                    <span className="text-[12px] text-fg-3 truncate">{row.lineDescription}</span>
                ) : null}
                {counterLabel}
            </div>
        </div>
    );
}

function DeleteAccountDialog({ account, onClose }: { account: Account; onClose: () => void }) {
    const del = useDeleteAccount();
    const toast = useToast();
    const navigate = useNavigate();
    const [error, setError] = useState<string | null>(null);

    async function onConfirm() {
        setError(null);
        try {
            await del.mutateAsync(account.id);
            toast.success(`Deleted “${account.name}”.`);
            await navigate({ to: '/accounts' });
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
