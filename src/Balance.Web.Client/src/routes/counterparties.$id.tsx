import { msg } from '@lingui/core/macro';
import { createFileRoute } from '@tanstack/react-router';
import { CounterpartyDetail } from '../screens/CounterpartyDetail';
import { asCounterpartyId } from '../lib/domain';
import { parsePage } from '../lib/routeSearch';

type Search = { page: number };

export const Route = createFileRoute('/counterparties/$id')({
    component: function CounterpartyDetailRoute() {
        const { id } = Route.useParams();
        const { page } = Route.useSearch();
        return (
            <CounterpartyDetail
                id={asCounterpartyId(id)}
                page={page}
                hrefForPage={p => ({
                    from: Route.fullPath,
                    to: '.',
                    search: (prev: Record<string, unknown>) => ({ ...prev, page: p }),
                })}
            />
        );
    },
    staticData: { title: msg`Counterparty` },
    validateSearch: (raw: Record<string, unknown>): Search => ({ page: parsePage(raw.page) }),
});
