import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { BANK_TRANSACTION_FILTERS, type BankTransactionFilter } from '../api/bankTransactions';
import { BankTransactionsInbox } from '../screens/BankTransactionsInbox';

type Search = { page: number; filter: BankTransactionFilter };

function isFilter(value: unknown): value is BankTransactionFilter {
    return (
        typeof value === 'string' && (BANK_TRANSACTION_FILTERS as readonly string[]).includes(value)
    );
}

export const Route = createFileRoute('/bank-transactions/')({
    component: function BankTransactionsRoute() {
        const { page, filter } = Route.useSearch();
        const navigate = useNavigate({ from: Route.fullPath });
        return (
            <BankTransactionsInbox
                page={page}
                filter={filter}
                onPageChange={p => {
                    void navigate({ search: prev => ({ ...prev, page: p }) });
                }}
                // Filter changes always reset to page 1 — pagination is per-filter.
                onFilterChange={f => {
                    void navigate({ search: { page: 1, filter: f } });
                }}
            />
        );
    },
    staticData: { title: 'Bank transactions' },
    validateSearch: (raw: Record<string, unknown>): Search => {
        const candidate = Number(raw.page);
        const page = Number.isInteger(candidate) && candidate >= 1 ? candidate : 1;
        const filter = isFilter(raw.filter) ? raw.filter : 'Inbox';
        return { page, filter };
    },
});
