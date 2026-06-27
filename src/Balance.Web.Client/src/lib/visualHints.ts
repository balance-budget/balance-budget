import type { IconName } from '../components/Icon';
import type { AccountType } from './domain';

export type VisualHint = {
    accentColor: string;
    iconName: IconName;
};

const ICON_BY_TYPE: Record<AccountType, IconName> = {
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
export const ACCOUNT_ICON_CHOICES: readonly IconName[] = [
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

const ACCOUNT_ICON_SET: ReadonlySet<IconName> = new Set(ACCOUNT_ICON_CHOICES);

// Narrows an arbitrary (e.g. persisted/API) string to a curated IconName so the
// fall-through to the type default below is type-checked, not just runtime-safe.
function isAccountIcon(name: string): name is IconName {
    return ACCOUNT_ICON_SET.has(name as IconName);
}

// One accent per AccountType — accounts no longer get per-instance tints, so
// the list / sidebar / dashboard read as type-grouped at a glance. This is the
// single source of truth for the AccountType→hue mapping; chart components
// reuse it (e.g. MoneyFlowChart) rather than re-declaring their own. These use
// the dedicated --color-type-* tokens (not the --color-chart-* palette) so the
// per-type "theme" colors stay stable even when the chart hues are retuned.
export const ACCENT_BY_TYPE: Record<AccountType, string> = {
    Asset: 'var(--color-type-asset)',
    Liability: 'var(--color-type-liability)',
    Equity: 'var(--color-type-equity)',
    Income: 'var(--color-type-income)',
    Expense: 'var(--color-type-expense)',
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
            account.icon !== null && isAccountIcon(account.icon)
                ? account.icon
                : ICON_BY_TYPE[account.type],
    };
}

// A wide palette of distinct hues for charts that show several series or slices
// at once. Colors are assigned by *position*, never by a hash, so a chart's
// colors are deterministic and stable across renders (same data → same colors),
// and adjacent series always differ. The order here is the order they appear.
const CHART_PALETTE: readonly string[] = [
    'var(--color-chart-1)',
    'var(--color-chart-2)',
    'var(--color-chart-3)',
    'var(--color-chart-4)',
    'var(--color-chart-5)',
    'var(--color-chart-6)',
    'var(--color-chart-7)',
    'var(--color-chart-8)',
];

// The i-th series/slice gets the i-th palette hue, wrapping once exhausted.
export function chartColorByIndex(index: number): string {
    // Palette is non-empty by construction, so the modulo lookup always hits;
    // the fallback only exists to keep noUncheckedIndexedAccess honest.
    return CHART_PALETTE[index % CHART_PALETTE.length] ?? 'var(--color-fg-3)';
}

// Stable, ordered color assignment keyed by id: each distinct id takes the next
// palette hue in first-seen order, and repeated ids reuse their hue. Pass the
// ids in the series' natural (API) order so colors stay put between renders.
export function buildChartColorMap(ids: readonly string[]): Map<string, string> {
    const map = new Map<string, string>();
    for (const id of ids) {
        if (!map.has(id)) map.set(id, chartColorByIndex(map.size));
    }
    return map;
}
