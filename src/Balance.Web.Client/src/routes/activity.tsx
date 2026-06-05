import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { Activity, type ActivityFilterState } from '../screens/Activity';
import { asAccountId } from '../lib/domain';
import { parseDate, parsePage, parseQ, type PageQSearch } from '../lib/routeSearch';

type ActivitySearch = PageQSearch & {
    account: string;
    from: string;
    to: string;
};

function parseAccountParam(raw: unknown): string {
    return typeof raw === 'string' ? raw : '';
}

export const Route = createFileRoute('/activity')({
    component: function ActivityRoute() {
        const { page, q, account, from, to } = Route.useSearch();
        const navigate = useNavigate({ from: Route.fullPath });
        const filters: ActivityFilterState = {
            account: account === '' ? null : asAccountId(account),
            from,
            to,
        };
        return (
            <Activity
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
                            ...(patch.account !== undefined && { account: patch.account ?? '' }),
                            ...(patch.from !== undefined && { from: patch.from }),
                            ...(patch.to !== undefined && { to: patch.to }),
                        }),
                    });
                }}
            />
        );
    },
    staticData: { title: 'Activity' },
    validateSearch: (raw: Record<string, unknown>): ActivitySearch => ({
        page: parsePage(raw.page),
        q: parseQ(raw.q),
        account: parseAccountParam(raw.account),
        from: parseDate(raw.from),
        to: parseDate(raw.to),
    }),
});
