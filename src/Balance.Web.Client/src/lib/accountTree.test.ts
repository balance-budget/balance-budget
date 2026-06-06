import { describe, expect, it } from 'vitest';
import type { Account } from '../api/accounts';
import { asAccountId, type AccountId } from './domain';
import { accountPathLabel, accountPathSegments, descendantAndSelfIds } from './accountTree';

function account(id: string, name: string, code: string, parentId: string | null): Account {
    return {
        id: asAccountId(id),
        name,
        code,
        type: 'Expense',
        currencyCode: 'EUR',
        isPostable: parentId !== null,
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

describe('descendantAndSelfIds', () => {
    it('collects the root and every transitive descendant', () => {
        const ids = descendantAndSelfIds(tree(), asAccountId('car'));
        expect([...ids].sort()).toEqual(['car', 'car-ins', 'car-ins-liab', 'car-tax']);
    });

    it('returns just the account itself for a leaf', () => {
        expect([...descendantAndSelfIds(tree(), asAccountId('home-tax'))]).toEqual(['home-tax']);
    });
});
