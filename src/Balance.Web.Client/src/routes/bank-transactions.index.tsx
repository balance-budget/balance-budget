import { msg } from '@lingui/core/macro';
import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { BANK_TRANSACTION_FILTERS, type BankTransactionFilter } from '../api/bankTransactions';
import { BankTransactionsInbox } from '../screens/BankTransactionsInbox';
import { parsePage, parseQ } from '../lib/routeSearch';

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
    staticData: { title: msg`Bank transactions` },
    validateSearch: (raw: Record<string, unknown>): Search => ({
        page: parsePage(raw.page),
        filter: isFilter(raw.filter) ? raw.filter : 'Inbox',
        q: parseQ(raw.q),
    }),
});
