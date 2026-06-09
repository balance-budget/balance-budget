import { msg } from '@lingui/core/macro';
import { createFileRoute } from '@tanstack/react-router';
import { Dashboard } from '../screens/Dashboard';

export const Route = createFileRoute('/')({
    component: Dashboard,
    staticData: { title: msg`Dashboard` },
});
