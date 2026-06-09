import { msg } from '@lingui/core/macro';
import { createFileRoute, Outlet } from '@tanstack/react-router';

export const Route = createFileRoute('/journal')({
    component: () => <Outlet />,
    staticData: { title: msg`Journal` },
});
