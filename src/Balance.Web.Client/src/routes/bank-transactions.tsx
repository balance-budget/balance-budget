import { createFileRoute, Outlet } from '@tanstack/react-router';

export const Route = createFileRoute('/bank-transactions')({
    component: () => <Outlet />,
    staticData: { title: 'Bank transactions' },
});
