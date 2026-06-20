import { useMemo } from 'react';
import {
    CartesianGrid,
    Line,
    LineChart,
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

type NetWorthChartProps = {
    points: NetWorthPoint[];
    currencyCode: string;
    height?: number;
};

const NET_WORTH_COLOR = 'var(--color-chart-blue)';
const LIQUID_COLOR = 'var(--color-chart-teal)';

type Row = { date: string; netWorth: number; liquid: number };

/**
 * The long-horizon dashboard chart (ADR-0030): total net worth and the liquid subset as two
 * monthly lines. The gap between them is illiquid net worth — a house amortizing against its
 * mortgage — so the two lines diverging over the years is the wealth-building story.
 */
export function NetWorthChart({ points, currencyCode, height = 240 }: NetWorthChartProps) {
    const { t } = useLingui();
    const catalog = useCurrencyCatalog();

    const rows = useMemo<Row[]>(
        () =>
            points.map(p => ({
                date: p.date,
                netWorth: p.netWorthMinor,
                liquid: p.liquidMinor,
            })),
        [points],
    );

    // Aim for roughly six date labels regardless of range length.
    const ticks = useMemo(() => {
        if (rows.length === 0) return [];
        const step = Math.max(1, Math.ceil(rows.length / 6));
        return rows.filter((_, i) => i % step === 0).map(r => r.date);
    }, [rows]);

    const axis = useMemo(
        () =>
            moneyAxis(
                rows.flatMap(r => [r.netWorth, r.liquid]),
                { includeZero: true },
            ),
        [rows],
    );

    return (
        <ResponsiveContainer width="100%" height={height}>
            <LineChart data={rows} margin={{ top: 10, right: 12, bottom: 0, left: 0 }}>
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
                <Line
                    type="monotone"
                    dataKey="netWorth"
                    name={t`Net worth`}
                    stroke={NET_WORTH_COLOR}
                    strokeWidth={1.75}
                    dot={false}
                    activeDot={{ r: 3, strokeWidth: 0 }}
                    isAnimationActive={false}
                />
                <Line
                    type="monotone"
                    dataKey="liquid"
                    name={t`Liquid`}
                    stroke={LIQUID_COLOR}
                    strokeWidth={1.75}
                    dot={false}
                    activeDot={{ r: 3, strokeWidth: 0 }}
                    isAnimationActive={false}
                />
            </LineChart>
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
    const illiquid = row.netWorth - row.liquid;

    const lines: { key: string; name: string; value: number; color: string }[] = [
        { key: 'net', name: netWorthLabel, value: row.netWorth, color: NET_WORTH_COLOR },
        { key: 'liquid', name: liquidLabel, value: row.liquid, color: LIQUID_COLOR },
        { key: 'illiquid', name: illiquidLabel, value: illiquid, color: 'var(--color-fg-3)' },
    ];

    return (
        <div className="rounded-xl border border-border-soft bg-bg-1 px-3 py-2 shadow-sm text-xs">
            <div className="text-fg-3 mb-1">
                {typeof label === 'string' ? formatTrendTooltipDate(label) : ''}
            </div>
            <div className="flex flex-col gap-1">
                {lines.map(l => (
                    <div key={l.key} className="flex items-center justify-between gap-x-4">
                        <span className="flex items-center gap-1.5">
                            <span
                                className="w-2 h-2 rounded-full inline-block"
                                style={{ background: l.color }}
                            />
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
