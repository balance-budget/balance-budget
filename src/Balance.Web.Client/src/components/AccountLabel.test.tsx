// @vitest-environment jsdom
import type { ReactNode } from 'react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, expect, it } from 'vitest';
import { accountsKeys, type Account } from '../api/accounts';
import { asAccountId } from '../lib/domain';
import { render, screen } from '../test-utils';
import { AccountLabel } from './AccountLabel';

/*
 * AccountLabel reads the chart of accounts out of the react-query cache, so these
 * render against a seeded client rather than mocking the hook — the cold-cache
 * fallback is half the component's contract (ADR-0039).
 *
 * The truncation itself is not asserted: jsdom has no layout, so which span shows an
 * ellipsis is unobservable here. The shrink classes are checked instead.
 */

function account(
    id: string,
    name: string,
    code: string,
    parentId: string | null,
    type: Account['type'] = 'Expense',
): Account {
    return {
        id: asAccountId(id),
        name,
        code,
        type,
        currencyCode: 'EUR',
        isPostable: parentId !== null,
        isLiquid: true,
        horizon: 'ShortTerm',
        parentId: parentId === null ? null : asAccountId(parentId),
        icon: null,
        balance: { amount: 0, currencyCode: 'EUR' },
        bankAccount: null,
    };
}

// Car ─ Insurance ─ Excess, plus a root of its own.
const ACCOUNTS: Account[] = [
    account('car', 'Car', '5000', null),
    account('car-ins', 'Insurance', '5130', 'car'),
    account('car-ins-excess', 'Excess', '5131', 'car-ins'),
    account('salary', 'Salary', '4100', null, 'Income'),
];

function renderLabel(ui: ReactNode, { seeded = true }: { seeded?: boolean } = {}) {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    if (seeded) {
        client.setQueryData(accountsKeys.list(), ACCOUNTS);
    }
    return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe('AccountLabel', () => {
    it('renders the full path with the ancestors dimmed and the leaf plain', () => {
        renderLabel(<AccountLabel accountId={asAccountId('car-ins-excess')} />);

        expect(screen.getByText('Car › Insurance').className).toContain('text-fg-3');
        expect(screen.getByText('Excess').className).not.toContain('text-fg-3');
    });

    it('lets the ancestors shrink away before the leaf does', () => {
        renderLabel(<AccountLabel accountId={asAccountId('car-ins-excess')} />);

        const ancestors = screen.getByText('Car › Insurance').className;
        expect(ancestors).toContain('truncate');
        expect(ancestors).toContain('shrink-[9999]');

        const leaf = screen.getByText('Excess').className;
        expect(leaf).toContain('truncate');
        expect(leaf).not.toContain('shrink-[9999]');
    });

    it('reveals the untruncated path through title', () => {
        renderLabel(<AccountLabel accountId={asAccountId('car-ins-excess')} />);

        expect(screen.getByTitle('Car › Insurance › Excess')).not.toBeNull();
    });

    it('renders a root account as its own name, with no separator', () => {
        renderLabel(<AccountLabel accountId={asAccountId('salary')} />);

        expect(screen.getByTitle('Salary')).not.toBeNull();
        expect(screen.queryByText('›')).toBeNull();
    });

    it('leaves the code out by default', () => {
        renderLabel(<AccountLabel accountId={asAccountId('car-ins-excess')} />);

        expect(screen.queryByText('5131')).toBeNull();
    });

    it('prefixes the code and puts it in the title reveal when showCode is set', () => {
        renderLabel(<AccountLabel accountId={asAccountId('car-ins-excess')} showCode />);

        expect(screen.getByText('5131')).not.toBeNull();
        // Read the attribute directly: getByTitle collapses the double space that
        // separates a code from its path.
        expect(screen.getByText('Excess').closest('[title]')?.getAttribute('title')).toBe(
            '5131  Car › Insurance › Excess',
        );
    });

    it('falls back to the flat name while the accounts cache is cold', () => {
        renderLabel(
            <AccountLabel accountId={asAccountId('car-ins-excess')} fallbackName="Excess" />,
            { seeded: false },
        );

        expect(screen.getByText('Excess')).not.toBeNull();
        expect(screen.queryByText('Car › Insurance')).toBeNull();
    });

    it('falls back to the flat name for an account missing from the chart', () => {
        renderLabel(<AccountLabel accountId={asAccountId('ghost')} fallbackName="Deleted thing" />);

        expect(screen.getByText('Deleted thing')).not.toBeNull();
    });

    it('renders an em dash when there is no name to be had at all', () => {
        renderLabel(<AccountLabel accountId={asAccountId('ghost')} />);

        expect(screen.getByText('—')).not.toBeNull();
    });

    it('draws the type-colored dot for the dot glyph, and no avatar', () => {
        const { container } = renderLabel(
            <AccountLabel accountId={asAccountId('car-ins-excess')} glyph="dot" />,
        );

        const dot = container.querySelector<HTMLElement>('.rounded-full');
        expect(dot?.style.background).toBe('var(--color-type-expense)');
        expect(container.querySelector('svg')).toBeNull();
    });

    it('draws the account avatar icon for the icon glyph', () => {
        const { container } = renderLabel(<AccountLabel accountId={asAccountId('salary')} />);

        expect(container.querySelector('svg')).not.toBeNull();
    });

    it('draws nothing for the none glyph', () => {
        const { container } = renderLabel(
            <AccountLabel accountId={asAccountId('salary')} glyph="none" />,
        );

        expect(container.querySelector('svg')).toBeNull();
        expect(container.querySelector('.rounded-full')).toBeNull();
    });
});
