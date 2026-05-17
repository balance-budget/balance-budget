import type { AccountTrend } from '../lib/domain';
import {
    ACCOUNT_CASH,
    ACCOUNT_CHECKING,
    ACCOUNT_CREDIT,
    ACCOUNT_SAVINGS,
} from './accounts';

/*
 * Deterministic-ish 30-day balance trend per account, used by the Dashboard
 * trend chart. Real screens will pull this from a /api/accounts/trend
 * endpoint; here we just generate predictable demo curves.
 */
function series(startMinor: number, drift: number, vol: number): { day: number; balanceMinor: number }[] {
    const out: { day: number; balanceMinor: number }[] = [];
    let v = startMinor;
    for (let i = 0; i < 30; i++) {
        v += drift + Math.round(Math.sin(i * 0.7) * vol);
        out.push({ day: i, balanceMinor: v });
    }
    return out;
}

export const TREND: AccountTrend[] = [
    {
        accountId: ACCOUNT_CHECKING,
        name: 'Checking',
        accentColor: 'var(--color-cat-transport)',
        points: series(280_000, -800, 6_000),
    },
    {
        accountId: ACCOUNT_SAVINGS,
        name: 'Savings',
        accentColor: 'var(--color-cat-savings)',
        points: series(800_000, 1_400, 4_000),
    },
    {
        accountId: ACCOUNT_CREDIT,
        name: 'Credit card',
        accentColor: 'var(--color-cat-shopping)',
        points: series(-12_000, -200, 3_500),
    },
    {
        accountId: ACCOUNT_CASH,
        name: 'Cash',
        accentColor: 'var(--color-cat-bills)',
        points: series(18_000, -50, 800),
    },
];
