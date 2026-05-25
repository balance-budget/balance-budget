import { useCurrencyCatalog, type CurrencyCatalog } from '../api/currencies';
import {
    BANK_TRANSACTION_FILTERS,
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

const FILTER_LABEL: Record<BankTransactionFilter, string> = {
    Inbox: 'Inbox',
    Matched: 'Matched',
    Dismissed: 'Dismissed',
    All: 'All',
};

const SUBTITLE: Record<BankTransactionFilter, string> = {
    Inbox: 'Bank rows waiting for a journal entry. Oldest first — work the queue.',
    Matched: 'Bank rows that have been categorised into a journal entry.',
    Dismissed: 'Bank rows you marked as not needing a journal entry.',
    All: 'Every imported bank row, regardless of state.',
};

const EMPTY_TITLE: Record<BankTransactionFilter, string> = {
    Inbox: "You're caught up.",
    Matched: 'Nothing categorised yet.',
    Dismissed: 'Nothing dismissed.',
    All: 'No bank transactions yet.',
};

const EMPTY_HINT: Record<BankTransactionFilter, string> = {
    Inbox: 'Imported rows that need categorising will appear here.',
    Matched: 'Categorise an inbox row to see it here.',
    Dismissed: 'Dismissed rows live here for audit.',
    All: 'Import a bank statement from Bank imports to get started.',
};

type Props = {
    page: number;
    filter: BankTransactionFilter;
    onPageChange: (page: number) => void;
    onFilterChange: (filter: BankTransactionFilter) => void;
};

export function BankTransactionsInbox({ page, filter, onPageChange, onFilterChange }: Props) {
    const skip = (page - 1) * PAGE_SIZE;
    const query = useBankTransactions(skip, PAGE_SIZE, filter);
    const catalog = useCurrencyCatalog();

    return (
        <Panel>
            <SectionHead title="Bank transactions" subtitle={SUBTITLE[filter]} />
            <FilterChips value={filter} onChange={onFilterChange} />
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
        <div className="flex items-center gap-2 mb-4" role="tablist" aria-label="Filter">
            {BANK_TRANSACTION_FILTERS.map(filter => {
                const active = filter === value;
                return (
                    <button
                        key={filter}
                        type="button"
                        role="tab"
                        aria-selected={active}
                        onClick={() => {
                            onChange(filter);
                        }}
                        className={cx(
                            'px-3 py-1 rounded-sm text-[12px] font-medium select-none transition-colors',
                            active
                                ? 'bg-brand-primary-soft text-brand-primary'
                                : 'text-fg-2 hover:bg-surface-2 hover:text-fg-1',
                        )}
                    >
                        {FILTER_LABEL[filter]}
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
    onPageChange: (page: number) => void;
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
        return (
            <div className="py-8 flex flex-col items-center gap-2 text-center">
                <span className="text-[14px] text-fg-2">{EMPTY_TITLE[filter]}</span>
                <span className="text-[12px] text-fg-3">{EMPTY_HINT[filter]}</span>
            </div>
        );
    }

    return (
        <div className="flex flex-col">
            <div className="grid grid-cols-[100px_1fr_minmax(180px,1.2fr)_140px] gap-3 px-2 pb-2 text-[11px] text-fg-3 uppercase tracking-wider border-b border-border-soft">
                <span>Date</span>
                <span>Description</span>
                <span>Counterparty</span>
                <span className="text-right">Amount</span>
            </div>
            {query.data.map(bt => (
                <Row key={bt.id} bankTransaction={bt} catalog={catalog} />
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

function Row({
    bankTransaction,
    catalog,
}: {
    bankTransaction: BankTransaction;
    catalog: CurrencyCatalog;
}) {
    return (
        <div className="grid grid-cols-[100px_1fr_minmax(180px,1.2fr)_140px] gap-3 items-center px-2 py-2 border-b border-border-soft last:border-b-0">
            <span className="text-[12px] text-fg-3 tabular">{bankTransaction.bookingDate}</span>
            <div className="min-w-0 flex flex-col leading-tight">
                <span className="text-[13px] text-fg-1 truncate">
                    {bankTransaction.description}
                </span>
                <StateChip bankTransaction={bankTransaction} />
            </div>
            <CounterpartyCell bankTransaction={bankTransaction} />
            <AmountCell bankTransaction={bankTransaction} catalog={catalog} />
        </div>
    );
}

function StateChip({ bankTransaction }: { bankTransaction: BankTransaction }) {
    if (bankTransaction.journalEntryId) {
        return <span className="text-[11px] text-success tabular">Categorised</span>;
    }
    if (bankTransaction.dismissedAt) {
        const reason = bankTransaction.dismissedReason ?? '';
        return (
            <span className="text-[11px] text-fg-3 tabular truncate">
                Dismissed{reason ? ` · ${reason}` : ''}
            </span>
        );
    }
    return null;
}

function CounterpartyCell({ bankTransaction }: { bankTransaction: BankTransaction }) {
    const name = bankTransaction.counterpartyName;
    const iban = bankTransaction.counterpartyAccountNumber;
    if (!name && !iban) {
        return <span className="text-[12px] text-fg-3">—</span>;
    }
    return (
        <div className="min-w-0 flex flex-col leading-tight">
            <span className="text-[12px] text-fg-2 truncate">{name ?? '—'}</span>
            {iban && <span className="text-[11px] text-fg-3 truncate tabular">{iban}</span>}
        </div>
    );
}

function AmountCell({
    bankTransaction,
    catalog,
}: {
    bankTransaction: BankTransaction;
    catalog: CurrencyCatalog;
}) {
    const money = bankTransaction.money;
    const colour = money.amount < 0 ? 'text-danger' : 'text-success';
    return (
        <span className={cx('font-mono text-[13px] tabular text-right', colour)}>
            {formatMoney(money.amount, money.currencyCode, catalog, { sign: true })}
        </span>
    );
}
