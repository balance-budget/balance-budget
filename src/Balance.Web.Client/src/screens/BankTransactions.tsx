import { useCurrencyCatalog, type CurrencyCatalog } from '../api/currencies';
import {
    useBankTransactions,
    type BankTransaction,
    type BankTransactionFilter,
} from '../api/bankTransactions';
import { ErrorState } from '../components/ErrorState';
import { Pagination } from '../components/Pagination';
import { Panel, SectionHead } from '../components/Panel';
import { Skeleton } from '../components/Skeleton';
import { cx } from '../lib/cx';
import { formatMoney } from '../lib/money';

const PAGE_SIZE = 50;

const FILTERS: { value: BankTransactionFilter; label: string }[] = [
    { value: 'Inbox', label: 'Inbox' },
    { value: 'Matched', label: 'Matched' },
    { value: 'Dismissed', label: 'Dismissed' },
    { value: 'All', label: 'All' },
];

type Props = {
    filter: BankTransactionFilter;
    page: number;
    onFilterChange: (filter: BankTransactionFilter) => void;
    onPageChange: (page: number) => void;
};

export function BankTransactions({ filter, page, onFilterChange, onPageChange }: Props) {
    const skip = (page - 1) * PAGE_SIZE;
    const query = useBankTransactions(filter, skip, PAGE_SIZE);
    const catalog = useCurrencyCatalog();

    return (
        <Panel>
            <SectionHead
                title="Bank transactions"
                subtitle="Rows imported from your bank. Inbox shows what's still waiting to be categorised."
            />
            <FilterChips
                value={filter}
                onChange={f => {
                    onFilterChange(f);
                    if (page !== 1) onPageChange(1);
                }}
            />
            <Body
                query={query}
                catalog={catalog}
                filter={filter}
                page={page}
                onPageChange={onPageChange}
            />
        </Panel>
    );
}

function FilterChips({
    value,
    onChange,
}: {
    value: BankTransactionFilter;
    onChange: (filter: BankTransactionFilter) => void;
}) {
    return (
        <div className="flex items-center gap-1 pb-3">
            {FILTERS.map(f => {
                const isActive = f.value === value;
                return (
                    <button
                        key={f.value}
                        type="button"
                        onClick={() => {
                            onChange(f.value);
                        }}
                        className={cx(
                            'px-3 py-[6px] rounded-sm text-[13px] font-medium select-none transition-colors',
                            isActive
                                ? 'bg-brand-primary-soft text-brand-primary'
                                : 'text-fg-2 hover:bg-surface-2 hover:text-fg-1',
                        )}
                    >
                        {f.label}
                    </button>
                );
            })}
        </div>
    );
}

function Body({
    query,
    catalog,
    filter,
    page,
    onPageChange,
}: {
    query: ReturnType<typeof useBankTransactions>;
    catalog: CurrencyCatalog;
    filter: BankTransactionFilter;
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
                message="Couldn't load bank transactions."
                onRetry={() => void query.refetch()}
            />
        );
    }

    if (query.data.length === 0 && page === 1) {
        return <EmptyState filter={filter} />;
    }

    return (
        <div className="flex flex-col">
            <div className="grid grid-cols-[100px_1fr_minmax(160px,1.4fr)_140px] gap-3 px-2 pb-2 text-[11px] text-fg-3 uppercase tracking-wider border-b border-border-soft">
                <span>Date</span>
                <span>Counterparty</span>
                <span>Description</span>
                <span className="text-right">Amount</span>
            </div>
            {query.data.map(row => (
                <Row key={row.id} row={row} catalog={catalog} />
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

function Row({ row, catalog }: { row: BankTransaction; catalog: CurrencyCatalog }) {
    return (
        <div className="grid grid-cols-[100px_1fr_minmax(160px,1.4fr)_140px] gap-3 items-center px-2 py-2 border-b border-border-soft last:border-b-0">
            <span className="text-[12px] text-fg-3 tabular">{row.bookingDate}</span>
            <span className="text-[13px] text-fg-1 truncate">{row.counterpartyName ?? '—'}</span>
            <span className="text-[12px] text-fg-2 truncate">{row.description}</span>
            <AmountCell row={row} catalog={catalog} />
        </div>
    );
}

function AmountCell({ row, catalog }: { row: BankTransaction; catalog: CurrencyCatalog }) {
    const colour = row.money.amount < 0 ? 'text-danger' : 'text-success';
    return (
        <span className={cx('font-mono text-[13px] tabular text-right', colour)}>
            {formatMoney(row.money.amount, row.money.currencyCode, catalog, { sign: true })}
        </span>
    );
}

function EmptyState({ filter }: { filter: BankTransactionFilter }) {
    const { title, hint } = emptyCopyFor(filter);
    return (
        <div className="py-8 flex flex-col items-center gap-2 text-center">
            <span className="text-[14px] text-fg-2">{title}</span>
            <span className="text-[12px] text-fg-3">{hint}</span>
        </div>
    );
}

function emptyCopyFor(filter: BankTransactionFilter): { title: string; hint: string } {
    if (filter === 'Inbox') {
        return {
            title: 'Inbox is clear.',
            hint: 'Every imported row has been categorised or dismissed.',
        };
    }
    if (filter === 'Matched') {
        return {
            title: 'No matched rows yet.',
            hint: 'Rows you categorise into a journal entry will appear here.',
        };
    }
    if (filter === 'Dismissed') {
        return {
            title: 'Nothing dismissed.',
            hint: 'Rows you mark as ‘no journal entry needed’ will appear here.',
        };
    }
    return {
        title: 'No bank transactions yet.',
        hint: 'Import a statement against one of your bank accounts to start populating this list.',
    };
}
