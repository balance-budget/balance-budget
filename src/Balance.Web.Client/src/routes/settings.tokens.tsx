import { createFileRoute } from '@tanstack/react-router';
import { Tokens } from '../screens/Tokens';

export const Route = createFileRoute('/settings/tokens')({
    component: Tokens,
    staticData: { title: 'API tokens' },
});
