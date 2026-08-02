import { useMemo, useState } from 'react';
import { Trans, useLingui } from '@lingui/react/macro';
import type { ToOptions } from '@tanstack/react-router';
import { useNavigate } from '@tanstack/react-router';
import { useAccounts, type Account } from '../api/accounts';
import { useCounterparty, useDeleteCounterparty } from '../api/counterparties';
import { useJournalEntries, type JournalEntry } from '../api/journalEntries';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { ErrorState } from '../components/ErrorState';
import { AccountLegs } from '../components/AccountLegs';
import { Icon } from '../components/Icon';
import { Pagination } from '../components/Pagination';
import { Panel, SectionHead } from '../components/Panel';
import { usePageHeader } from '../components/PageHeader';
import { ProjectionAmount } from '../components/ProjectionAmount';
import { Skeleton } from '../components/Skeleton';
import { RowLink } from '../components/ui/RowLink';
import { Cell, Column, Row, Table, TableBody, TableHeader } from '../components/ui/Table';
import { useToast } from '../components/ui/Toast';
import { formatTableDate } from '../lib/dates';
import { type AccountId, type CounterpartyId } from '../lib/domain';
import { handleActionError } from '../lib/formErrors';
import { projectEntry } from '../lib/journalProjection';
import { LinkedBankAccountsSection } from './LinkedBankAccounts';
import { CounterpartyFormModal } from './CounterpartyForm';

const PAGE_SIZE = 50;

type Props = {
    id: CounterpartyId;
    page: number;
    hrefForPage: (page: number) => ToOptions;
};

export function CounterpartyDetail({ id, page, hrefForPage }: Props) {
    const { t } = useLingui();
    const query = useCounterparty(id);
    const [editing, setEditing] = useState(false);
    const [deleting, setDeleting] = useState(false);

    // TopBar owns the title (the counterparty name) under a "Counterparties"
    // breadcrumb; the entity actions ride on the first content section below.
    usePageHeader({
        title: query.data?.name,
        breadcrumb: [{ label: t`Counterparties`, href: { to: '/counterparties' } }],
    });

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
                    message={t`Couldn't load counterparty.`}
                    onRetry={() => void query.refetch()}
                />
            </Panel>
        );
    }

    const cp = query.data;

    return (
        <>
            <Panel>
                <SectionHead
                    title={<Trans>Linked bank accounts</Trans>}
                    action={
                        <div className="flex items-center gap-2 shrink-0">
                            <button
                                type="button"
                                onClick={() => {
                                    setEditing(true);
                                }}
                                className="inline-flex items-center gap-2 px-3 py-[7px] rounded-lg text-sm font-medium text-fg-2 hover:text-fg-1 hover:bg-surface-2"
                            >
                                <Icon name="pencil" size={14} strokeWidth={2} />
                                <Trans>Edit</Trans>
                            </button>
                            <button
                                type="button"
                                onClick={() => {
                                    setDeleting(true);
                                }}
                                className="inline-flex items-center gap-2 px-3 py-[7px] rounded-lg text-sm font-medium text-fg-2 hover:text-danger hover:bg-surface-2"
                            >
                                <Icon name="trash" size={14} strokeWidth={2} />
                                <Trans>Delete</Trans>
                            </button>
                        </div>
                    }
                />
                <LinkedBankAccountsSection owner={{ kind: 'counterparty', id: cp.id }} />
            </Panel>

            <Panel>
                <SectionHead
                    title={<Trans>Journal entries</Trans>}
                    subtitle={<Trans>Every journal entry referencing {cp.name}.</Trans>}
                />
                <JournalEntriesSection
                    counterpartyId={cp.id}
                    page={page}
                    hrefForPage={hrefForPage}
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
    hrefForPage,
}: {
    counterpartyId: CounterpartyId;
    page: number;
    hrefForPage: (page: number) => ToOptions;
}) {
    const { t } = useLingui();
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
                message={t`Couldn't load journal entries.`}
                onRetry={() => void entries.refetch()}
            />
        );
    }

    if (entries.data.items.length === 0 && page === 1) {
        return (
            <div className="py-6 text-center text-sm text-fg-3">
                <Trans>No journal entries yet for this counterparty.</Trans>
            </div>
        );
    }

    return (
        <div className="flex flex-col">
            <div className="overflow-x-auto">
                <Table aria-label={t`Journal entries`}>
                    <TableHeader>
                        <Column width={100}>
                            <Trans>Date</Trans>
                        </Column>
                        <Column isRowHeader>
                            <Trans>Description</Trans>
                        </Column>
                        <Column width={200}>
                            <Trans>From</Trans>
                        </Column>
                        <Column width={200}>
                            <Trans>To</Trans>
                        </Column>
                        <Column width={140} className="text-right">
                            <Trans>Amount</Trans>
                        </Column>
                    </TableHeader>
                    <TableBody items={entries.data.items}>
                        {entry => <CounterpartyEntryRow entry={entry} accountById={accountById} />}
                    </TableBody>
                </Table>
            </div>
            <Pagination
                page={page}
                pageSize={PAGE_SIZE}
                totalCount={entries.data.totalCount}
                hrefForPage={hrefForPage}
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
    const href = { to: '/journal/$id', params: { id: String(entry.id) } } as const;
    return (
        <Row id={entry.id} href={href} className="cursor-pointer">
            <Cell className="text-xs text-fg-3 tabular-nums">{formatTableDate(entry.date)}</Cell>
            <Cell className="text-sm text-fg-1 truncate">
                <RowLink href={href}>{description}</RowLink>
            </Cell>
            <Cell className="text-xs text-fg-2">
                <AccountLegs legs={projection.fromLegs} />
            </Cell>
            <Cell className="text-xs text-fg-2">
                <AccountLegs legs={projection.toLegs} />
            </Cell>
            <Cell className="text-right">
                <ProjectionAmount projection={projection} variant="row" />
            </Cell>
        </Row>
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
    const { t } = useLingui();
    const del = useDeleteCounterparty();
    const toast = useToast();
    const navigate = useNavigate();
    const [error, setError] = useState<string | null>(null);

    async function onConfirm() {
        setError(null);
        try {
            await del.mutateAsync(id);
            toast.success(t`Deleted “${name}”.`);
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
            title={t`Delete “${name}”?`}
            message={t`This can't be undone.`}
            confirmLabel={t`Delete`}
            variant="destructive"
            busy={del.isPending}
            error={error}
        />
    );
}
