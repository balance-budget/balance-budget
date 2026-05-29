import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { BANK_TRANSACTION_FILTERS, type BankTransactionFilter } from '../api/bankTransactions';
import { BankTransactionsInbox } from '../screens/BankTransactionsInbox';

type Search = { page: number; filter: BankTransactionFilter; q: string };

function isFilter(value: unknown): value is BankTransactionFilter {
    return (
        typeof value === 'string' && (BANK_TRANSACTION_FILTERS as readonly string[]).includes(value)
    );
}

export const Route = createFileRoute('/bank-transactions/')({
    component: function BankTransactionsRoute() {
        const { page, filter, q } = Route.useSearch();
        const navigate = useNavigate({ from: Route.fullPath });
        return (
            <BankTransactionsInbox
                page={page}
                filter={filter}
                q={q}
                onPageChange={p => {
                    void navigate({ search: prev => ({ ...prev, page: p }) });
                }}
                // Filter changes always reset to page 1 — pagination is per-filter.
                onFilterChange={f => {
                    void navigate({ search: prev => ({ ...prev, page: 1, filter: f }) });
                }}
                // Search changes reset to page 1 — the previous page may not exist
                // for the narrowed result set.
                onSearchChange={value => {
                    void navigate({ search: prev => ({ ...prev, page: 1, q: value }) });
                }}
            />
        );
    },
    staticData: { title: 'Bank transactions' },
    validateSearch: (raw: Record<string, unknown>): Search => {
        const candidate = Number(raw.page);
        const page = Number.isInteger(candidate) && candidate >= 1 ? candidate : 1;
        const filter = isFilter(raw.filter) ? raw.filter : 'Inbox';
        const q = typeof raw.q === 'string' ? raw.q : '';
        return { page, filter, q };
    },
});
