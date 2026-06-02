import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { Activity } from '../screens/Activity';
import { parsePage, parseQ, type PageQSearch } from '../lib/routeSearch';

export const Route = createFileRoute('/activity')({
    component: function ActivityRoute() {
        const { page, q } = Route.useSearch();
        const navigate = useNavigate({ from: Route.fullPath });
        return (
            <Activity
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
    staticData: { title: 'Activity' },
    validateSearch: (raw: Record<string, unknown>): PageQSearch => ({
        page: parsePage(raw.page),
        q: parseQ(raw.q),
    }),
});
