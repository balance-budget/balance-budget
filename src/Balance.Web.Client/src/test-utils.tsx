/* eslint-disable react-refresh/only-export-components -- test helper module, not HMR-relevant. */
import type { ReactElement, ReactNode } from 'react';
import { render as rtlRender, type RenderOptions } from '@testing-library/react';
import { I18nProvider } from '@lingui/react';
import { createMemoryHistory, createRootRoute, createRouter } from '@tanstack/react-router';
import { RouterProvider as AriaRouterProvider } from 'react-aria-components';
import { i18n } from './i18n/i18n';
import { ariaRouterProps } from './lib/ariaRouter';

/**
 * A router with no routes: `buildLocation` still interpolates `to` + params +
 * search, which is all the anchors under test need, and it keeps the whole route
 * tree (and every screen it imports) out of component tests.
 */
export const testRouter = createRouter({
    routeTree: createRootRoute(),
    history: createMemoryHistory({ initialEntries: ['/'] }),
});

// Components wrapped with Lingui macros (<Trans>, useLingui) need the I18nProvider
// in the tree, and anything rendering a React Aria `href` needs the router bridge
// that resolves it to a URL. Tests render through this wrapper so they don't each
// repeat them.
function Wrapper({ children }: { children: ReactNode }) {
    return (
        <I18nProvider i18n={i18n}>
            <AriaRouterProvider {...ariaRouterProps(testRouter)}>{children}</AriaRouterProvider>
        </I18nProvider>
    );
}

function render(ui: ReactElement, options?: Omit<RenderOptions, 'wrapper'>) {
    return rtlRender(ui, { wrapper: Wrapper, ...options });
}

export * from '@testing-library/react';
export { render };
