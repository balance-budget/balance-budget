import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { Counterparties } from '../screens/Counterparties';
import { parsePage, parseQ, type PageQSearch } from '../lib/routeSearch';

export const Route = createFileRoute('/counterparties/')({
    component: function CounterpartiesRoute() {
        const { page, q } = Route.useSearch();
        const navigate = useNavigate({ from: Route.fullPath });
        return (
            <Counterparties
                page={page}
                q={q}
                onPageChange={p => {
                    void navigate({ search: prev => ({ ...prev, page: p }) });
                }}
                onSearchChange={value => {
                    void navigate({ search: prev => ({ ...prev, page: 1, q: value }) });
                }}
            />
        );
    },
    staticData: { title: 'Counterparties' },
    validateSearch: (raw: Record<string, unknown>): PageQSearch => ({
        page: parsePage(raw.page),
        q: parseQ(raw.q),
    }),
});
