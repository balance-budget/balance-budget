import { createFileRoute, useNavigate } from '@tanstack/react-router';
import type { RegisterStatusFilter } from '../api/register';
import { AccountDetail, type RegisterFilterState } from '../screens/AccountDetail';
import { asAccountId } from '../lib/domain';
import { parseDate, parsePage, parseQ, type PageQSearch } from '../lib/routeSearch';

const STATUS_VALUES: readonly RegisterStatusFilter[] = ['Uncleared', 'Cleared', 'Reconciled'];

type RegisterSearch = PageQSearch & {
    posted: string;
    counter: string;
    from: string;
    to: string;
    status: RegisterStatusFilter;
};

function parseAccountParam(raw: unknown): string {
    return typeof raw === 'string' ? raw : '';
}

function parseStatus(raw: unknown): RegisterStatusFilter {
    return STATUS_VALUES.find(s => s === raw) ?? '';
}

export const Route = createFileRoute('/accounts/$id')({
    component: function AccountDetailRoute() {
        const { id } = Route.useParams();
        const { page, q, posted, counter, from, to, status } = Route.useSearch();
        const navigate = useNavigate({ from: Route.fullPath });
        const filters: RegisterFilterState = {
            posted: posted === '' ? null : asAccountId(posted),
            counter: counter === '' ? null : asAccountId(counter),
            from,
            to,
            status,
        };
        return (
            <AccountDetail
                id={asAccountId(id)}
                page={page}
                q={q}
                filters={filters}
                onPageChange={p => {
                    void navigate({ search: prev => ({ ...prev, page: p }) });
                }}
                onSearchChange={value => {
                    void navigate({ search: prev => ({ ...prev, page: 1, q: value }) });
                }}
                onFiltersChange={patch => {
                    void navigate({
                        search: prev => ({
                            ...prev,
                            page: 1,
                            ...(patch.posted !== undefined && { posted: patch.posted ?? '' }),
                            ...(patch.counter !== undefined && { counter: patch.counter ?? '' }),
                            ...(patch.from !== undefined && { from: patch.from }),
                            ...(patch.to !== undefined && { to: patch.to }),
                            ...(patch.status !== undefined && { status: patch.status }),
                        }),
                    });
                }}
            />
        );
    },
    staticData: { title: 'Account' },
    validateSearch: (raw: Record<string, unknown>): RegisterSearch => ({
        page: parsePage(raw.page),
        q: parseQ(raw.q),
        posted: parseAccountParam(raw.posted),
        counter: parseAccountParam(raw.counter),
        from: parseDate(raw.from),
        to: parseDate(raw.to),
        status: parseStatus(raw.status),
    }),
});
