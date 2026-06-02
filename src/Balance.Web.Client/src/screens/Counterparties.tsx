import { useState } from 'react';
import { Link } from '@tanstack/react-router';
import {
    useCounterpartiesPage,
    useDeleteCounterparty,
    type Counterparty,
} from '../api/counterparties';
import { ErrorState } from '../components/ErrorState';
import { Icon } from '../components/Icon';
import { Pagination } from '../components/Pagination';
import { Panel, SectionHead } from '../components/Panel';
import { SearchInput } from '../components/SearchInput';
import { Skeleton } from '../components/Skeleton';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { useToast } from '../components/Toast';
import { handleActionError } from '../lib/formErrors';
import { useDebouncedValue } from '../lib/useDebouncedValue';
import { CounterpartyFormModal } from './CounterpartyForm';

const PAGE_SIZE = 50;

export function Counterparties({
    page,
    q,
    onPageChange,
    onSearchChange,
}: {
    page: number;
    q: string;
    onPageChange: (p: number) => void;
    onSearchChange: (q: string) => void;
}) {
    const [creating, setCreating] = useState(false);
    const [editing, setEditing] = useState<Counterparty | null>(null);
    const [deleting, setDeleting] = useState<Counterparty | null>(null);

    return (
        <>
            <Panel>
                <SectionHead
                    title="Counterparties"
                    subtitle="Real-world parties on the other side of journal entries."
                    action={
                        <button
                            type="button"
                            onClick={() => {
                                setCreating(true);
                            }}
                            className="inline-flex items-center gap-2 px-3 py-[7px] rounded-sm bg-brand-primary text-white text-[13px] font-medium hover:bg-brand-primary-dark"
                        >
                            <Icon name="plus" size={14} strokeWidth={2} />
                            New counterparty
                        </button>
                    }
                />
                <div className="mb-4">
                    <SearchInput
                        value={q}
                        onChange={onSearchChange}
                        placeholder="Search counterparties…"
                    />
                </div>
                <CounterpartyList
                    page={page}
                    q={q}
                    onPageChange={onPageChange}
                    onEdit={setEditing}
                    onDelete={setDeleting}
                />
            </Panel>

            {creating && (
                <CounterpartyFormModal
                    mode="create"
                    onClose={() => {
                        setCreating(false);
                    }}
                />
            )}
            {editing && (
                <CounterpartyFormModal
                    mode="edit"
                    counterparty={editing}
                    onClose={() => {
                        setEditing(null);
                    }}
                />
            )}
            {deleting && (
                <DeleteCounterpartyDialog
                    counterparty={deleting}
                    onClose={() => {
                        setDeleting(null);
                    }}
                />
            )}
        </>
    );
}

function CounterpartyList({
    page,
    q,
    onPageChange,
    onEdit,
    onDelete,
}: {
    page: number;
    q: string;
    onPageChange: (p: number) => void;
    onEdit: (c: Counterparty) => void;
    onDelete: (c: Counterparty) => void;
}) {
    const skip = (page - 1) * PAGE_SIZE;
    const debouncedQ = useDebouncedValue(q, 200);
    const query = useCounterpartiesPage(skip, PAGE_SIZE, debouncedQ);

    if (query.isPending) {
        return (
            <div className="flex flex-col gap-3">
                <Skeleton className="h-10 w-full" />
                <Skeleton className="h-10 w-full" />
                <Skeleton className="h-10 w-full" />
            </div>
        );
    }

    if (query.isError) {
        return (
            <ErrorState
                message="Couldn't load counterparties."
                onRetry={() => void query.refetch()}
            />
        );
    }

    if (query.data.items.length === 0 && debouncedQ !== '') {
        return (
            <div className="py-8 text-center text-[14px] text-fg-2">
                No matches for “{debouncedQ}”.
            </div>
        );
    }

    if (query.data.items.length === 0 && page === 1) {
        return (
            <div className="py-8 flex flex-col items-center gap-2 text-center">
                <span className="text-[14px] text-fg-2">No counterparties yet.</span>
                <span className="text-[12px] text-fg-3">
                    Add the parties you receive money from or pay to.
                </span>
            </div>
        );
    }

    return (
        <div>
            {query.data.items.map(c => (
                <CounterpartyRow key={c.id} counterparty={c} onEdit={onEdit} onDelete={onDelete} />
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

function CounterpartyRow({
    counterparty,
    onEdit,
    onDelete,
}: {
    counterparty: Counterparty;
    onEdit: (c: Counterparty) => void;
    onDelete: (c: Counterparty) => void;
}) {
    return (
        <div className="py-3 first:pt-0 last:pb-0 flex items-center gap-3 border-b border-border-soft last:border-b-0">
            <Link
                to="/counterparties/$id"
                search={{ page: 1 }}
                params={{ id: counterparty.id }}
                className="flex-1 min-w-0 text-14 font-medium text-fg-1 hover:text-brand-primary truncate"
            >
                {counterparty.name}
            </Link>
            <button
                type="button"
                onClick={() => {
                    onEdit(counterparty);
                }}
                aria-label="Edit"
                className="p-2 rounded-sm text-fg-3 hover:text-fg-1 hover:bg-surface-2"
            >
                <Icon name="pencil" size={14} strokeWidth={2} />
            </button>
            <button
                type="button"
                onClick={() => {
                    onDelete(counterparty);
                }}
                aria-label="Delete"
                className="p-2 rounded-sm text-fg-3 hover:text-danger hover:bg-surface-2"
            >
                <Icon name="trash" size={14} strokeWidth={2} />
            </button>
        </div>
    );
}

function DeleteCounterpartyDialog({
    counterparty,
    onClose,
}: {
    counterparty: Counterparty;
    onClose: () => void;
}) {
    const del = useDeleteCounterparty();
    const toast = useToast();
    const [error, setError] = useState<string | null>(null);

    async function onConfirm() {
        setError(null);
        try {
            await del.mutateAsync(counterparty.id);
            toast.success(`Deleted “${counterparty.name}”.`);
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
            title={`Delete “${counterparty.name}”?`}
            message="This can't be undone."
            confirmLabel="Delete"
            variant="destructive"
            busy={del.isPending}
            error={error}
        />
    );
}
