import { useMemo, useState } from 'react';
import { Link, useNavigate } from '@tanstack/react-router';
import { useAccounts, type Account } from '../api/accounts';
import { useCounterparty, useDeleteCounterparty } from '../api/counterparties';
import { useJournalEntries, type JournalEntry } from '../api/journalEntries';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { ErrorState } from '../components/ErrorState';
import { Icon } from '../components/Icon';
import { Pagination } from '../components/Pagination';
import { Panel, SectionHead } from '../components/Panel';
import { ProjectionAmount } from '../components/ProjectionAmount';
import { Skeleton } from '../components/Skeleton';
import { useToast } from '../components/ui/Toast';
import { type AccountId, type CounterpartyId } from '../lib/domain';
import { handleActionError } from '../lib/formErrors';
import { formatLegLabel, projectEntry, type JournalProjection } from '../lib/journalProjection';
import { LinkedBankAccountsSection } from './LinkedBankAccounts';
import { CounterpartyFormModal } from './CounterpartyForm';

const PAGE_SIZE = 50;

type Props = {
    id: CounterpartyId;
    page: number;
    onPageChange: (page: number) => void;
};

export function CounterpartyDetail({ id, page, onPageChange }: Props) {
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
                <div className="flex flex-col gap-3 lg:flex-row lg:items-start lg:justify-between mb-4">
                    <div className="flex flex-col gap-[2px] min-w-0">
                        <Link
                            to="/counterparties"
                            search={{ page: 1, q: '' }}
                            className="text-xs text-fg-3 hover:text-fg-1 inline-flex items-center gap-1"
                        >
                            ← Counterparties
                        </Link>
                        <h1 className="text-xl font-medium text-fg-1 truncate">{cp.name}</h1>
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
                            Edit
                        </button>
                        <button
                            type="button"
                            onClick={() => {
                                setDeleting(true);
                            }}
                            className="inline-flex items-center gap-2 px-3 py-[7px] rounded-lg text-sm font-medium text-fg-2 hover:text-danger hover:bg-surface-2"
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

            <Panel>
                <SectionHead
                    title="Journal entries"
                    subtitle={`Every entry referencing ${cp.name}.`}
                />
                <JournalEntriesSection
                    counterpartyId={cp.id}
                    page={page}
                    onPageChange={onPageChange}
                />
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

function JournalEntriesSection({
    counterpartyId,
    page,
    onPageChange,
}: {
    counterpartyId: CounterpartyId;
    page: number;
    onPageChange: (page: number) => void;
}) {
    const skip = (page - 1) * PAGE_SIZE;
    const entries = useJournalEntries(skip, PAGE_SIZE, '', { counterpartyId });
    const accounts = useAccounts();

    const accountById = useMemo(
        () => new Map<AccountId, Account>((accounts.data ?? []).map(a => [a.id, a])),
        [accounts.data],
    );

    if (entries.isPending) {
        return (
            <div className="flex flex-col gap-2">
                <Skeleton className="h-8 w-full" />
                <Skeleton className="h-8 w-full" />
                <Skeleton className="h-8 w-full" />
            </div>
        );
    }

    if (entries.isError) {
        return (
            <ErrorState
                message="Couldn't load journal entries."
                onRetry={() => void entries.refetch()}
            />
        );
    }

    if (entries.data.items.length === 0 && page === 1) {
        return (
            <div className="py-6 text-center text-sm text-fg-3">
                No journal entries yet for this counterparty.
            </div>
        );
    }

    return (
        <div className="flex flex-col">
            <div className="hidden lg:grid grid-cols-[100px_1fr_minmax(180px,1.2fr)_140px] gap-3 px-2 pb-2 text-xs text-fg-3 uppercase tracking-wider border-b border-border-soft">
                <span>Date</span>
                <span>Description</span>
                <span>From → To</span>
                <span className="text-right">Amount</span>
            </div>
            {entries.data.items.map(entry => (
                <CounterpartyEntryRow key={entry.id} entry={entry} accountById={accountById} />
            ))}
            <Pagination
                page={page}
                pageSize={PAGE_SIZE}
                totalCount={entries.data.totalCount}
                onPageChange={onPageChange}
            />
        </div>
    );
}

function CounterpartyEntryRow({
    entry,
    accountById,
}: {
    entry: JournalEntry;
    accountById: ReadonlyMap<AccountId, Account>;
}) {
    const projection = projectEntry(entry, accountById);
    const description = entry.description ?? '—';
    return (
        <Link
            to="/journal/$id"
            params={{ id: entry.id }}
            className="block border-b border-border-soft last:border-b-0 hover:bg-surface-2"
        >
            <div className="hidden lg:grid grid-cols-[100px_1fr_minmax(180px,1.2fr)_140px] gap-3 items-center px-2 py-2">
                <span className="text-xs text-fg-3 tabular">{entry.date}</span>
                <span className="text-sm text-fg-1 truncate">{description}</span>
                <FromToCell projection={projection} lineCount={entry.lines.length} />
                <ProjectionAmount projection={projection} variant="row" />
            </div>
            <div className="lg:hidden flex flex-col gap-1 px-2 py-3">
                <div className="flex items-center justify-between gap-3">
                    <span className="text-xs text-fg-3 tabular shrink-0">{entry.date}</span>
                    <ProjectionAmount projection={projection} variant="row" />
                </div>
                <span className="text-sm text-fg-1 truncate">{description}</span>
                <FromToCell projection={projection} lineCount={entry.lines.length} />
            </div>
        </Link>
    );
}

function FromToCell({
    projection,
    lineCount,
}: {
    projection: JournalProjection;
    lineCount: number;
}) {
    if (!projection.isSimplifiable) {
        return <span className="text-xs text-fg-3 truncate">Split ({lineCount} lines)</span>;
    }
    const fromLabel = formatLegLabel(projection.fromLegs);
    const toLabel = formatLegLabel(projection.toLegs);
    return (
        <span className="text-xs text-fg-2 truncate flex items-center gap-1">
            <span className="truncate">{fromLabel}</span>
            <Icon name="chevron-right" size={10} strokeWidth={2} className="text-fg-3 shrink-0" />
            <span className="truncate">{toLabel}</span>
        </span>
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
            await navigate({ to: '/counterparties', search: { page: 1, q: '' } });
        } catch (err) {
            handleActionError(err, { setError, toast: toast.error });
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
