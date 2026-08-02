// Must run before any React Aria collection renders (see the module comment).
import './lib/patchReactAriaChildNodes';

import { StrictMode } from 'react';
import type { MessageDescriptor } from '@lingui/core';
import { createRoot } from 'react-dom/client';
import {
    createRouter,
    RouterProvider,
    type NavigateOptions,
    type ToOptions,
} from '@tanstack/react-router';
import { RouterProvider as AriaRouterProvider } from 'react-aria-components';
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
import { ariaRouterProps } from './lib/router';
import { RouteError } from './components/RouteError';
import { ThemeProvider } from './components/ThemeProvider';
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
// React Aria's `href` is typed through this augmentation, so every RAC link takes
// TanStack's `ToOptions` object instead of a hand-written URL string (ADR-0040).
declare module 'react-aria-components' {
    interface RouterConfig {
        href: ToOptions;
        routerOptions: Omit<NavigateOptions, keyof ToOptions>;
    }
}

declare module '@tanstack/react-router' {
    interface Register {
        router: typeof router;
    }
    // Route-level static metadata — each createFileRoute may set this so
    // __root can render a title without a hand-maintained pathname map.
    interface StaticDataRouteOption {
        title?: MessageDescriptor;
    }
}
/* eslint-enable @typescript-eslint/consistent-type-definitions */

const rootElement = document.getElementById('root');
if (!rootElement) throw new Error('Missing #root element in index.html');

createRoot(rootElement).render(
    <StrictMode>
        <QueryClientProvider client={queryClient}>
            <ThemeProvider>
                <LocaleProvider>
                    <AriaRouterProvider {...ariaRouterProps(router)}>
                        <RouterProvider router={router} />
                    </AriaRouterProvider>
                    <AppToastRegion />
                </LocaleProvider>
            </ThemeProvider>
        </QueryClientProvider>
    </StrictMode>,
);
