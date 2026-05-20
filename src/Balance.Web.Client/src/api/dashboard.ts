import { useQuery } from '@tanstack/react-query';
import type { components } from '../lib/api-types';
import type { Money } from './accounts';

type WireSummary = components['schemas']['DashboardSummaryOutput'];
type WireMoney = components['schemas']['Money'];

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

export const dashboardKeys = {
    all: ['dashboard'] as const,
    summary: () => [...dashboardKeys.all, 'summary'] as const,
};

async function fetchSummary(signal: AbortSignal): Promise<WireSummary> {
    const response = await fetch('/api/dashboard/summary', { signal });
    if (!response.ok) {
        throw new Error(`Failed to load dashboard summary (${response.status})`);
    }
    return (await response.json()) as WireSummary;
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

export function useDashboardSummary() {
    return useQuery({
        queryKey: dashboardKeys.summary(),
        queryFn: async ({ signal }) => toSummary(await fetchSummary(signal)),
    });
}
