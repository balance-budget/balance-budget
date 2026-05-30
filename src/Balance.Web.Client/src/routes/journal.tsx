import { createFileRoute, Outlet } from '@tanstack/react-router';

export const Route = createFileRoute('/journal')({
    component: () => <Outlet />,
    staticData: { title: 'Journal' },
});
