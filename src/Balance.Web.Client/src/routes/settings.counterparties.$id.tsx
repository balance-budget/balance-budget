import { createFileRoute } from '@tanstack/react-router';
import { CounterpartyDetail } from '../screens/CounterpartyDetail';
import { asCounterpartyId } from '../lib/domain';

export const Route = createFileRoute('/settings/counterparties/$id')({
    component: function CounterpartyDetailRoute() {
        const { id } = Route.useParams();
        return <CounterpartyDetail id={asCounterpartyId(id)} />;
    },
    staticData: { title: 'Counterparty' },
});
