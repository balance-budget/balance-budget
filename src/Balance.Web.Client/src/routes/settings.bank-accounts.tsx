import { createFileRoute, Outlet } from '@tanstack/react-router';

export const Route = createFileRoute('/settings/bank-accounts')({
    component: () => <Outlet />,
    staticData: { title: 'Bank accounts' },
});
