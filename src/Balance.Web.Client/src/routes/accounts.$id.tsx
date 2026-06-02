import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { AccountDetail } from '../screens/AccountDetail';
import { asAccountId } from '../lib/domain';
import { parsePage, parseQ, type PageQSearch } from '../lib/routeSearch';

export const Route = createFileRoute('/accounts/$id')({
    component: function AccountDetailRoute() {
        const { id } = Route.useParams();
        const { page, q } = Route.useSearch();
        const navigate = useNavigate({ from: Route.fullPath });
        return (
            <AccountDetail
                id={asAccountId(id)}
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
    staticData: { title: 'Account' },
    validateSearch: (raw: Record<string, unknown>): PageQSearch => ({
        page: parsePage(raw.page),
        q: parseQ(raw.q),
    }),
});
