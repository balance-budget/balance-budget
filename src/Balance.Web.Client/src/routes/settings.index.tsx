import { createFileRoute, redirect } from '@tanstack/react-router';

export const Route = createFileRoute('/settings/')({
    beforeLoad: () => {
        // TanStack Router's documented redirect pattern uses `throw redirect(...)`
        // — the value is a sentinel the router catches, not a runtime Error.
        // eslint-disable-next-line @typescript-eslint/only-throw-error
        throw redirect({
            to: '/settings/bank-accounts',
            search: { owner: 'Mine', page: 1, q: '' },
        });
    },
});
