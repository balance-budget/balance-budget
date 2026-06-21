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

type NetWorthChartProps = {
    points: NetWorthPoint[];
    currencyCode: string;
    height?: number;
};

// The two stacked bands are a fixed, ordered pair (liquid below, illiquid
// above), so they take the first two palette slots by position.
const LIQUID_COLOR = chartColorByIndex(0);
const ILLIQUID_COLOR = chartColorByIndex(1);

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
                    stroke={LIQUID_COLOR}
                    strokeWidth={1.25}
                    fill={LIQUID_COLOR}
                    fillOpacity={0.55}
                    isAnimationActive={false}
                />
                <Area
                    type="monotone"
                    dataKey="illiquid"
                    name={t`Illiquid`}
                    stackId="netWorth"
                    stroke={ILLIQUID_COLOR}
                    strokeWidth={1.25}
                    fill={ILLIQUID_COLOR}
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

    // Components first, then the total they sum to.
    const lines: { key: string; name: string; value: number; color: string; strong?: boolean }[] = [
        { key: 'liquid', name: liquidLabel, value: row.liquid, color: LIQUID_COLOR },
        { key: 'illiquid', name: illiquidLabel, value: row.illiquid, color: ILLIQUID_COLOR },
        {
            key: 'net',
            name: netWorthLabel,
            value: netWorth,
            color: 'var(--color-fg-3)',
            strong: true,
        },
    ];

    return (
        <div className="rounded-xl border border-border-soft bg-bg-1 px-3 py-2 shadow-sm text-xs">
            <div className="text-fg-3 mb-1">
                {typeof label === 'string' ? formatTrendTooltipDate(label) : ''}
            </div>
            <div className="flex flex-col gap-1">
                {lines.map(l => (
                    <div
                        key={l.key}
                        className={
                            l.strong
                                ? 'flex items-center justify-between gap-x-4 mt-1 pt-1 border-t border-border-soft'
                                : 'flex items-center justify-between gap-x-4'
                        }
                    >
                        <span className="flex items-center gap-1.5">
                            {l.strong ? (
                                <span className="w-2 h-2" />
                            ) : (
                                <span
                                    className="w-2 h-2 rounded-full inline-block"
                                    style={{ background: l.color }}
                                />
                            )}
                            <span className="text-fg-2">{l.name}</span>
                        </span>
                        <span className="font-mono tabular-nums text-fg-1">
                            {formatMoney(l.value, currencyCode, catalog)}
                        </span>
                    </div>
                ))}
            </div>
        </div>
    );
}
