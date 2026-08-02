import { useMemo } from 'react';
import { Trans, useLingui } from '@lingui/react/macro';
import type { ToOptions } from '@tanstack/react-router';
import { Link } from '@tanstack/react-router';
import { useAccounts, type Account } from '../api/accounts';
import { useJournalEntries, type JournalEntry } from '../api/journalEntries';
import { AccountSelect } from '../components/AccountSelect';
import { DateRangePicker } from '../components/ui/DateRangePicker';
import { SearchField } from '../components/ui/SearchField';
import { RowLink } from '../components/ui/RowLink';
import { Cell, Column, Row, Table, TableBody, TableHeader } from '../components/ui/Table';
import { ErrorState } from '../components/ErrorState';
import { AccountLegs } from '../components/AccountLegs';
import { Icon } from '../components/Icon';
import { Pagination } from '../components/Pagination';
import { Panel, SectionHead } from '../components/Panel';
import { ProjectionAmount } from '../components/ProjectionAmount';
import { Skeleton } from '../components/Skeleton';
import { formatTableDate } from '../lib/dates';
import { type AccountId } from '../lib/domain';
import { projectEntry } from '../lib/journalProjection';
import { useDebouncedValue } from '../lib/useDebouncedValue';

const PAGE_SIZE = 50;

/** The Activity list's URL-backed filters, minus the separately-debounced `q`. */
export type ActivityFilterState = {
    account: AccountId | null;
    from: string;
    to: string;
};

export function Activity({
    page,
    q,
    filters,
    hrefForPage,
    onSearchChange,
    onFiltersChange,
}: {
    page: number;
    q: string;
    filters: ActivityFilterState;
    hrefForPage: (page: number) => ToOptions;
    onSearchChange: (q: string) => void;
    onFiltersChange: (patch: Partial<ActivityFilterState>) => void;
}) {
    const { t } = useLingui();
    const skip = (page - 1) * PAGE_SIZE;
    const debouncedQ = useDebouncedValue(q, 200);
    const entries = useJournalEntries(skip, PAGE_SIZE, debouncedQ, {
        accountId: filters.account,
        from: filters.from,
        to: filters.to,
    });
    const accounts = useAccounts();

    return (
        <Panel>
            <SectionHead
                subtitle={<Trans>Every bookkeeping event, newest first.</Trans>}
                action={
                    <Link
                        to="/journal/new"
                        className="inline-flex items-center gap-2 px-3 py-[7px] rounded-lg bg-brand-primary text-white text-sm font-medium hover:bg-brand-primary-dark"
                    >
                        <Icon name="plus" size={14} strokeWidth={2} />
                        <Trans>New journal entry</Trans>
                    </Link>
                }
            />
            <div className="mb-4 flex flex-col gap-3">
                <SearchField
                    aria-label={t`Search activity`}
                    value={q}
                    onChange={onSearchChange}
                    placeholder={t`Search description or counterparty…`}
                />
                <ActivityFilterBar filters={filters} onFiltersChange={onFiltersChange} />
            </div>
            <JournalBody
                entries={entries}
                accounts={accounts.data ?? []}
                page={page}
                query={debouncedQ}
                filtered={filters.account !== null || filters.from !== '' || filters.to !== ''}
                hrefForPage={hrefForPage}
            />
        </Panel>
    );
}

function ActivityFilterBar({
    filters,
    onFiltersChange,
}: {
    filters: ActivityFilterState;
    onFiltersChange: (patch: Partial<ActivityFilterState>) => void;
}) {
    const { t } = useLingui();
    return (
        <div className="flex flex-wrap items-center gap-2">
            <div className="w-64">
                {/* One symmetric account filter — an Activity row has no focal account, so this
                 *  matches entries touching the account or any of its descendants (ADR-0019);
                 *  placeholders stay selectable to mean "this whole subtree". */}
                <AccountSelect
                    value={filters.account}
                    onChange={v => {
                        onFiltersChange({ account: v });
                    }}
                    onClear={() => {
                        onFiltersChange({ account: null });
                    }}
                    noneLabel={t`Any account`}
                    placeholder={t`Account…`}
                    ariaLabel={t`Filter by account`}
                />
            </div>
            <DateRangePicker
                aria-label={t`Date range`}
                value={{ from: filters.from, to: filters.to }}
                onChange={range => {
                    onFiltersChange({ from: range.from, to: range.to });
                }}
                fieldClassName="text-xs py-[5px]"
            />
        </div>
    );
}

function JournalBody({
    entries,
    accounts,
    page,
    query,
    filtered,
    hrefForPage,
}: {
    entries: ReturnType<typeof useJournalEntries>;
    accounts: Account[];
    page: number;
    query: string;
    filtered: boolean;
    hrefForPage: (page: number) => ToOptions;
}) {
    const { t } = useLingui();
    const accountById = useMemo(
        () => new Map<AccountId, Account>(accounts.map(a => [a.id, a])),
        [accounts],
    );

    if (entries.isPending) {
        return (
            <div className="flex flex-col gap-2">
                <Skeleton className="h-10 w-full" />
                <Skeleton className="h-10 w-full" />
                <Skeleton className="h-10 w-full" />
                <Skeleton className="h-10 w-full" />
                <Skeleton className="h-10 w-full" />
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

    if (entries.data.items.length === 0 && query !== '') {
        return (
            <div className="py-8 text-center text-sm text-fg-2">
                <Trans>No matches for “{query}”.</Trans>
            </div>
        );
    }

    if (entries.data.items.length === 0 && filtered) {
        return (
            <div className="py-8 text-center text-sm text-fg-2">
                <Trans>No journal entries match the current filters.</Trans>
            </div>
        );
    }

    if (entries.data.items.length === 0 && page === 1) {
        return (
            <div className="py-8 flex flex-col items-center gap-2 text-center">
                <span className="text-sm text-fg-2">
                    <Trans>No journal entries yet.</Trans>
                </span>
                <span className="text-xs text-fg-3">
                    <Trans>Create one manually or import a bank statement.</Trans>
                </span>
            </div>
        );
    }

    return (
        <div className="flex flex-col">
            <div className="overflow-x-auto">
                <JournalTable entries={entries.data.items} accountById={accountById} />
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

function JournalTable({
    entries,
    accountById,
}: {
    entries: JournalEntry[];
    accountById: ReadonlyMap<AccountId, Account>;
}) {
    const { t } = useLingui();
    return (
        <Table aria-label={t`Activity`}>
            <TableHeader>
                <Column width={100}>
                    <Trans>Date</Trans>
                </Column>
                <Column width={24}>
                    <span className="sr-only">
                        <Trans>Source</Trans>
                    </span>
                </Column>
                <Column isRowHeader>
                    <Trans>Counterparty</Trans>
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
            <TableBody items={entries}>
                {entry => <JournalRow entry={entry} accountById={accountById} />}
            </TableBody>
        </Table>
    );
}

function JournalRow({
    entry,
    accountById,
}: {
    entry: JournalEntry;
    accountById: ReadonlyMap<AccountId, Account>;
}) {
    const projection = projectEntry(entry, accountById);
    const heading = entry.counterpartyName ?? entry.description ?? '—';
    const href = { to: '/journal/$id', params: { id: String(entry.id) } } as const;
    return (
        <Row id={entry.id} href={href} className="cursor-pointer">
            <Cell className="text-xs text-fg-3 tabular-nums">{formatTableDate(entry.date)}</Cell>
            <Cell className="text-fg-3">
                {entry.hasBankTransactions ? (
                    <Icon name="download" size={12} strokeWidth={2} aria-hidden="true" />
                ) : null}
            </Cell>
            <Cell>
                <RowLink href={href} className="block min-w-0">
                    <span className="text-sm text-fg-1 truncate block">{heading}</span>
                </RowLink>
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
