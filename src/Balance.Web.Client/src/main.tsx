import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { createRouter, RouterProvider } from '@tanstack/react-router';
import { MutationCache, QueryCache, QueryClient, QueryClientProvider } from '@tanstack/react-query';

import '@fontsource/poppins/300.css';
import '@fontsource/poppins/400.css';
import '@fontsource/poppins/500.css';
import '@fontsource/poppins/600.css';
import '@fontsource/poppins/700.css';
import '@fontsource/jetbrains-mono/400.css';
import '@fontsource/jetbrains-mono/500.css';

import './index.css';
import { authKeys } from './api/auth';
import { RouteError } from './components/RouteError';
import { AppToastRegion } from './components/ui/Toast';
import { LocaleProvider } from './i18n/LocaleProvider';
import { ApiError } from './lib/http';
import { routeTree } from './routeTree.gen';

const router = createRouter({
    routeTree,
    defaultErrorComponent: RouteError,
});

function isAuthFlowQuery(queryKey: readonly unknown[]): boolean {
    // The /me bootstrap probe handles its own 401 (it's literally asking "am I logged in?").
    return queryKey[0] === authKeys.me[0];
}

function handleUnauthenticated() {
    const currentPath = window.location.pathname;
    if (currentPath === '/login' || currentPath === '/setup') return;
    queryClient.setQueryData(authKeys.me, null);
    void router.navigate({
        to: '/login',
        search: { returnTo: currentPath },
        replace: true,
    });
}

const queryClient = new QueryClient({
    defaultOptions: {
        queries: {
            staleTime: 30_000,
            refetchOnWindowFocus: false,
            retry: 1,
        },
    },
    queryCache: new QueryCache({
        onError: (error, query) => {
            if (
                error instanceof ApiError &&
                error.status === 401 &&
                !isAuthFlowQuery(query.queryKey)
            ) {
                handleUnauthenticated();
            }
        },
    }),
    mutationCache: new MutationCache({
        onError: error => {
            if (error instanceof ApiError && error.status === 401) {
                handleUnauthenticated();
            }
        },
    }),
});

/* eslint-disable @typescript-eslint/consistent-type-definitions --
   declaration-merging into TanStack's module requires `interface`. */
declare module '@tanstack/react-router' {
    interface Register {
        router: typeof router;
    }
    // Route-level static metadata — each createFileRoute may set this so
    // __root can render a title without a hand-maintained pathname map.
    interface StaticDataRouteOption {
        title?: string;
    }
}
/* eslint-enable @typescript-eslint/consistent-type-definitions */

const rootElement = document.getElementById('root');
if (!rootElement) throw new Error('Missing #root element in index.html');

createRoot(rootElement).render(
    <StrictMode>
        <QueryClientProvider client={queryClient}>
            <LocaleProvider>
                <RouterProvider router={router} />
                <AppToastRegion />
            </LocaleProvider>
        </QueryClientProvider>
    </StrictMode>,
);
