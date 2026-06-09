import { msg } from '@lingui/core/macro';
import { createFileRoute } from '@tanstack/react-router';
import { Setup } from '../screens/Setup';

export const Route = createFileRoute('/setup')({
    component: Setup,
    staticData: { title: msg`First-run setup` },
});
