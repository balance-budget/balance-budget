import type { AccountId, AccountType } from './domain';

export type VisualHint = {
    accentColor: string;
    iconName: string;
};

const ICON_BY_TYPE: Record<AccountType, string> = {
    Asset: 'wallet',
    Liability: 'credit-card',
    Equity: 'landmark',
    Income: 'trending-up',
    Expense: 'shopping-cart',
};

const PALETTE_BY_TYPE: Record<AccountType, readonly string[]> = {
    Asset: ['var(--color-cat-transport)', 'var(--color-cat-savings)'],
    Liability: ['var(--color-cat-shopping)', 'var(--color-cat-bills)'],
    Equity: ['var(--color-cat-housing)'],
    Income: ['var(--color-cat-entertain)'],
    Expense: [
        'var(--color-cat-food)',
        'var(--color-cat-bills)',
        'var(--color-cat-shopping)',
        'var(--color-cat-housing)',
        'var(--color-cat-transport)',
    ],
};

// FNV-1a 32-bit. Deterministic and dependency-free; only used to pick a palette
// slot so collisions are harmless beyond two accounts sharing a tint.
function hashId(id: string): number {
    let hash = 2166136261;
    for (let i = 0; i < id.length; i++) {
        hash ^= id.charCodeAt(i);
        hash = Math.imul(hash, 16777619) >>> 0;
    }
    return hash;
}

export function visualHintFor(accountType: AccountType, id: AccountId): VisualHint {
    const palette = PALETTE_BY_TYPE[accountType];
    return {
        accentColor: palette[hashId(id) % palette.length],
        iconName: ICON_BY_TYPE[accountType],
    };
}
