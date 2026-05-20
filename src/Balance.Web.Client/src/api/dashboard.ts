import { useQuery } from '@tanstack/react-query';
import type { components } from '../lib/api-types';
import { asAccountId, type AccountTrend } from '../lib/domain';
import { visualHintFor } from '../lib/visualHints';
import type { Money } from './accounts';

type WireSummary = components['schemas']['DashboardSummaryOutput'];
type WireMoney = components['schemas']['Money'];
type WireTrend = components['schemas']['AccountBalanceTrendOutput'];
type WireTrendSeries = components['schemas']['AccountTrendSeries'];

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
        `/api/dashboard/account-balance-trend?range=${range}`,
        { signal },
    );
    if (!response.ok) {
        throw new Error(`Failed to load account balance trend (${response.status})`);
    }
    return (await response.json()) as WireTrend;
}

function toMoney(wire: WireMoney, fallbackCurrencyCode: string): Money {
    const raw = wire.amount;
    const amount = typeof raw === 'string' ? Number(raw) : (raw ?? 0);
    return {
        amount,
        currencyCode: wire.currencyCode ?? fallbackCurrencyCode,
    };
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

function toAccountTrend(series: WireTrendSeries): AccountTrend {
    const accountId = asAccountId(series.accountId);
    // Asset is the only AccountType this projection emits — visualHintFor's palette
    // for Asset is what we want for every trend line. Keeping the lookup here (vs.
    // wiring colour on the server) keeps visual hints a frontend concern.
    const visual = visualHintFor('Asset', accountId);
    return {
        accountId,
        name: series.accountName,
        accentColor: visual.accentColor,
        points: series.points.map(point => ({
            date: point.date,
            balanceMinor:
                typeof point.balance.amount === 'string'
                    ? Number(point.balance.amount)
                    : (point.balance.amount ?? 0),
        })),
    };
}

function toTrend(wire: WireTrend): AccountBalanceTrend {
    return {
        series: wire.series.map(toAccountTrend),
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
