import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { Activity } from '../screens/Activity';

type ActivitySearch = { page: number; q: string };

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
    validateSearch: (raw: Record<string, unknown>): ActivitySearch => {
        const candidate = Number(raw.page);
        const page = Number.isInteger(candidate) && candidate >= 1 ? candidate : 1;
        const q = typeof raw.q === 'string' ? raw.q : '';
        return { page, q };
    },
});
