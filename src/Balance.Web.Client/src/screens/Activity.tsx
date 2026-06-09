import { useMemo } from 'react';
import { Plural, Trans, useLingui } from '@lingui/react/macro';
import { Link } from '@tanstack/react-router';
import { useAccounts, type Account } from '../api/accounts';
import { useJournalEntries, type JournalEntry } from '../api/journalEntries';
import { AccountSelect } from '../components/AccountSelect';
import { DateRangePicker } from '../components/ui/DateRangePicker';
import { SearchField } from '../components/ui/SearchField';
import { ErrorState } from '../components/ErrorState';
import { Icon } from '../components/Icon';
import { Pagination } from '../components/Pagination';
import { Panel, SectionHead } from '../components/Panel';
import { ProjectionAmount } from '../components/ProjectionAmount';
import { Skeleton } from '../components/Skeleton';
import { type AccountId } from '../lib/domain';
import { formatLegLabel, projectEntry, type JournalProjection } from '../lib/journalProjection';
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
    onPageChange,
    onSearchChange,
    onFiltersChange,
}: {
    page: number;
    q: string;
    filters: ActivityFilterState;
    onPageChange: (p: number) => void;
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
                title={<Trans>Activity</Trans>}
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
                onPageChange={onPageChange}
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
    onPageChange,
}: {
    entries: ReturnType<typeof useJournalEntries>;
    accounts: Account[];
    page: number;
    query: string;
    filtered: boolean;
    onPageChange: (p: number) => void;
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
            <div className="hidden lg:grid grid-cols-[100px_24px_1fr_minmax(180px,1.2fr)_140px] gap-3 px-2 pb-2 text-xs text-fg-3 uppercase tracking-wider border-b border-border-soft">
                <span>
                    <Trans>Date</Trans>
                </span>
                <span />
                <span>
                    <Trans>Counterparty</Trans>
                </span>
                <span>
                    <Trans>From</Trans> → <Trans>To</Trans>
                </span>
                <span className="text-right">
                    <Trans>Amount</Trans>
                </span>
            </div>
            {entries.data.items.map(entry => (
                <JournalRow key={entry.id} entry={entry} accountById={accountById} />
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

function JournalRow({
    entry,
    accountById,
}: {
    entry: JournalEntry;
    accountById: ReadonlyMap<AccountId, Account>;
}) {
    const projection = projectEntry(entry, accountById);
    const heading = entry.counterpartyName ?? entry.description ?? '—';
    return (
        <Link
            to="/journal/$id"
            params={{ id: entry.id }}
            className="block border-b border-border-soft last:border-b-0 hover:bg-surface-2"
        >
            <div className="hidden lg:grid grid-cols-[100px_24px_1fr_minmax(180px,1.2fr)_140px] gap-3 items-center px-2 py-2">
                <span className="text-xs text-fg-3 tabular-nums">{entry.date}</span>
                <span className="flex items-center justify-center text-fg-3" aria-hidden="true">
                    {entry.hasBankTransactions ? (
                        <Icon name="download" size={12} strokeWidth={2} />
                    ) : null}
                </span>
                <span className="text-sm text-fg-1 truncate">{heading}</span>
                <FromToCell projection={projection} lineCount={entry.lines.length} />
                <ProjectionAmount projection={projection} variant="row" />
            </div>
            <div className="lg:hidden flex flex-col gap-1 px-2 py-3">
                <div className="flex items-center justify-between gap-3">
                    <div className="flex items-center gap-2 min-w-0">
                        <span className="text-xs text-fg-3 tabular-nums shrink-0">
                            {entry.date}
                        </span>
                        {entry.hasBankTransactions ? (
                            <Icon
                                name="download"
                                size={12}
                                strokeWidth={2}
                                className="text-fg-3 shrink-0"
                            />
                        ) : null}
                    </div>
                    <ProjectionAmount projection={projection} variant="row" />
                </div>
                <span className="text-sm text-fg-1 truncate">{heading}</span>
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
        return (
            <span className="text-xs text-fg-3 truncate">
                <Trans>
                    Split (<Plural value={lineCount} one="# line" other="# lines" />)
                </Trans>
            </span>
        );
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
