import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { CounterpartyDetail } from '../screens/CounterpartyDetail';
import { asCounterpartyId } from '../lib/domain';

type Search = { page: number };

export const Route = createFileRoute('/counterparties/$id')({
    component: function CounterpartyDetailRoute() {
        const { id } = Route.useParams();
        const { page } = Route.useSearch();
        const navigate = useNavigate({ from: Route.fullPath });
        return (
            <CounterpartyDetail
                id={asCounterpartyId(id)}
                page={page}
                onPageChange={p => {
                    void navigate({ search: prev => ({ ...prev, page: p }) });
                }}
            />
        );
    },
    staticData: { title: 'Counterparty' },
    validateSearch: (raw: Record<string, unknown>): Search => {
        const candidate = Number(raw.page);
        const page = Number.isInteger(candidate) && candidate >= 1 ? candidate : 1;
        return { page };
    },
});
