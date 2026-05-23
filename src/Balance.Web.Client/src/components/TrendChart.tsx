import { useMemo } from 'react';
import {
    CartesianGrid,
    Legend,
    Line,
    LineChart,
    ResponsiveContainer,
    Tooltip,
    type TooltipContentProps,
    XAxis,
    YAxis,
} from 'recharts';
import { useCurrencyCatalog, type CurrencyCatalog } from '../api/currencies';
import type { TrendRange } from '../api/dashboard';
import { formatTrendAxisDate, formatTrendTooltipDate } from '../lib/dates';
import { asAccountId, type AccountId, type AccountTrend } from '../lib/domain';
import { formatMoney, formatMoneyAxis } from '../lib/money';

type TrendChartProps = {
    series: AccountTrend[];
    range: TrendRange;
    currencyCode: string;
    height?: number;
};

type ChartRow = { date: string } & Record<string, number | string>;

function buildRows(series: AccountTrend[]): ChartRow[] {
    const first = series[0];
    if (!first) return [];
    return first.points.map((firstPoint, i) => {
        const row: ChartRow = { date: firstPoint.date };
        for (const s of series) {
            // All series are aligned to the same date axis by the backend, so
            // s.points[i] is guaranteed to exist alongside first.points[i].
            const point = s.points[i];
            if (point) row[s.accountId] = point.balanceMinor;
        }
        return row;
    });
}

function computeTicks(rows: ChartRow[], range: TrendRange): string[] {
    if (rows.length === 0) return [];
    if (range === '1M') {
        // Weekly cadence; day-of-month carries information at this scale.
        return rows.filter((_, i) => i % 7 === 0).map(r => r.date);
    }
    // Monthly cadence at the 1st of the month — the day-of-month is noise
    // for 3M / 6M / 1Y, so anchor to month boundaries instead.
    return rows.filter(r => r.date.endsWith('-01')).map(r => r.date);
}

/**
 * Multi-account balance trend rendered by Recharts. Each series is one Asset
 * Account; the chart shows a unified crosshair tooltip with all balances at
 * the snapped date, sorted value-descending. Axes auto-fit; y-axis labels are
 * compact above €10k, full below.
 */
export function TrendChart({ series, range, currencyCode, height = 240 }: TrendChartProps) {
    const catalog = useCurrencyCatalog();
    const rows = useMemo(() => buildRows(series), [series]);
    const ticks = useMemo(() => computeTicks(rows, range), [rows, range]);
    const seriesByKey = useMemo(() => new Map(series.map(s => [s.accountId, s])), [series]);

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
                    tickFormatter={(d: string) => formatTrendAxisDate(d, range)}
                    tick={{ fill: 'var(--color-fg-3)', fontSize: 11 }}
                    axisLine={false}
                    tickLine={false}
                />
                <YAxis
                    domain={['auto', 'auto']}
                    tickFormatter={(v: number) => formatMoneyAxis(v, currencyCode, catalog)}
                    tick={{ fill: 'var(--color-fg-3)', fontSize: 11 }}
                    axisLine={false}
                    tickLine={false}
                    width={60}
                />
                <Tooltip
                    content={
                        <TrendTooltip
                            seriesByKey={seriesByKey}
                            currencyCode={currencyCode}
                            catalog={catalog}
                        />
                    }
                    cursor={{
                        stroke: 'var(--color-border-strong)',
                        strokeWidth: 1,
                        strokeDasharray: '2 2',
                    }}
                />
                <Legend
                    iconType="circle"
                    iconSize={8}
                    wrapperStyle={{ fontSize: 13, paddingTop: 8 }}
                />
                {series.map(s => (
                    <Line
                        key={s.accountId}
                        type="monotone"
                        dataKey={s.accountId}
                        name={s.name}
                        stroke={s.accentColor}
                        strokeWidth={1.75}
                        dot={false}
                        activeDot={{ r: 3, strokeWidth: 0 }}
                        isAnimationActive={false}
                    />
                ))}
            </LineChart>
        </ResponsiveContainer>
    );
}

type TrendTooltipProps = Partial<TooltipContentProps<number, string>> & {
    seriesByKey: Map<AccountId, AccountTrend>;
    currencyCode: string;
    catalog: CurrencyCatalog;
};

function TrendTooltip({
    active,
    payload,
    label,
    seriesByKey,
    currencyCode,
    catalog,
}: TrendTooltipProps) {
    if (!active || !payload || payload.length === 0) return null;

    const sorted = [...payload].sort((a, b) => (Number(b.value) || 0) - (Number(a.value) || 0));

    return (
        <div className="rounded-md border border-border-soft bg-bg-1 px-3 py-2 shadow-sm text-[12px]">
            <div className="text-fg-3 mb-1">
                {typeof label === 'string' ? formatTrendTooltipDate(label) : ''}
            </div>
            <div className="flex flex-col gap-1">
                {sorted.map(item => {
                    const series = seriesByKey.get(asAccountId(String(item.dataKey)));
                    const value = Number(item.value) || 0;
                    return (
                        <div
                            key={String(item.dataKey)}
                            className="flex items-center justify-between gap-x-4"
                        >
                            <span className="flex items-center gap-1.5">
                                <span
                                    className="w-2 h-2 rounded-full inline-block"
                                    style={{ background: item.color }}
                                />
                                <span className="text-fg-2">
                                    {series?.name ?? String(item.name ?? '')}
                                </span>
                            </span>
                            <span className="font-mono tabular text-fg-1">
                                {formatMoney(value, currencyCode, catalog)}
                            </span>
                        </div>
                    );
                })}
            </div>
        </div>
    );
}
