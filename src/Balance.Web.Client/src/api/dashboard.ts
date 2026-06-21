import { useQuery } from '@tanstack/react-query';
import type { components } from '../lib/api-types.gen';
import { asAccountId, type AccountId, type AccountTrend, type TrendPoint } from '../lib/domain';
import { getJson } from '../lib/http';
import { toMoney, type Money } from '../lib/money';

type WireSummary = components['schemas']['DashboardSummaryOutput'];
type WireTrend = components['schemas']['AccountBalanceTrendOutput'];
type WireTrendSeries = components['schemas']['AccountTrendSeries'];
type WireTrendDelta = components['schemas']['TrendDelta'];
type WireNetWorthTrend = components['schemas']['NetWorthTrendOutput'];
type WireSpending = components['schemas']['SpendingByCategoryOutput'];

export type DashboardSummary = {
    netWorth: Money;
    liquidNetWorth: Money;
    incomeMtd: Money;
    expensesMtd: Money;
    incomeMtdPrior: Money;
    expensesMtdPrior: Money;
    periodStart: string;
    periodEnd: string;
    currencyCode: string;
};

// The UI uses short labels (1M / 3M / 6M / 1Y) while the wire carries PascalCase
// enum names (OneMonth / ...). One bidirectional map keeps both directions in
// lockstep — adding a new range only touches WIRE_BY_TOKEN.
export const TREND_RANGES = ['1M', '3M', '6M', '1Y'] as const;
export type TrendRange = (typeof TREND_RANGES)[number];

const WIRE_BY_TOKEN = {
    '1M': 'OneMonth',
    '3M': 'ThreeMonths',
    '6M': 'SixMonths',
    '1Y': 'OneYear',
} as const satisfies Record<TrendRange, WireTrend['range']>;

const TOKEN_BY_WIRE = Object.fromEntries(
    Object.entries(WIRE_BY_TOKEN).map(([token, wire]) => [wire, token]),
) as Record<WireTrend['range'], TrendRange>;

export type AccountBalanceTrend = {
    series: AccountTrend[];
    periodStart: string;
    periodEnd: string;
    range: TrendRange;
    currencyCode: string;
};

// Long-horizon net-worth chart (ADR-0030). UI tokens map to the wire enum, same
// pattern as the balance-trend ranges above.
export const NET_WORTH_RANGES = ['1Y', '3Y', 'All'] as const;
export type NetWorthRange = (typeof NET_WORTH_RANGES)[number];

const NET_WORTH_WIRE_BY_TOKEN = {
    '1Y': 'OneYear',
    '3Y': 'ThreeYears',
    All: 'All',
} as const satisfies Record<NetWorthRange, WireNetWorthTrend['range']>;

/** One month's net worth: total plus the liquid subset. The gap between them is
 *  illiquid net worth (e.g. a house amortizing against its mortgage). */
export type NetWorthPoint = { date: string; netWorthMinor: number; liquidMinor: number };

export type NetWorthTrend = {
    points: NetWorthPoint[];
    range: NetWorthRange;
    currencyCode: string;
};

/** This-month spend for one leaf Expense category; amount is a positive magnitude in minor units. */
export type SpendingCategory = { accountId: AccountId; name: string; amountMinor: number };

export type SpendingByCategory = {
    slices: SpendingCategory[];
    otherMinor: number;
    totalMinor: number;
    periodStart: string;
    periodEnd: string;
    currencyCode: string;
};

export const dashboardKeys = {
    all: ['dashboard'] as const,
    summary: () => [...dashboardKeys.all, 'summary'] as const,
    accountBalanceTrend: (range: TrendRange) =>
        [...dashboardKeys.all, 'account-balance-trend', range] as const,
    netWorthTrend: (range: NetWorthRange) =>
        [...dashboardKeys.all, 'net-worth-trend', range] as const,
    spendingByCategory: () => [...dashboardKeys.all, 'spending-by-category'] as const,
};

function fetchSummary(signal: AbortSignal): Promise<WireSummary> {
    return getJson<WireSummary>('/api/dashboard/summary', signal, 'load dashboard summary');
}

function fetchTrend(range: TrendRange, signal: AbortSignal): Promise<WireTrend> {
    return getJson<WireTrend>(
        `/api/dashboard/account-balance-trend?range=${WIRE_BY_TOKEN[range]}`,
        signal,
        'load account balance trend',
    );
}

function toSummary(wire: WireSummary): DashboardSummary {
    return {
        netWorth: toMoney(wire.netWorth, wire.currencyCode),
        liquidNetWorth: toMoney(wire.liquidNetWorth, wire.currencyCode),
        incomeMtd: toMoney(wire.incomeMtd, wire.currencyCode),
        expensesMtd: toMoney(wire.expensesMtd, wire.currencyCode),
        incomeMtdPrior: toMoney(wire.incomeMtdPrior, wire.currencyCode),
        expensesMtdPrior: toMoney(wire.expensesMtdPrior, wire.currencyCode),
        periodStart: wire.periodStart,
        periodEnd: wire.periodEnd,
        currencyCode: wire.currencyCode,
    };
}

function toMinor(raw: number | string | undefined): number {
    return typeof raw === 'string' ? Number(raw) : (raw ?? 0);
}

// Walks from periodStart to periodEnd inclusive, applying any matching delta on each
// day. The backend ships opening + sparse activity (one row per active day); the daily
// series the chart wants is reconstructed here. See AccountBalanceTrendOutput for the
// wire contract.
function expandToDailyPoints(
    opening: number,
    deltas: WireTrendDelta[],
    periodStart: string,
    periodEnd: string,
): TrendPoint[] {
    const byDate = new Map(deltas.map(d => [d.date, toMinor(d.amount)]));
    const points: TrendPoint[] = [];
    let running = opening;
    for (
        let cursor = new Date(periodStart);
        cursor <= new Date(periodEnd);
        cursor.setUTCDate(cursor.getUTCDate() + 1)
    ) {
        const date = cursor.toISOString().slice(0, 10);
        const delta = byDate.get(date);
        if (delta !== undefined) running += delta;
        points.push({ date, balanceMinor: running });
    }
    return points;
}

function toAccountTrend(
    series: WireTrendSeries,
    periodStart: string,
    periodEnd: string,
): AccountTrend {
    const accountId = asAccountId(series.accountId);
    return {
        accountId,
        name: series.accountName,
        horizon: series.horizon,
        points: expandToDailyPoints(
            toMinor(series.openingBalance),
            series.deltas,
            periodStart,
            periodEnd,
        ),
    };
}

function toTrend(wire: WireTrend): AccountBalanceTrend {
    return {
        series: wire.series.map(s => toAccountTrend(s, wire.periodStart, wire.periodEnd)),
        periodStart: wire.periodStart,
        periodEnd: wire.periodEnd,
        range: TOKEN_BY_WIRE[wire.range],
        currencyCode: wire.currencyCode,
    };
}

export function useDashboardSummary() {
    return useQuery({
        queryKey: dashboardKeys.summary(),
        queryFn: async ({ signal }) => toSummary(await fetchSummary(signal)),
    });
}

export function useAccountBalanceTrend(range: TrendRange) {
    return useQuery({
        queryKey: dashboardKeys.accountBalanceTrend(range),
        queryFn: async ({ signal }) => toTrend(await fetchTrend(range, signal)),
    });
}

function toNetWorthTrend(wire: WireNetWorthTrend): NetWorthTrend {
    return {
        points: wire.points.map(p => ({
            date: p.asOf,
            netWorthMinor: toMinor(p.netWorth),
            liquidMinor: toMinor(p.liquidNetWorth),
        })),
        range: NET_WORTH_RANGES.find(t => NET_WORTH_WIRE_BY_TOKEN[t] === wire.range) ?? '1Y',
        currencyCode: wire.currencyCode,
    };
}

export function useNetWorthTrend(range: NetWorthRange) {
    return useQuery({
        queryKey: dashboardKeys.netWorthTrend(range),
        queryFn: async ({ signal }) =>
            toNetWorthTrend(
                await getJson<WireNetWorthTrend>(
                    `/api/dashboard/net-worth-trend?range=${NET_WORTH_WIRE_BY_TOKEN[range]}`,
                    signal,
                    'load net worth trend',
                ),
            ),
    });
}

function toSpendingByCategory(wire: WireSpending): SpendingByCategory {
    return {
        slices: wire.slices.map(s => ({
            accountId: asAccountId(s.accountId),
            name: s.accountName,
            amountMinor: toMinor(s.amount),
        })),
        otherMinor: toMinor(wire.otherAmount),
        totalMinor: toMinor(wire.totalAmount),
        periodStart: wire.periodStart,
        periodEnd: wire.periodEnd,
        currencyCode: wire.currencyCode,
    };
}

export function useSpendingByCategory() {
    return useQuery({
        queryKey: dashboardKeys.spendingByCategory(),
        queryFn: async ({ signal }) =>
            toSpendingByCategory(
                await getJson<WireSpending>(
                    '/api/dashboard/spending-by-category',
                    signal,
                    'load spending by category',
                ),
            ),
    });
}
