import { asAccountId, type AccountTrend } from '../lib/domain';

/*
 * Deterministic-ish 30-day balance trend per account, used by the Dashboard
 * trend chart. Decoupled from real account IDs — the trend stays demo-only
 * until the per-account running-balance projection lands (later slice of PRD #37).
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
        accountId: asAccountId('demo-trend-checking'),
        name: 'Checking',
        accentColor: 'var(--color-cat-transport)',
        points: series(280_000, -800, 6_000),
    },
    {
        accountId: asAccountId('demo-trend-savings'),
        name: 'Savings',
        accentColor: 'var(--color-cat-savings)',
        points: series(800_000, 1_400, 4_000),
    },
    {
        accountId: asAccountId('demo-trend-credit'),
        name: 'Credit card',
        accentColor: 'var(--color-cat-shopping)',
        points: series(-12_000, -200, 3_500),
    },
    {
        accountId: asAccountId('demo-trend-cash'),
        name: 'Cash',
        accentColor: 'var(--color-cat-bills)',
        points: series(18_000, -50, 800),
    },
];
