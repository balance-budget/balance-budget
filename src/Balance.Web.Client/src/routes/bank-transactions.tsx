import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { BankTransactions } from '../screens/BankTransactions';
import type { BankTransactionFilter } from '../api/bankTransactions';

type Search = { page: number; filter: BankTransactionFilter };

const FILTER_VALUES: ReadonlySet<BankTransactionFilter> = new Set<BankTransactionFilter>([
    'Inbox',
    'Matched',
    'Dismissed',
    'All',
]);

function parseFilter(raw: unknown): BankTransactionFilter {
    return typeof raw === 'string' && FILTER_VALUES.has(raw as BankTransactionFilter)
        ? (raw as BankTransactionFilter)
        : 'Inbox';
}

export const Route = createFileRoute('/bank-transactions')({
    component: function BankTransactionsRoute() {
        const { page, filter } = Route.useSearch();
        const navigate = useNavigate({ from: Route.fullPath });
        return (
            <BankTransactions
                page={page}
                filter={filter}
                onPageChange={p => {
                    void navigate({ search: { page: p, filter } });
                }}
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
        return { page, filter: parseFilter(raw.filter) };
    },
});
