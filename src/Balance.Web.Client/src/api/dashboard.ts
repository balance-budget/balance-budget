import { useQuery } from '@tanstack/react-query';
import type { components } from '../lib/api-types';
import { asAccountId, type AccountTrend, type TrendPoint } from '../lib/domain';
import { toMoney, type Money } from '../lib/money';
import { visualHintFor } from '../lib/visualHints';

type WireSummary = components['schemas']['DashboardSummaryOutput'];
type WireTrend = components['schemas']['AccountBalanceTrendOutput'];
type WireTrendSeries = components['schemas']['AccountTrendSeries'];
type WireTrendDelta = components['schemas']['TrendDelta'];

export type DashboardSummary = {
    netWorth: Money;
    incomeMtd: Money;
    expensesMtd: Money;
    incomeMtdPrior: Money;
    expensesMtdPrior: Money;
    periodStart: string;
    periodEnd: string;
    currencyCode: string;
};

// Wire enum values are PascalCase (OneMonth / ThreeMonths / ...); the URL token
// is a short label (1M / 3M / 6M / 1Y). This module exposes the short form.
export const TREND_RANGES = ['1M', '3M', '6M', '1Y'] as const;
export type TrendRange = (typeof TREND_RANGES)[number];

export type AccountBalanceTrend = {
    series: AccountTrend[];
    periodStart: string;
    periodEnd: string;
    range: TrendRange;
    currencyCode: string;
};

export const dashboardKeys = {
    all: ['dashboard'] as const,
    summary: () => [...dashboardKeys.all, 'summary'] as const,
    accountBalanceTrend: (range: TrendRange) =>
        [...dashboardKeys.all, 'account-balance-trend', range] as const,
};

async function fetchSummary(signal: AbortSignal): Promise<WireSummary> {
    const response = await fetch('/api/dashboard/summary', { signal });
    if (!response.ok) {
        throw new Error(`Failed to load dashboard summary (${response.status})`);
    }
    return (await response.json()) as WireSummary;
}

async function fetchTrend(range: TrendRange, signal: AbortSignal): Promise<WireTrend> {
    const response = await fetch(
        `/api/dashboard/account-balance-trend?range=${tokenToWireRange(range)}`,
        { signal },
    );
    if (!response.ok) {
        throw new Error(`Failed to load account balance trend (${response.status})`);
    }
    return (await response.json()) as WireTrend;
}

function toSummary(wire: WireSummary): DashboardSummary {
    return {
        netWorth: toMoney(wire.netWorth, wire.currencyCode),
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
    // Asset is the only AccountType this projection emits — visualHintFor's palette
    // for Asset is what we want for every trend line. Keeping the lookup here (vs.
    // wiring colour on the server) keeps visual hints a frontend concern.
    const visual = visualHintFor('Asset', accountId);
    return {
        accountId,
        name: series.accountName,
        accentColor: visual.accentColor,
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
        series: wire.series.map(s =>
            toAccountTrend(s, wire.periodStart, wire.periodEnd),
        ),
        periodStart: wire.periodStart,
        periodEnd: wire.periodEnd,
        range: wireRangeToToken(wire.range),
        currencyCode: wire.currencyCode,
    };
}

function wireRangeToToken(range: WireTrend['range']): TrendRange {
    switch (range) {
        case 'OneMonth':
            return '1M';
        case 'ThreeMonths':
            return '3M';
        case 'SixMonths':
            return '6M';
        case 'OneYear':
            return '1Y';
    }
}

function tokenToWireRange(range: TrendRange): WireTrend['range'] {
    switch (range) {
        case '1M':
            return 'OneMonth';
        case '3M':
            return 'ThreeMonths';
        case '6M':
            return 'SixMonths';
        case '1Y':
            return 'OneYear';
    }
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
