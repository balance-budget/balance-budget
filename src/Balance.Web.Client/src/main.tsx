import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { createRouter, RouterProvider } from '@tanstack/react-router';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import '@fontsource/poppins/300.css';
import '@fontsource/poppins/400.css';
import '@fontsource/poppins/500.css';
import '@fontsource/poppins/600.css';
import '@fontsource/poppins/700.css';
import '@fontsource/jetbrains-mono/400.css';
import '@fontsource/jetbrains-mono/500.css';

import './index.css';
import { RouteError } from './components/RouteError';
import { routeTree } from './routeTree.gen';

const router = createRouter({
    routeTree,
    defaultErrorComponent: RouteError,
});

const queryClient = new QueryClient({
    defaultOptions: {
        queries: {
            staleTime: 30_000,
            refetchOnWindowFocus: false,
            retry: 1,
        },
    },
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
            <RouterProvider router={router} />
        </QueryClientProvider>
    </StrictMode>,
);
