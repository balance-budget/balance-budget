import { describe, expect, it } from 'vitest';
import type { Account } from '../api/accounts';
import { asAccountId, type AccountId } from './domain';
import {
    accountPathLabel,
    accountPathSegments,
    buildChildrenMap,
    descendantAndSelfIds,
    groupRootsByType,
    sortSiblings,
} from './accountTree';

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

// Car ─ Tax, Car ─ Insurance ─ Liability ; Home ─ Tax
function tree(): Account[] {
    return [
        account('car', 'Car', '5000', null),
        account('car-tax', 'Tax', '5120', 'car'),
        account('car-ins', 'Insurance', '5130', 'car'),
        account('car-ins-liab', 'Liability', '5131', 'car-ins'),
        account('home', 'Home', '6000', null),
        account('home-tax', 'Tax', '6120', 'home'),
    ];
}

function byId(accounts: Account[]): Map<AccountId, Account> {
    return new Map(accounts.map(a => [a.id, a]));
}

describe('accountPathSegments', () => {
    it('returns the full chain from root to leaf', () => {
        const map = byId(tree());
        expect(accountPathSegments(map, asAccountId('car-tax'))).toEqual(['Car', 'Tax']);
        expect(accountPathSegments(map, asAccountId('car-ins-liab'))).toEqual([
            'Car',
            'Insurance',
            'Liability',
        ]);
    });

    it('returns a single segment for a root account', () => {
        expect(accountPathSegments(byId(tree()), asAccountId('home'))).toEqual(['Home']);
    });

    it('returns an empty chain for an unknown account', () => {
        expect(accountPathSegments(byId(tree()), asAccountId('ghost'))).toEqual([]);
    });
});

describe('accountPathLabel', () => {
    it('prefixes the code and joins the path so sibling leaves are distinct', () => {
        const map = byId(tree());
        expect(accountPathLabel(map, asAccountId('car-tax'))).toBe('5120  Car › Tax');
        expect(accountPathLabel(map, asAccountId('home-tax'))).toBe('6120  Home › Tax');
    });

    it('returns null for an unknown account', () => {
        expect(accountPathLabel(byId(tree()), asAccountId('ghost'))).toBeNull();
    });
});

describe('sortSiblings', () => {
    it('orders by code numerically, then by name', () => {
        const a = account('a', 'Zeta', '5000', null);
        const b = account('b', 'Alpha', '5100', null);
        const c = account('c', 'Beta', '5000', null);
        // 5000 group ordered by name (Beta < Zeta), then 5100.
        expect([a, b, c].sort(sortSiblings).map(x => x.id)).toEqual([
            asAccountId('c'),
            asAccountId('a'),
            asAccountId('b'),
        ]);
    });

    it('compares codes numerically, not lexically (9 before 10)', () => {
        const nine = account('nine', 'Nine', '9', null);
        const ten = account('ten', 'Ten', '10', null);
        expect([ten, nine].sort(sortSiblings).map(x => x.id)).toEqual([
            asAccountId('nine'),
            asAccountId('ten'),
        ]);
    });
});

describe('buildChildrenMap', () => {
    it('buckets children under their parent, roots under null, each sorted', () => {
        const map = buildChildrenMap(tree());
        expect(map.get(null)?.map(a => a.id)).toEqual([asAccountId('car'), asAccountId('home')]);
        expect(map.get(asAccountId('car'))?.map(a => a.id)).toEqual([
            asAccountId('car-tax'),
            asAccountId('car-ins'),
        ]);
        expect(map.get(asAccountId('home'))?.map(a => a.id)).toEqual([asAccountId('home-tax')]);
    });
});

describe('groupRootsByType', () => {
    it('groups only roots by type, sorted within each type', () => {
        const accounts = [
            account('asset-b', 'Bank', '1100', null, 'Asset'),
            account('asset-a', 'Cash', '1000', null, 'Asset'),
            account('asset-a-child', 'Wallet', '1010', 'asset-a', 'Asset'),
            account('exp', 'Groceries', '5000', null, 'Expense'),
        ];
        const grouped = groupRootsByType(accounts);
        expect(grouped.get('Asset')?.map(a => a.id)).toEqual([
            asAccountId('asset-a'),
            asAccountId('asset-b'),
        ]);
        expect(grouped.get('Expense')?.map(a => a.id)).toEqual([asAccountId('exp')]);
    });
});

describe('descendantAndSelfIds', () => {
    it('collects the root and every transitive descendant', () => {
        const ids = descendantAndSelfIds(tree(), asAccountId('car'));
        expect([...ids].sort()).toEqual(['car', 'car-ins', 'car-ins-liab', 'car-tax']);
    });

    it('returns just the account itself for a leaf', () => {
        expect([...descendantAndSelfIds(tree(), asAccountId('home-tax'))]).toEqual(['home-tax']);
    });
});
