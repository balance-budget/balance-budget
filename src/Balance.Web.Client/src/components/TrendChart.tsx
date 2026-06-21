import { useMemo } from 'react';
import {
    Area,
    AreaChart,
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
import { moneyAxis } from '../lib/chartAxis';
import { AxisBreakMark } from './AxisBreakMark';
import { buildChartColorMap, chartColorByIndex } from '../lib/visualHints';
import { ChartTooltipShell, ChartTooltipRow, ChartTooltipTotalRow } from './ChartTooltip';
import { Trans } from '@lingui/react/macro';

type TrendChartProps = {
    series: AccountTrend[];
    range: TrendRange;
    currencyCode: string;
    height?: number;
    /** Account ids whose line is currently toggled off via the legend. */
    hiddenAccountIds: Set<string>;
    /** Toggle a single series on/off; called with the clicked legend's account id. */
    onToggleSeries: (accountId: string) => void;
    /** `'line'` overlays each account; `'stacked'` stacks them as signed areas so the
     *  top edge is the tier total and overdrafts dip below zero (ADR-0030). */
    variant?: 'line' | 'stacked';
};

type ChartRow = { date: string } & Record<string, number | string>;

/** A recharts dataKey may be a string, number, or accessor function; our line
 *  dataKeys are always the account id string, so narrow to that. */
type LegendEntry = { dataKey?: string | number | ((obj: unknown) => unknown) };

function legendAccountId(entry: LegendEntry): string | null {
    const key = entry.dataKey;
    return typeof key === 'string' || typeof key === 'number' ? String(key) : null;
}

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
export function TrendChart({
    series,
    range,
    currencyCode,
    height = 240,
    hiddenAccountIds,
    onToggleSeries,
    variant = 'line',
}: TrendChartProps) {
    const catalog = useCurrencyCatalog();
    const rows = useMemo(() => buildRows(series), [series]);
    const ticks = useMemo(() => computeTicks(rows, range), [rows, range]);
    const seriesByKey = useMemo(() => new Map(series.map(s => [s.accountId, s])), [series]);
    // This chart owns its colors: one stable hue per account by the series'
    // (API) order, so each TrendChart is self-contained and always starts at the
    // first palette slot regardless of what other charts show.
    const colorByAccount = useMemo(
        () => buildChartColorMap(series.map(s => s.accountId)),
        [series],
    );
    const colorOf = (accountId: AccountId) => colorByAccount.get(accountId) ?? chartColorByIndex(0);
    // Scale to the visible series only, so toggling one off via the legend rescales the axis.
    // A stacked chart must scale to the stacked totals (positives and negatives summed per day),
    // not individual balances, or the bands overflow the axis.
    const axis = useMemo(() => {
        const visible = series.filter(s => !hiddenAccountIds.has(s.accountId));
        if (variant === 'stacked') {
            const sums = rows.flatMap(row => {
                let pos = 0;
                let neg = 0;
                for (const s of visible) {
                    const v = Number(row[s.accountId] ?? 0);
                    if (v >= 0) pos += v;
                    else neg += v;
                }
                return [pos, neg];
            });
            return moneyAxis(sums);
        }
        return moneyAxis(visible.flatMap(s => s.points.map(p => p.balanceMinor)));
    }, [series, rows, hiddenAccountIds, variant]);

    // Grid, axes, tooltip and legend are identical for both variants. Recharts traverses an
    // array of children, so share them as one keyed array rather than duplicating the JSX.
    const sharedChildren = [
        <CartesianGrid
            key="grid"
            stroke="var(--color-border-soft)"
            vertical={false}
            strokeDasharray="2 4"
        />,
        <XAxis
            key="x"
            dataKey="date"
            ticks={ticks}
            interval={0}
            tickFormatter={(d: string) => formatTrendAxisDate(d, range)}
            tick={{ fill: 'var(--color-fg-3)', fontSize: 11 }}
            axisLine={false}
            tickLine={false}
        />,
        <YAxis
            key="y"
            domain={axis?.domain ?? ['auto', 'auto']}
            ticks={axis?.ticks}
            tickFormatter={(v: number) => formatMoneyAxis(v, currencyCode, catalog)}
            tick={{ fill: 'var(--color-fg-3)', fontSize: 11 }}
            axisLine={false}
            tickLine={false}
            width={60}
        />,
        <Tooltip
            key="tip"
            content={
                <TrendTooltip
                    seriesByKey={seriesByKey}
                    currencyCode={currencyCode}
                    catalog={catalog}
                    showTotal={variant === 'stacked'}
                />
            }
            cursor={{
                stroke: 'var(--color-border-strong)',
                strokeWidth: 1,
                strokeDasharray: '2 2',
            }}
        />,
        <Legend
            key="legend"
            iconType="circle"
            iconSize={8}
            wrapperStyle={{ fontSize: 13, paddingTop: 8, cursor: 'pointer' }}
            onClick={(entry: LegendEntry) => {
                const id = legendAccountId(entry);
                if (id !== null) onToggleSeries(id);
            }}
            formatter={(value: string, entry: LegendEntry) => {
                const id = legendAccountId(entry);
                const off = id !== null && hiddenAccountIds.has(id);
                return (
                    <span
                        style={{
                            color: off ? 'var(--color-fg-3)' : undefined,
                            textDecoration: off ? 'line-through' : undefined,
                        }}
                    >
                        {value}
                    </span>
                );
            }}
        />,
        axis?.truncated ? <AxisBreakMark key="break" /> : null,
    ];

    const margin = { top: 10, right: 12, bottom: 0, left: 0 };

    return (
        <ResponsiveContainer width="100%" height={height}>
            {variant === 'stacked' ? (
                <AreaChart data={rows} margin={margin} stackOffset="sign">
                    {sharedChildren}
                    {series.map(s => (
                        <Area
                            key={s.accountId}
                            type="monotone"
                            dataKey={s.accountId}
                            name={s.name}
                            stackId="balance"
                            stroke={colorOf(s.accountId)}
                            strokeWidth={1.25}
                            fill={colorOf(s.accountId)}
                            fillOpacity={0.55}
                            isAnimationActive={false}
                            hide={hiddenAccountIds.has(s.accountId)}
                        />
                    ))}
                </AreaChart>
            ) : (
                <LineChart data={rows} margin={margin}>
                    {sharedChildren}
                    {series.map(s => (
                        <Line
                            key={s.accountId}
                            type="monotone"
                            dataKey={s.accountId}
                            name={s.name}
                            stroke={colorOf(s.accountId)}
                            strokeWidth={1.75}
                            dot={false}
                            activeDot={{ r: 3, strokeWidth: 0 }}
                            isAnimationActive={false}
                            hide={hiddenAccountIds.has(s.accountId)}
                        />
                    ))}
                </LineChart>
            )}
        </ResponsiveContainer>
    );
}

type TrendTooltipProps = Partial<TooltipContentProps<number, string>> & {
    seriesByKey: Map<AccountId, AccountTrend>;
    currencyCode: string;
    catalog: CurrencyCatalog;
    /** Stacked variant only: the top edge is the tier total, so show it. */
    showTotal: boolean;
};

function TrendTooltip({
    active,
    payload,
    label,
    seriesByKey,
    currencyCode,
    catalog,
    showTotal,
}: TrendTooltipProps) {
    if (!active || !payload || payload.length === 0) return null;

    const sorted = [...payload].sort((a, b) => (Number(b.value) || 0) - (Number(a.value) || 0));
    const total = sorted.reduce((sum, item) => sum + (Number(item.value) || 0), 0);

    return (
        <ChartTooltipShell heading={typeof label === 'string' ? formatTrendTooltipDate(label) : ''}>
            {sorted.map(item => {
                const series = seriesByKey.get(asAccountId(String(item.dataKey)));
                return (
                    <ChartTooltipRow
                        key={String(item.dataKey)}
                        color={item.color}
                        name={series?.name ?? String(item.name ?? '')}
                        value={formatMoney(Number(item.value) || 0, currencyCode, catalog)}
                    />
                );
            })}
            {showTotal && sorted.length > 1 && (
                <ChartTooltipTotalRow
                    name={<Trans>Total</Trans>}
                    value={formatMoney(total, currencyCode, catalog)}
                />
            )}
        </ChartTooltipShell>
    );
}
