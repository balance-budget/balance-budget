import { useMemo } from 'react';
import {
    Area,
    AreaChart,
    CartesianGrid,
    ResponsiveContainer,
    Tooltip,
    type TooltipContentProps,
    XAxis,
    YAxis,
} from 'recharts';
import { useLingui } from '@lingui/react/macro';
import { useCurrencyCatalog, type CurrencyCatalog } from '../api/currencies';
import type { NetWorthPoint } from '../api/dashboard';
import { moneyAxis } from '../lib/chartAxis';
import { formatMonthAxisDate, formatTrendTooltipDate } from '../lib/dates';
import { formatMoney, formatMoneyAxis } from '../lib/money';
import { chartColorByIndex } from '../lib/visualHints';
import { ChartTooltipShell, ChartTooltipRow, ChartTooltipTotalRow } from './ChartTooltip';

type NetWorthChartProps = {
    points: NetWorthPoint[];
    currencyCode: string;
    height?: number;
};

// The two stacked bands are a fixed, ordered pair (liquid below, illiquid
// above), so they take the first two palette slots by position.

type Row = { date: string; liquid: number; illiquid: number };

/**
 * The long-horizon dashboard chart (ADR-0030): net worth as a signed stack of its two components,
 * liquid (bottom) and illiquid (top), so the top edge is total net worth. The illiquid band is a
 * house amortizing against its mortgage; watching it grow over the years is the wealth-building
 * story. Signed stacking (stackOffset="sign") keeps a net-debt component honestly below zero.
 */
export function NetWorthChart({ points, currencyCode, height = 240 }: NetWorthChartProps) {
    const { t } = useLingui();
    const catalog = useCurrencyCatalog();

    const rows = useMemo<Row[]>(
        () =>
            points.map(p => ({
                date: p.date,
                liquid: p.liquidMinor,
                illiquid: p.netWorthMinor - p.liquidMinor,
            })),
        [points],
    );

    // Aim for roughly six date labels regardless of range length.
    const ticks = useMemo(() => {
        if (rows.length === 0) return [];
        const step = Math.max(1, Math.ceil(rows.length / 6));
        return rows.filter((_, i) => i % step === 0).map(r => r.date);
    }, [rows]);

    // Scale to the stacked totals (positives and negatives summed per month), not the individual
    // components, so the bands never overflow the axis.
    const axis = useMemo(() => {
        const sums = rows.flatMap(r => {
            const pos = Math.max(r.liquid, 0) + Math.max(r.illiquid, 0);
            const neg = Math.min(r.liquid, 0) + Math.min(r.illiquid, 0);
            return [pos, neg];
        });
        return moneyAxis(sums, { includeZero: true });
    }, [rows]);

    return (
        <ResponsiveContainer width="100%" height={height}>
            <AreaChart
                data={rows}
                margin={{ top: 10, right: 12, bottom: 0, left: 0 }}
                stackOffset="sign"
            >
                <CartesianGrid
                    stroke="var(--color-border-soft)"
                    vertical={false}
                    strokeDasharray="2 4"
                />
                <XAxis
                    dataKey="date"
                    ticks={ticks}
                    interval={0}
                    tickFormatter={formatMonthAxisDate}
                    tick={{ fill: 'var(--color-fg-3)', fontSize: 11 }}
                    axisLine={false}
                    tickLine={false}
                />
                <YAxis
                    domain={axis?.domain ?? ['auto', 'auto']}
                    ticks={axis?.ticks}
                    tickFormatter={(v: number) => formatMoneyAxis(v, currencyCode, catalog)}
                    tick={{ fill: 'var(--color-fg-3)', fontSize: 11 }}
                    axisLine={false}
                    tickLine={false}
                    width={60}
                />
                <Tooltip
                    content={
                        <NetWorthTooltip
                            currencyCode={currencyCode}
                            catalog={catalog}
                            netWorthLabel={t`Net worth`}
                            liquidLabel={t`Liquid`}
                            illiquidLabel={t`Illiquid`}
                        />
                    }
                    cursor={{
                        stroke: 'var(--color-border-strong)',
                        strokeWidth: 1,
                        strokeDasharray: '2 2',
                    }}
                />
                <Area
                    type="monotone"
                    dataKey="liquid"
                    name={t`Liquid`}
                    stackId="netWorth"
                    stroke={chartColorByIndex(0)}
                    strokeWidth={1.25}
                    fill={chartColorByIndex(0)}
                    fillOpacity={0.55}
                    isAnimationActive={false}
                />
                <Area
                    type="monotone"
                    dataKey="illiquid"
                    name={t`Illiquid`}
                    stackId="netWorth"
                    stroke={chartColorByIndex(1)}
                    strokeWidth={1.25}
                    fill={chartColorByIndex(1)}
                    fillOpacity={0.55}
                    isAnimationActive={false}
                />
            </AreaChart>
        </ResponsiveContainer>
    );
}

type NetWorthTooltipProps = Partial<TooltipContentProps<number, string>> & {
    currencyCode: string;
    catalog: CurrencyCatalog;
    netWorthLabel: string;
    liquidLabel: string;
    illiquidLabel: string;
};

function NetWorthTooltip({
    active,
    payload,
    label,
    currencyCode,
    catalog,
    netWorthLabel,
    liquidLabel,
    illiquidLabel,
}: NetWorthTooltipProps) {
    if (!active || !payload || payload.length === 0) return null;

    const row = payload[0]?.payload as Row | undefined;
    if (!row) return null;
    const netWorth = row.liquid + row.illiquid;

    return (
        <ChartTooltipShell heading={typeof label === 'string' ? formatTrendTooltipDate(label) : ''}>
            <ChartTooltipRow
                color={chartColorByIndex(0)}
                name={liquidLabel}
                value={formatMoney(row.liquid, currencyCode, catalog)}
            />
            <ChartTooltipRow
                color={chartColorByIndex(1)}
                name={illiquidLabel}
                value={formatMoney(row.illiquid, currencyCode, catalog)}
            />
            <ChartTooltipTotalRow
                name={netWorthLabel}
                value={formatMoney(netWorth, currencyCode, catalog)}
            />
        </ChartTooltipShell>
    );
}
