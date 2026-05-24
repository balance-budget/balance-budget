import { useState } from 'react';
import { Link, useNavigate } from '@tanstack/react-router';
import { useCounterparty, useDeleteCounterparty } from '../api/counterparties';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { ErrorState } from '../components/ErrorState';
import { Icon } from '../components/Icon';
import { Panel, SectionHead } from '../components/Panel';
import { Skeleton } from '../components/Skeleton';
import { useToast } from '../components/Toast';
import { type CounterpartyId } from '../lib/domain';
import { ApiError } from '../lib/http';
import { LinkedBankAccountsSection } from './LinkedBankAccounts';
import { CounterpartyFormModal } from './CounterpartyForm';

export function CounterpartyDetail({ id }: { id: CounterpartyId }) {
    const query = useCounterparty(id);
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
                    message="Couldn't load counterparty."
                    onRetry={() => void query.refetch()}
                />
            </Panel>
        );
    }

    const cp = query.data;

    return (
        <>
            <Panel>
                <div className="flex items-start justify-between gap-3 mb-4">
                    <div className="flex flex-col gap-[2px] min-w-0">
                        <Link
                            to="/settings/counterparties"
                            className="text-[12px] text-fg-3 hover:text-fg-1 inline-flex items-center gap-1"
                        >
                            ← Counterparties
                        </Link>
                        <h1 className="text-[22px] font-medium text-fg-1 truncate">{cp.name}</h1>
                    </div>
                    <div className="flex items-center gap-2 shrink-0">
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
            </Panel>

            <Panel>
                <SectionHead title="Linked bank accounts" />
                <LinkedBankAccountsSection owner={{ kind: 'counterparty', id: cp.id }} />
            </Panel>

            {editing && (
                <CounterpartyFormModal
                    mode="edit"
                    counterparty={cp}
                    onClose={() => {
                        setEditing(false);
                    }}
                />
            )}
            {deleting && (
                <DeleteCounterpartyDialog
                    name={cp.name}
                    id={cp.id}
                    onClose={() => {
                        setDeleting(false);
                    }}
                />
            )}
        </>
    );
}

function DeleteCounterpartyDialog({
    id,
    name,
    onClose,
}: {
    id: CounterpartyId;
    name: string;
    onClose: () => void;
}) {
    const del = useDeleteCounterparty();
    const toast = useToast();
    const navigate = useNavigate();
    const [error, setError] = useState<string | null>(null);

    async function onConfirm() {
        setError(null);
        try {
            await del.mutateAsync(id);
            toast.success(`Deleted “${name}”.`);
            await navigate({ to: '/settings/counterparties' });
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
            title={`Delete “${name}”?`}
            message="This can't be undone."
            confirmLabel="Delete"
            variant="destructive"
            busy={del.isPending}
            error={error}
        />
    );
}
