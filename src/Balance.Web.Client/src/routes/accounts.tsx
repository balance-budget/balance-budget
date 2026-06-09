import { msg } from '@lingui/core/macro';
import { createFileRoute, Outlet } from '@tanstack/react-router';

export const Route = createFileRoute('/accounts')({
    component: () => <Outlet />,
    staticData: { title: msg`Accounts` },
});
