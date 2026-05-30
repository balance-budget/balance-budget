import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { AccountDetail } from '../screens/AccountDetail';
import { asAccountId } from '../lib/domain';

type RegisterSearch = { page: number; q: string };

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
    validateSearch: (raw: Record<string, unknown>): RegisterSearch => {
        const candidate = Number(raw.page);
        const page = Number.isInteger(candidate) && candidate >= 1 ? candidate : 1;
        const q = typeof raw.q === 'string' ? raw.q : '';
        return { page, q };
    },
});
