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

// The curated set a user may pick a custom account icon from. This list — not the
// backend — is the source of truth for which icons exist (the API validates shape
// only), so a stored name that ever drops out of this set falls back to the type
// default below instead of breaking. Every entry must be registered in Icon.tsx.
export const ACCOUNT_ICON_CHOICES: readonly string[] = [
    // money & banking
    'wallet',
    'banknote',
    'piggy-bank',
    'landmark',
    'bank',
    'credit-card',
    'bitcoin',
    'shield',
    'trending-up',
    'trending-down',
    'repeat',
    'calendar-sync',
    // spending & daily life
    'shopping-cart',
    'shopping-bag',
    'shopping-basket',
    'sofa',
    'store',
    'utensils',
    'coffee',
    'shirt',
    'gift',
    'home',
    'zap',
    'smartphone',
    // transport & travel
    'road',
    'car',
    'motorbike',
    'fuel',
    'ev-charger',
    'bike',
    'train',
    'plane',
    'umbrella',
    'wrench',
    // people, health & leisure
    'briefcase',
    'graduation-cap',
    'heart',
    'stethoscope',
    'baby',
    'paw-print',
    'dumbbell',
    'gamepad',
    'music',
    'tv',
    'book-open',
    'leaf',
    'circle-question-mark',
    'amphora',
    'roller-coaster',
    'ferris-wheel',
    'palmtree',
    'sparkles',
];

const ACCOUNT_ICON_SET: ReadonlySet<string> = new Set(ACCOUNT_ICON_CHOICES);

// One accent per AccountType — accounts no longer get per-instance tints, so
// the list / sidebar / dashboard read as type-grouped at a glance.
const ACCENT_BY_TYPE: Record<AccountType, string> = {
    Asset: 'var(--color-cat-transport)',
    Liability: 'var(--color-cat-shopping)',
    Equity: 'var(--color-cat-housing)',
    Income: 'var(--color-cat-entertain)',
    Expense: 'var(--color-cat-food)',
};

type AccountVisual = {
    type: AccountType;
    /** The user-chosen icon name, or null to inherit the type default. */
    icon: string | null;
};

// The icon honors the user's custom choice (falling back to the type default for
// null or no-longer-offered names); the accent color is never user-chosen — it
// always derives from the AccountType so lists stay type-grouped at a glance.
export function visualHintFor(account: AccountVisual): VisualHint {
    return {
        accentColor: ACCENT_BY_TYPE[account.type],
        iconName:
            account.icon !== null && ACCOUNT_ICON_SET.has(account.icon)
                ? account.icon
                : ICON_BY_TYPE[account.type],
    };
}

// A distribution donut only ever shows one AccountType at a time, so its
// slices read best as one hue — the type accent — rather than a grab-bag of
// palette colors. Same-hue slices are then pulled apart by shade (below).
export function chartBaseColorForType(accountType: AccountType): string {
    return ACCENT_BY_TYPE[accountType];
}

// Fan successive same-hue slices toward white so a monochrome donut stays
// legible. The first (largest) slice keeps the pure accent; later slices step
// progressively lighter. oklab keeps the steps perceptually even, and color-mix
// anchors each shade to the CSS custom property instead of baking hex into JS.
export function shadeOf(baseColor: string, index: number, count: number): string {
    if (count <= 1 || index <= 0) return baseColor;
    const pct = Math.round((index / (count - 1)) * 52);
    return `color-mix(in oklab, ${baseColor}, white ${pct}%)`;
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
// beyond two trend lines sharing a color.
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
