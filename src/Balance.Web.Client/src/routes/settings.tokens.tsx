import { msg } from '@lingui/core/macro';
import { createFileRoute } from '@tanstack/react-router';
import { Tokens } from '../screens/Tokens';

export const Route = createFileRoute('/settings/tokens')({
    component: Tokens,
    staticData: { title: msg`API tokens` },
});
