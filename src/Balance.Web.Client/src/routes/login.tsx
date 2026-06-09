import { msg } from '@lingui/core/macro';
import { createFileRoute } from '@tanstack/react-router';
import { Login } from '../screens/Login';

type LoginSearch = { returnTo?: string };

export const Route = createFileRoute('/login')({
    validateSearch: (search: Record<string, unknown>): LoginSearch => ({
        returnTo: typeof search.returnTo === 'string' ? search.returnTo : undefined,
    }),
    component: function LoginRoute() {
        const { returnTo } = Route.useSearch();
        return <Login returnTo={returnTo} />;
    },
    staticData: { title: msg`Sign in` },
});
