import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { Journal } from '../screens/Journal';

type JournalSearch = { page: number };

export const Route = createFileRoute('/journal/')({
    component: function JournalRoute() {
        const { page } = Route.useSearch();
        const navigate = useNavigate({ from: Route.fullPath });
        return (
            <Journal
                page={page}
                onPageChange={p => {
                    void navigate({ search: { page: p } });
                }}
            />
        );
    },
    staticData: { title: 'Journal entries' },
    validateSearch: (raw: Record<string, unknown>): JournalSearch => {
        const candidate = Number(raw.page);
        const page = Number.isInteger(candidate) && candidate >= 1 ? candidate : 1;
        return { page };
    },
});
