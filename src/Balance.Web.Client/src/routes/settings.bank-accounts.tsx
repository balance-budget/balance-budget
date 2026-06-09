import { msg } from '@lingui/core/macro';
import { createFileRoute, Outlet } from '@tanstack/react-router';

export const Route = createFileRoute('/settings/bank-accounts')({
    component: () => <Outlet />,
    staticData: { title: msg`Bank accounts` },
});
