import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { Counterparties } from '../screens/Counterparties';

type CounterpartiesSearch = { page: number; q: string };

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
    validateSearch: (raw: Record<string, unknown>): CounterpartiesSearch => {
        const candidate = Number(raw.page);
        const page = Number.isInteger(candidate) && candidate >= 1 ? candidate : 1;
        const q = typeof raw.q === 'string' ? raw.q : '';
        return { page, q };
    },
});
