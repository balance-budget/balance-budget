import { msg } from '@lingui/core/macro';
import { createFileRoute, Outlet } from '@tanstack/react-router';

export const Route = createFileRoute('/bank-transactions')({
    component: () => <Outlet />,
    staticData: { title: msg`Bank transactions` },
});
