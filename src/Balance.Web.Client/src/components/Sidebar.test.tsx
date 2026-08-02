// @vitest-environment jsdom
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import {
    createMemoryHistory,
    createRootRoute,
    createRouter,
    RouterProvider,
} from '@tanstack/react-router';
import { I18nProvider } from '@lingui/react';
import { RouterProvider as AriaRouterProvider } from 'react-aria-components';
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { accountsKeys, type Account } from '../api/accounts';
import { authKeys } from '../api/auth';
import { i18n } from '../i18n/i18n';
import { ariaRouterProps } from '../lib/router';
import { asAccountId } from '../lib/domain';
import { Sidebar } from './Sidebar';

/*
 * The sidebar tree has the most bespoke row content in the app (avatar, balance,
 * an expand button that must stay outside the anchor), so it is where an account
 * row is most likely to lose its link and quietly go back to being a div that
 * cannot be opened in a new tab (ADR-0040).
 */

function account(id: string, name: string): Account {
    return {
        id: asAccountId(id),
        name,
        code: '1000',
        type: 'Asset',
        currencyCode: 'EUR',
        isPostable: true,
        isLiquid: true,
        horizon: 'ShortTerm',
        parentId: null,
        icon: null,
        balance: { amount: 12_345, currencyCode: 'EUR' },
        bankAccount: null,
    };
}

function renderSidebar() {
    const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    queryClient.setQueryData(accountsKeys.list(), [account('1', 'Checking')]);
    queryClient.setQueryData(authKeys.me, null);

    const router = createRouter({
        routeTree: createRootRoute({
            component: () => <Sidebar open onClose={() => undefined} />,
        }),
        history: createMemoryHistory({ initialEntries: ['/'] }),
    });

    return render(
        <QueryClientProvider client={queryClient}>
            <I18nProvider i18n={i18n}>
                <AriaRouterProvider {...ariaRouterProps(router)}>
                    <RouterProvider router={router} />
                </AriaRouterProvider>
            </I18nProvider>
        </QueryClientProvider>,
    );
}

describe('sidebar account tree', () => {
    it('renders each account as a link to its register', async () => {
        renderSidebar();

        const link = await screen.findByRole('link', { name: /Checking/ });

        expect(link.tagName).toBe('A');
        expect(link.getAttribute('href')).toContain('/accounts/1');
    });
});
