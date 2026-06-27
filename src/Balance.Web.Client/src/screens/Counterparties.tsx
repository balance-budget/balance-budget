import { useState } from 'react';
import { Trans, useLingui } from '@lingui/react/macro';
import { useNavigate } from '@tanstack/react-router';
import {
    useCounterpartiesPage,
    useDeleteCounterparty,
    type Counterparty,
} from '../api/counterparties';
import { ErrorState } from '../components/ErrorState';
import { Icon } from '../components/Icon';
import { Pagination } from '../components/Pagination';
import { Panel, SectionHead } from '../components/Panel';
import { Skeleton } from '../components/Skeleton';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { useToast } from '../components/ui/Toast';
import { GridList, GridListItem } from '../components/ui/GridList';
import { SearchField } from '../components/ui/SearchField';
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
    const { t } = useLingui();
    const [creating, setCreating] = useState(false);
    const [editing, setEditing] = useState<Counterparty | null>(null);
    const [deleting, setDeleting] = useState<Counterparty | null>(null);

    return (
        <>
            <Panel>
                <SectionHead
                    subtitle={
                        <Trans>Real-world parties on the other side of journal entries.</Trans>
                    }
                    action={
                        <button
                            type="button"
                            onClick={() => {
                                setCreating(true);
                            }}
                            className="inline-flex items-center gap-2 px-3 py-[7px] rounded-lg bg-brand-primary text-white text-sm font-medium hover:bg-brand-primary-dark"
                        >
                            <Icon name="plus" size={14} strokeWidth={2} />
                            <Trans>New counterparty</Trans>
                        </button>
                    }
                />
                <div className="mb-4">
                    <SearchField
                        aria-label={t`Search counterparties`}
                        value={q}
                        onChange={onSearchChange}
                        placeholder={t`Search counterparties…`}
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
    const { t } = useLingui();
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
                message={t`Couldn't load counterparties.`}
                onRetry={() => void query.refetch()}
            />
        );
    }

    if (query.data.items.length === 0 && debouncedQ !== '') {
        return (
            <div className="py-8 text-center text-sm text-fg-2">
                <Trans>No matches for “{debouncedQ}”.</Trans>
            </div>
        );
    }

    if (query.data.items.length === 0 && page === 1) {
        return (
            <div className="py-8 flex flex-col items-center gap-2 text-center">
                <span className="text-sm text-fg-2">
                    <Trans>No counterparties yet.</Trans>
                </span>
                <span className="text-xs text-fg-3">
                    <Trans>Add the parties you receive money from or pay to.</Trans>
                </span>
            </div>
        );
    }

    return (
        <div>
            <CounterpartyGrid items={query.data.items} onEdit={onEdit} onDelete={onDelete} />
            <Pagination
                page={page}
                pageSize={PAGE_SIZE}
                totalCount={query.data.totalCount}
                onPageChange={onPageChange}
            />
        </div>
    );
}

function CounterpartyGrid({
    items,
    onEdit,
    onDelete,
}: {
    items: Counterparty[];
    onEdit: (c: Counterparty) => void;
    onDelete: (c: Counterparty) => void;
}) {
    const { t } = useLingui();
    const navigate = useNavigate();
    return (
        <GridList
            aria-label={t`Counterparties`}
            items={items}
            onAction={key => {
                void navigate({
                    to: '/counterparties/$id',
                    search: { page: 1 },
                    params: { id: String(key) },
                });
            }}
        >
            {c => <CounterpartyRow counterparty={c} onEdit={onEdit} onDelete={onDelete} />}
        </GridList>
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
    const { t } = useLingui();
    return (
        <GridListItem
            id={counterparty.id}
            textValue={counterparty.name}
            className="flex items-center gap-3 px-3 py-[10px] cursor-pointer"
        >
            <span className="flex-1 min-w-0 text-sm font-medium text-fg-1 truncate">
                {counterparty.name}
            </span>
            <button
                type="button"
                onClick={() => {
                    onEdit(counterparty);
                }}
                aria-label={t`Edit`}
                className="p-2 rounded-lg text-fg-3 hover:text-fg-1 hover:bg-surface-3"
            >
                <Icon name="pencil" size={14} strokeWidth={2} />
            </button>
            <button
                type="button"
                onClick={() => {
                    onDelete(counterparty);
                }}
                aria-label={t`Delete`}
                className="p-2 rounded-lg text-fg-3 hover:text-danger hover:bg-surface-3"
            >
                <Icon name="trash" size={14} strokeWidth={2} />
            </button>
        </GridListItem>
    );
}

function DeleteCounterpartyDialog({
    counterparty,
    onClose,
}: {
    counterparty: Counterparty;
    onClose: () => void;
}) {
    const { t } = useLingui();
    const del = useDeleteCounterparty();
    const toast = useToast();
    const [error, setError] = useState<string | null>(null);

    async function onConfirm() {
        setError(null);
        try {
            await del.mutateAsync(counterparty.id);
            toast.success(t`Deleted “${counterparty.name}”.`);
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
            title={t`Delete “${counterparty.name}”?`}
            message={t`This can't be undone.`}
            confirmLabel={t`Delete`}
            variant="destructive"
            busy={del.isPending}
            error={error}
        />
    );
}
