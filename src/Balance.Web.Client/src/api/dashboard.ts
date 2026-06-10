import { useQuery } from '@tanstack/react-query';
import type { components } from '../lib/api-types.gen';
import {
    asAccountId,
    asJournalLineId,
    type AccountId,
    type AccountTrend,
    type JournalLineId,
    type TrendPoint,
} from '../lib/domain';
import { getJson } from '../lib/http';
import { toMoney, type Money } from '../lib/money';
import { chartColorFor } from '../lib/visualHints';

type WireSummary = components['schemas']['DashboardSummaryOutput'];
type WireTrend = components['schemas']['AccountBalanceTrendOutput'];
type WireTrendSeries = components['schemas']['AccountTrendSeries'];
type WireTrendDelta = components['schemas']['TrendDelta'];
type WireRegisterPreviews = components['schemas']['DashboardRegisterPreviewOutput'];

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

/** One row of a Register preview on a dashboard account card; the amount is
 *  already normalized to the account's normal balance, like the Register. */
export type RegisterPreviewRow = {
    journalLineId: JournalLineId;
    date: string;
    entryDescription: string | null;
    lineDescription: string | null;
    counterpartyName: string | null;
    amount: Money;
};

/** Register previews keyed by postable account; accounts without activity are absent. */
export type RegisterPreviews = ReadonlyMap<AccountId, RegisterPreviewRow[]>;

export const dashboardKeys = {
    all: ['dashboard'] as const,
    summary: () => [...dashboardKeys.all, 'summary'] as const,
    accountBalanceTrend: (range: TrendRange) =>
        [...dashboardKeys.all, 'account-balance-trend', range] as const,
    registerPreviews: () => [...dashboardKeys.all, 'register-previews'] as const,
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
        accentColor: chartColorFor(accountId),
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

function toRegisterPreviews(wire: WireRegisterPreviews): RegisterPreviews {
    return new Map(
        wire.accounts.map(a => [
            asAccountId(a.accountId),
            a.rows.map(r => ({
                journalLineId: asJournalLineId(r.journalLineId),
                date: r.date,
                entryDescription: r.entryDescription,
                lineDescription: r.lineDescription,
                counterpartyName: r.counterpartyName,
                amount: toMoney(r.amount),
            })),
        ]),
    );
}

/** Every account card's Register preview in one request: the per-account fan-out
 *  the dashboard used to do (one register call per account) piles up on
 *  resource-constrained hosts. */
export function useDashboardRegisterPreviews() {
    return useQuery({
        queryKey: dashboardKeys.registerPreviews(),
        queryFn: async ({ signal }) =>
            toRegisterPreviews(
                await getJson<WireRegisterPreviews>(
                    '/api/dashboard/register-previews',
                    signal,
                    'load register previews',
                ),
            ),
    });
}
