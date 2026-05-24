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

// One accent per AccountType — accounts no longer get per-instance tints, so
// the list / sidebar / dashboard read as type-grouped at a glance.
const ACCENT_BY_TYPE: Record<AccountType, string> = {
    Asset: 'var(--color-cat-transport)',
    Liability: 'var(--color-cat-shopping)',
    Equity: 'var(--color-cat-housing)',
    Income: 'var(--color-cat-entertain)',
    Expense: 'var(--color-cat-food)',
};

export function visualHintFor(accountType: AccountType): VisualHint {
    return {
        accentColor: ACCENT_BY_TYPE[accountType],
        iconName: ICON_BY_TYPE[accountType],
    };
}

// Charts need lines that read as distinct even when all accounts share an
// AccountType. Pick deterministically from a wider category palette by hashing
// the account id, independent of the type-level avatar accent.
const CHART_PALETTE: readonly string[] = [
    'var(--color-cat-transport)',
    'var(--color-cat-savings)',
    'var(--color-cat-housing)',
    'var(--color-cat-entertain)',
    'var(--color-cat-food)',
    'var(--color-cat-bills)',
    'var(--color-cat-shopping)',
];

// FNV-1a 32-bit. Deterministic and dependency-free; collisions are harmless
// beyond two trend lines sharing a colour.
function hashId(id: string): number {
    let hash = 2166136261;
    for (let i = 0; i < id.length; i++) {
        hash ^= id.charCodeAt(i);
        hash = Math.imul(hash, 16777619) >>> 0;
    }
    return hash;
}

export function chartColorFor(id: AccountId): string {
    // Palette is non-empty by construction, so the modulo lookup always hits;
    // the fallback only exists to keep noUncheckedIndexedAccess honest.
    return CHART_PALETTE[hashId(id) % CHART_PALETTE.length] ?? 'var(--color-fg-3)';
}
