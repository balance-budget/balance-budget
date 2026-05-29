import { useMemo } from 'react';
import { Link } from '@tanstack/react-router';
import { useAccounts, type Account } from '../api/accounts';
import { useCurrencyCatalog, type CurrencyCatalog } from '../api/currencies';
import { useJournalEntries, type JournalEntry } from '../api/journalEntries';
import { ErrorState } from '../components/ErrorState';
import { Icon } from '../components/Icon';
import { Pagination } from '../components/Pagination';
import { Panel, SectionHead } from '../components/Panel';
import { Skeleton } from '../components/Skeleton';
import { cx } from '../lib/cx';
import { type AccountId } from '../lib/domain';
import { formatLegLabel, projectEntry, type JournalProjection } from '../lib/journalProjection';
import { formatMoney } from '../lib/money';

const PAGE_SIZE = 50;

export function Journal({
    page,
    onPageChange,
}: {
    page: number;
    onPageChange: (p: number) => void;
}) {
    const skip = (page - 1) * PAGE_SIZE;
    const entries = useJournalEntries(skip, PAGE_SIZE);
    const accounts = useAccounts();
    const catalog = useCurrencyCatalog();

    return (
        <Panel>
            <SectionHead
                title="Journal entries"
                subtitle="Every bookkeeping event, newest first."
                action={
                    <Link
                        to="/journal/new"
                        className="inline-flex items-center gap-2 px-3 py-[7px] rounded-sm bg-brand-primary text-white text-[13px] font-medium hover:bg-brand-primary-dark"
                    >
                        <Icon name="plus" size={14} strokeWidth={2} />
                        New entry
                    </Link>
                }
            />
            <JournalBody
                entries={entries}
                accounts={accounts.data ?? []}
                catalog={catalog}
                page={page}
                onPageChange={onPageChange}
            />
        </Panel>
    );
}

function JournalBody({
    entries,
    accounts,
    catalog,
    page,
    onPageChange,
}: {
    entries: ReturnType<typeof useJournalEntries>;
    accounts: Account[];
    catalog: CurrencyCatalog;
    page: number;
    onPageChange: (p: number) => void;
}) {
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
                message="Couldn't load journal entries."
                onRetry={() => void entries.refetch()}
            />
        );
    }

    if (entries.data.length === 0 && page === 1) {
        return (
            <div className="py-8 flex flex-col items-center gap-2 text-center">
                <span className="text-[14px] text-fg-2">No journal entries yet.</span>
                <span className="text-[12px] text-fg-3">
                    Create one manually or import a bank statement.
                </span>
            </div>
        );
    }

    return (
        <div className="flex flex-col">
            <div className="hidden lg:grid grid-cols-[100px_24px_1fr_minmax(180px,1.2fr)_140px] gap-3 px-2 pb-2 text-[11px] text-fg-3 uppercase tracking-wider border-b border-border-soft">
                <span>Date</span>
                <span />
                <span>Counterparty</span>
                <span>From → To</span>
                <span className="text-right">Amount</span>
            </div>
            {entries.data.map(entry => (
                <JournalRow
                    key={entry.id}
                    entry={entry}
                    accountById={accountById}
                    catalog={catalog}
                />
            ))}
            <Pagination
                page={page}
                pageSize={PAGE_SIZE}
                count={entries.data.length}
                onPageChange={onPageChange}
            />
        </div>
    );
}

function JournalRow({
    entry,
    accountById,
    catalog,
}: {
    entry: JournalEntry;
    accountById: ReadonlyMap<AccountId, Account>;
    catalog: CurrencyCatalog;
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
                <span className="text-[12px] text-fg-3 tabular">{entry.date}</span>
                <span className="flex items-center justify-center text-fg-3" aria-hidden="true">
                    {entry.hasBankTransactions ? (
                        <Icon name="download" size={12} strokeWidth={2} />
                    ) : null}
                </span>
                <span className="text-[13px] text-fg-1 truncate">{heading}</span>
                <FromToCell projection={projection} lineCount={entry.lines.length} />
                <AmountCell projection={projection} catalog={catalog} />
            </div>
            <div className="lg:hidden flex flex-col gap-1 px-2 py-3">
                <div className="flex items-center justify-between gap-3">
                    <div className="flex items-center gap-2 min-w-0">
                        <span className="text-[12px] text-fg-3 tabular shrink-0">{entry.date}</span>
                        {entry.hasBankTransactions ? (
                            <Icon
                                name="download"
                                size={12}
                                strokeWidth={2}
                                className="text-fg-3 shrink-0"
                            />
                        ) : null}
                    </div>
                    <AmountCell projection={projection} catalog={catalog} />
                </div>
                <span className="text-[13px] text-fg-1 truncate">{heading}</span>
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
        return <span className="text-[12px] text-fg-3 truncate">Split ({lineCount} lines)</span>;
    }

    const fromLabel = formatLegLabel(projection.fromLegs);
    const toLabel = formatLegLabel(projection.toLegs);

    return (
        <span className="text-[12px] text-fg-2 truncate flex items-center gap-1">
            <span className="truncate">{fromLabel}</span>
            <Icon name="chevron-right" size={10} strokeWidth={2} className="text-fg-3 shrink-0" />
            <span className="truncate">{toLabel}</span>
        </span>
    );
}

function AmountCell({
    projection,
    catalog,
}: {
    projection: JournalProjection;
    catalog: CurrencyCatalog;
}) {
    // ADR-0012: transfers (NetWorthChange == 0) render unsigned magnitude in
    // muted text; operating entries render the signed net-worth change with
    // colour by sign. Font/size matches the per-account Register row for
    // visual consistency across the two amount-on-row surfaces.
    const money = projection.isTransfer ? projection.grossMagnitude : projection.netWorthChange;
    const colour = projection.isTransfer
        ? 'text-fg-3'
        : money.amount < 0
          ? 'text-danger'
          : 'text-success';
    return (
        <span className={cx('font-mono text-[13px] tabular text-right', colour)}>
            {formatMoney(money.amount, money.currencyCode, catalog, {
                sign: !projection.isTransfer,
            })}
        </span>
    );
}
