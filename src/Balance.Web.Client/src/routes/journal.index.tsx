import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { Journal } from '../screens/Journal';

type JournalSearch = { page: number; q: string };

export const Route = createFileRoute('/journal/')({
    component: function JournalRoute() {
        const { page, q } = Route.useSearch();
        const navigate = useNavigate({ from: Route.fullPath });
        return (
            <Journal
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
    staticData: { title: 'Journal entries' },
    validateSearch: (raw: Record<string, unknown>): JournalSearch => {
        const candidate = Number(raw.page);
        const page = Number.isInteger(candidate) && candidate >= 1 ? candidate : 1;
        const q = typeof raw.q === 'string' ? raw.q : '';
        return { page, q };
    },
});
