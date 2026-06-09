import { msg } from '@lingui/core/macro';
import { createFileRoute, Outlet } from '@tanstack/react-router';

export const Route = createFileRoute('/loans')({
    component: () => <Outlet />,
    staticData: { title: msg`Loans` },
});
