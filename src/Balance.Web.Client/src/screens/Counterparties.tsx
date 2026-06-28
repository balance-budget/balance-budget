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
import { Cell, Column, Row, Table, TableBody, TableHeader } from '../components/ui/Table';
import { RowActions } from '../components/ui/RowActions';
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
            <CounterpartyTable items={query.data.items} onEdit={onEdit} onDelete={onDelete} />
            <Pagination
                page={page}
                pageSize={PAGE_SIZE}
                totalCount={query.data.totalCount}
                onPageChange={onPageChange}
            />
        </div>
    );
}

function CounterpartyTable({
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
        <div className="overflow-x-auto">
            <Table
                aria-label={t`Counterparties`}
                onRowAction={key => {
                    void navigate({
                        to: '/counterparties/$id',
                        search: { page: 1 },
                        params: { id: String(key) },
                    });
                }}
            >
                <TableHeader>
                    <Column isRowHeader>
                        <Trans>Name</Trans>
                    </Column>
                    <Column width={96} className="text-right">
                        <span className="sr-only">
                            <Trans>Actions</Trans>
                        </span>
                    </Column>
                </TableHeader>
                <TableBody items={items}>
                    {c => <CounterpartyRow counterparty={c} onEdit={onEdit} onDelete={onDelete} />}
                </TableBody>
            </Table>
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
    const { t } = useLingui();
    return (
        <Row id={counterparty.id} className="cursor-pointer">
            <Cell className="text-sm font-medium text-fg-1 truncate">{counterparty.name}</Cell>
            <Cell className="text-right">
                <RowActions
                    actions={[
                        {
                            icon: 'pencil',
                            label: t`Edit`,
                            onPress: () => {
                                onEdit(counterparty);
                            },
                        },
                        {
                            icon: 'trash',
                            label: t`Delete`,
                            danger: true,
                            onPress: () => {
                                onDelete(counterparty);
                            },
                        },
                    ]}
                />
            </Cell>
        </Row>
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
