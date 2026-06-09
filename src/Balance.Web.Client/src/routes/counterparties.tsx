import { msg } from '@lingui/core/macro';
import { createFileRoute, Outlet } from '@tanstack/react-router';

export const Route = createFileRoute('/counterparties')({
    component: () => <Outlet />,
    staticData: { title: msg`Counterparties` },
});
