import { Link } from '@tanstack/react-router';
import { useCurrencyCatalog, type CurrencyCatalog } from '../api/currencies';
import { useJournalEntries, type JournalEntryRow } from '../api/journalEntries';
import { ErrorState } from '../components/ErrorState';
import { Icon } from '../components/Icon';
import { Pagination } from '../components/Pagination';
import { Panel, SectionHead } from '../components/Panel';
import { Skeleton } from '../components/Skeleton';
import { cx } from '../lib/cx';
import { formatMoney } from '../lib/money';

const PAGE_SIZE = 50;

export function Journal({ page, onPageChange }: { page: number; onPageChange: (p: number) => void }) {
    const skip = (page - 1) * PAGE_SIZE;
    const query = useJournalEntries(skip, PAGE_SIZE);
    const catalog = useCurrencyCatalog();

    return (
        <Panel>
            <SectionHead
                title="Journal entries"
                subtitle="Every bookkeeping event, newest first."
                action={
                    <button
                        type="button"
                        disabled
                        title="Manual entry creation isn't built yet."
                        className="inline-flex items-center gap-2 px-3 py-[7px] rounded-sm bg-brand-primary text-white text-[13px] font-medium opacity-40 cursor-not-allowed"
                    >
                        <Icon name="plus" size={14} strokeWidth={2} />
                        New entry
                    </button>
                }
            />
            <JournalBody
                query={query}
                catalog={catalog}
                page={page}
                onPageChange={onPageChange}
            />
        </Panel>
    );
}

function JournalBody({
    query,
    catalog,
    page,
    onPageChange,
}: {
    query: ReturnType<typeof useJournalEntries>;
    catalog: CurrencyCatalog;
    page: number;
    onPageChange: (p: number) => void;
}) {
    if (query.isPending) {
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

    if (query.isError) {
        return (
            <ErrorState
                message="Couldn't load journal entries."
                onRetry={() => void query.refetch()}
            />
        );
    }

    if (query.data.length === 0 && page === 1) {
        return (
            <div className="py-8 flex flex-col items-center gap-2 text-center">
                <span className="text-[14px] text-fg-2">No journal entries yet.</span>
                <span className="text-[12px] text-fg-3">
                    Manual entry creation isn't built yet — entries appear here once you import a
                    bank statement.
                </span>
            </div>
        );
    }

    return (
        <div className="flex flex-col">
            <div className="grid grid-cols-[100px_24px_1fr_minmax(180px,1.2fr)_140px] gap-3 px-2 pb-2 text-[11px] text-fg-3 uppercase tracking-wider border-b border-border-soft">
                <span>Date</span>
                <span />
                <span>Counterparty</span>
                <span>From → To</span>
                <span className="text-right">Amount</span>
            </div>
            {query.data.map(row => (
                <JournalRow key={row.id} row={row} catalog={catalog} />
            ))}
            <Pagination
                page={page}
                pageSize={PAGE_SIZE}
                count={query.data.length}
                onPageChange={onPageChange}
            />
        </div>
    );
}

function JournalRow({ row, catalog }: { row: JournalEntryRow; catalog: CurrencyCatalog }) {
    return (
        <Link
            to="/journal/$id"
            params={{ id: row.id }}
            className="grid grid-cols-[100px_24px_1fr_minmax(180px,1.2fr)_140px] gap-3 items-center px-2 py-2 border-b border-border-soft last:border-b-0 hover:bg-surface-2"
        >
            <span className="text-[12px] text-fg-3 tabular">{row.date}</span>
            <span className="flex items-center justify-center text-fg-3" aria-hidden="true">
                {row.bankTransactionId ? (
                    <Icon name="download" size={12} strokeWidth={2} />
                ) : null}
            </span>
            <span className="text-[13px] text-fg-1 truncate">
                {row.counterpartyName ?? row.description ?? '—'}
            </span>
            <FromToCell row={row} />
            <AmountCell row={row} catalog={catalog} />
        </Link>
    );
}

function FromToCell({ row }: { row: JournalEntryRow }) {
    if (!row.isSimplifiable) {
        return (
            <span className="text-[12px] text-fg-3 truncate">Split ({row.lineCount} lines)</span>
        );
    }

    const fromLabel = legLabel(row.fromLegs);
    const toLabel = legLabel(row.toLegs);

    return (
        <span className="text-[12px] text-fg-2 truncate flex items-center gap-1">
            <span className="truncate">{fromLabel}</span>
            <Icon
                name="chevron-right"
                size={10}
                strokeWidth={2}
                className="text-fg-3 shrink-0"
            />
            <span className="truncate">{toLabel}</span>
        </span>
    );
}

function legLabel(legs: JournalEntryRow['fromLegs']): string {
    const first = legs[0];
    if (!first) return '—';
    if (legs.length === 1) return first.accountName;
    return `${first.accountName} +${legs.length - 1}`;
}

function AmountCell({ row, catalog }: { row: JournalEntryRow; catalog: CurrencyCatalog }) {
    // ADR-0012: transfers (NetWorthChange == 0) render unsigned magnitude in
    // muted text; operating entries render the signed net-worth change with
    // colour by sign. Font/size matches the per-account Register row for
    // visual consistency across the two amount-on-row surfaces.
    const money = row.isTransfer ? row.grossMagnitude : row.netWorthChange;
    const colour = row.isTransfer
        ? 'text-fg-3'
        : money.amount < 0
          ? 'text-danger'
          : 'text-success';
    return (
        <span className={cx('font-mono text-[13px] tabular text-right', colour)}>
            {formatMoney(money.amount, money.currencyCode, catalog, { sign: !row.isTransfer })}
        </span>
    );
}
