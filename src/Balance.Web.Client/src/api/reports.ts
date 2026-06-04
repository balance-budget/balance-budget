import { keepPreviousData, useQuery } from '@tanstack/react-query';
import type { components } from '../lib/api-types';
import { asAccountId, type AccountId } from '../lib/domain';
import { getJson } from '../lib/http';
import type { ReportPeriod } from '../lib/reportPeriod';
import { toMoney, type Money } from '../lib/money';

type WireDistribution = components['schemas']['DistributionOutput'];
type WireDistributionSlice = components['schemas']['DistributionSlice'];
type WireFlow = components['schemas']['MoneyFlowOutput'];
type WireFlowNode = components['schemas']['MoneyFlowNode'];
type WireFlowLink = components['schemas']['MoneyFlowLink'];

// UI uses lowercase tokens; the wire carries PascalCase enum names.
export type DistributionType = 'income' | 'expense';
const WIRE_TYPE: Record<DistributionType, WireDistribution['type']> = {
    income: 'Income',
    expense: 'Expense',
};

export type DistributionSlice = {
    accountId: AccountId;
    name: string;
    code: string;
    amount: Money;
    hasChildren: boolean;
};

export type Distribution = {
    type: DistributionType;
    parentAccountId: AccountId | null;
    total: Money;
    slices: DistributionSlice[];
    currencyCode: string;
};

export type MoneyFlowNodeKind = WireFlowNode['kind'];
export type MoneyFlowNode = {
    id: string;
    name: string;
    kind: MoneyFlowNodeKind;
    // The account's parent id, or null for roots/the hub. Lets the chart walk ancestry to prune a
    // node's descendants from the expanded set when it collapses.
    parentId: string | null;
    // True only when expanding this node would reveal a child that moved this period — the chart
    // shows an expand affordance only where it's true.
    hasChildren: boolean;
};
export type MoneyFlowLink = { source: string; target: string; value: Money };
export type MoneyFlow = {
    nodes: MoneyFlowNode[];
    links: MoneyFlowLink[];
    currencyCode: string;
};

export const HUB_NODE_ID = 'hub';

export const reportKeys = {
    all: ['reports'] as const,
    distribution: (
        type: DistributionType,
        period: ReportPeriod,
        currency: string,
        parentId: string | null,
    ) =>
        [
            ...reportKeys.all,
            'distribution',
            type,
            period.from,
            period.to,
            currency,
            parentId,
        ] as const,
    flow: (period: ReportPeriod, currency: string, expanded: readonly string[]) =>
        // Sort so the key is stable regardless of expand order; collapsing back to a prior set is a
        // cache hit.
        [
            ...reportKeys.all,
            'flow',
            period.from,
            period.to,
            currency,
            [...expanded].sort().join(','),
        ] as const,
};

function toSlice(wire: WireDistributionSlice, currency: string): DistributionSlice {
    return {
        accountId: asAccountId(wire.accountId),
        name: wire.name,
        code: wire.code,
        amount: toMoney(wire.amount, currency),
        hasChildren: wire.hasChildren,
    };
}

function toDistribution(wire: WireDistribution): Distribution {
    return {
        type: wire.type === 'Income' ? 'income' : 'expense',
        parentAccountId: wire.parentAccountId === null ? null : asAccountId(wire.parentAccountId),
        total: toMoney(wire.total, wire.currencyCode),
        slices: wire.slices.map(s => toSlice(s, wire.currencyCode)),
        currencyCode: wire.currencyCode,
    };
}

function toMoneyFlow(wire: WireFlow): MoneyFlow {
    return {
        nodes: wire.nodes.map((n: WireFlowNode) => ({
            id: n.id,
            name: n.name,
            kind: n.kind,
            parentId: n.parentId,
            hasChildren: n.hasChildren,
        })),
        links: wire.links.map((l: WireFlowLink) => ({
            source: l.source,
            target: l.target,
            value: toMoney(l.value, wire.currencyCode),
        })),
        currencyCode: wire.currencyCode,
    };
}

export function useDistribution(
    type: DistributionType,
    period: ReportPeriod,
    currency: string,
    parentId: AccountId | null,
) {
    return useQuery({
        queryKey: reportKeys.distribution(type, period, currency, parentId),
        queryFn: async ({ signal }) => {
            const params = new URLSearchParams({
                type: WIRE_TYPE[type],
                from: period.from,
                to: period.to,
                currency,
            });
            if (parentId !== null) params.set('parentId', parentId);
            const wire = await getJson<WireDistribution>(
                `/api/reports/distribution?${params}`,
                signal,
                'load distribution',
            );
            return toDistribution(wire);
        },
    });
}

export function useMoneyFlow(period: ReportPeriod, currency: string, expanded: readonly string[]) {
    return useQuery({
        queryKey: reportKeys.flow(period, currency, expanded),
        queryFn: async ({ signal }) => {
            const params = new URLSearchParams({
                from: period.from,
                to: period.to,
                currency,
            });
            // Each expanded account id opts that node into showing its children; an empty set draws
            // roots only.
            for (const id of expanded) params.append('expanded', id);
            const wire = await getJson<WireFlow>(
                `/api/reports/flow?${params}`,
                signal,
                'load money flow',
            );
            return toMoneyFlow(wire);
        },
        // Keep the previous diagram on screen while a fresh expansion loads, so expanding feels
        // in-place rather than a reload; collapsing back to a cached set is instant.
        placeholderData: keepPreviousData,
    });
}
