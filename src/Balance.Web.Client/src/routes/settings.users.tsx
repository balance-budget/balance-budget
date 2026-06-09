import { msg } from '@lingui/core/macro';
import { createFileRoute } from '@tanstack/react-router';
import { Users } from '../screens/Users';

export const Route = createFileRoute('/settings/users')({
    component: Users,
    staticData: { title: msg`Users` },
});
