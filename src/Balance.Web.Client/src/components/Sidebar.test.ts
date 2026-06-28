import { describe, expect, it } from 'vitest';
import type { Account } from '../api/accounts';
import { asAccountId, type AccountType } from '../lib/domain';
import { computeRowDecor } from './sidebarTree';

function account(id: string, code: string, parentId: string | null): Account {
    return {
        id: asAccountId(id),
        name: id,
        code,
        type: 'Asset',
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

// transport ─ public, car ─ fuel, insurance, parking ; transport ─ bike
const TREE: Account[] = [
    account('transport', '1000', null),
    account('public', '1100', 'transport'),
    account('car', '1200', 'transport'),
    account('fuel', '1210', 'car'),
    account('insurance', '1220', 'car'),
    account('parking', '1230', 'car'),
    account('bike', '1300', 'transport'),
];

const ORDER: AccountType[] = ['Asset'];

describe('computeRowDecor', () => {
    it('rounds each nesting block at its first and last visible row', () => {
        const decor = computeRowDecor(TREE, ORDER, new Set(['transport', 'car']));

        // Root rows are never part of a block.
        expect(decor.get('transport')).toEqual({ roundTop: false, roundBottom: false });

        // Outer block (transport's children): opens at the first child, closes at
        // the last child after the inner subtree.
        expect(decor.get('public')).toEqual({ roundTop: true, roundBottom: false });
        expect(decor.get('bike')).toEqual({ roundTop: false, roundBottom: true });

        // Inner block (car's children): opens at fuel, closes at parking.
        expect(decor.get('fuel')).toEqual({ roundTop: true, roundBottom: false });
        expect(decor.get('insurance')).toEqual({ roundTop: false, roundBottom: false });
        expect(decor.get('parking')).toEqual({ roundTop: false, roundBottom: true });
    });

    it('a collapsed node hides its children, so its block disappears', () => {
        const decor = computeRowDecor(TREE, ORDER, new Set(['transport']));
        // car is collapsed: public..bike are the only L2 rows, fuel/etc not visible.
        expect(decor.get('public')).toEqual({ roundTop: true, roundBottom: false });
        expect(decor.get('bike')).toEqual({ roundTop: false, roundBottom: true });
        expect(decor.has('fuel')).toBe(false);
    });

    it('a single visible child is a fully rounded block', () => {
        const decor = computeRowDecor(
            [account('root', '2000', null), account('only', '2100', 'root')],
            ORDER,
            new Set(['root']),
        );
        expect(decor.get('only')).toEqual({ roundTop: true, roundBottom: true });
    });
});
