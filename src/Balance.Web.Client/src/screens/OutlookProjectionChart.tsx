import { useId, useMemo } from 'react';
import { useLingui } from '@lingui/react/macro';
import {
    Area,
    CartesianGrid,
    ComposedChart,
    Line,
    ReferenceLine,
    ResponsiveContainer,
    Tooltip,
    type TooltipContentProps,
    XAxis,
    YAxis,
} from 'recharts';
import { useCurrencyCatalog, type CurrencyCatalog } from '../api/currencies';
import type { OutlookAccountProjection } from '../api/outlook';
import { formatMonthAxisDate } from '../lib/dates';
import { formatCalendarDate } from '../i18n/format';
import { formatMoney, formatMoneyAxis } from '../lib/money';
import { moneyAxis } from '../lib/chartAxis';
import { AxisBreakMark } from '../components/AxisBreakMark';
import { ChartTooltipShell, ChartTooltipRow } from '../components/ChartTooltip';

type ChartRow = {
    month: string;
    actual?: number;
    mid?: number;
    band?: [number, number];
    scenario?: number;
    expectedIn?: number;
    expectedOut?: number;
};

/**
 * The liquid-balance Projection (ADR-0027): ledger actuals (solid) flowing into the
 * projected month-end balance — a mid line inside a Typical-spend uncertainty band — with an
 * optional what-if scenario overlaid. The actuals and baseline meet at the current balance.
 */
export function OutlookProjectionChart({
    account,
    height = 260,
}: {
    account: OutlookAccountProjection;
    height?: number;
}) {
    const { t } = useLingui();
    const catalog = useCurrencyCatalog();

    const rows = useMemo<ChartRow[]>(() => {
        const result: ChartRow[] = account.actuals.map(a => ({
            month: a.month,
            actual: a.endBalance,
        }));

        // Seed the projection at the anchor (last actual) so the baseline/scenario lines
        // and the band start from today's real balance rather than floating.
        const anchor = account.actuals.at(-1);
        const last = result.at(-1);
        if (anchor && last) {
            last.mid = anchor.endBalance;
            last.scenario = anchor.endBalance;
            last.band = [anchor.endBalance, anchor.endBalance];
        }

        account.baseline.forEach((b, i) => {
            const scenarioMonth = account.scenario?.[i];
            result.push({
                month: b.month,
                mid: b.endBalanceMid,
                band: [b.endBalanceLow, b.endBalanceHigh],
                scenario: scenarioMonth?.endBalanceMid,
                expectedIn: b.expectedIn,
                expectedOut: b.expectedOut,
            });
        });

        return result;
    }, [account]);

    // Scale to the plotted lines only — actuals and the projected mid/scenario.
    // The Typical-spend band is deliberately excluded: a wide uncertainty cone
    // (e.g. a one-off purchase blowing out the low edge) would otherwise compress
    // the whole chart into an unreadable strip. Instead we let the band run off the
    // frame and fade out near the edges (see the band gradient below).
    const axis = useMemo(
        () =>
            moneyAxis(
                rows.flatMap(r =>
                    [r.actual, r.mid, r.scenario].filter((v): v is number => v !== undefined),
                ),
            ),
        [rows],
    );

    const hasScenario = account.scenario !== null;
    // Vertical fade for the band: a soft cue that the uncertainty cone runs off the
    // frame rather than the balance flatlining at the axis edge. A vertical gradient
    // only needs y-bounds, which we derive from the chart height and the fixed top
    // margin / default X-axis band — no plot-width probing, so no friction with the
    // ResponsiveContainer. The mid 88% stays full opacity, so a normal-width band is
    // untouched; only a cone that reaches the top/bottom edge fades.
    const bandGradientId = useId();
    const plotTop = 10; // matches the chart's top margin
    const plotBottom = height - 30; // height minus Recharts' default X-axis band
    // The December row's category key, so the year-end marker lands on the right tick (absent when
    // the horizon stops before December).
    const yearEndMonth = `${account.yearEnd.date.slice(0, 7)}-01`;

    return (
        <ResponsiveContainer width="100%" height={height}>
            <ComposedChart data={rows} margin={{ top: 10, right: 12, bottom: 0, left: 0 }}>
                <defs>
                    <linearGradient
                        id={bandGradientId}
                        gradientUnits="userSpaceOnUse"
                        x1="0"
                        y1={plotTop}
                        x2="0"
                        y2={plotBottom}
                    >
                        <stop offset="0%" stopColor="var(--color-brand-primary)" stopOpacity={0} />
                        <stop
                            offset="12%"
                            stopColor="var(--color-brand-primary)"
                            stopOpacity={0.12}
                        />
                        <stop
                            offset="88%"
                            stopColor="var(--color-brand-primary)"
                            stopOpacity={0.12}
                        />
                        <stop
                            offset="100%"
                            stopColor="var(--color-brand-primary)"
                            stopOpacity={0}
                        />
                    </linearGradient>
                </defs>
                <CartesianGrid
                    stroke="var(--color-border-soft)"
                    vertical={false}
                    strokeDasharray="2 4"
                />
                <XAxis
                    dataKey="month"
                    tickFormatter={formatMonthAxisDate}
                    tick={{ fill: 'var(--color-fg-3)', fontSize: 11 }}
                    axisLine={false}
                    tickLine={false}
                    minTickGap={16}
                />
                <YAxis
                    domain={axis?.domain ?? ['auto', 'auto']}
                    ticks={axis?.ticks}
                    tickFormatter={(v: number) => formatMoneyAxis(v, account.currencyCode, catalog)}
                    tick={{ fill: 'var(--color-fg-3)', fontSize: 11 }}
                    axisLine={false}
                    tickLine={false}
                    width={60}
                />
                <ReferenceLine y={0} stroke="var(--color-danger)" strokeDasharray="3 3" />
                <ReferenceLine
                    x={yearEndMonth}
                    stroke="var(--color-border-strong)"
                    strokeDasharray="3 3"
                    label={{
                        value: t`Year-end`,
                        position: 'insideTopRight',
                        fill: 'var(--color-fg-3)',
                        fontSize: 10,
                    }}
                />
                <Tooltip
                    content={
                        <ProjectionTooltip
                            currencyCode={account.currencyCode}
                            catalog={catalog}
                            hasScenario={hasScenario}
                        />
                    }
                    cursor={{ stroke: 'var(--color-border-strong)', strokeDasharray: '2 2' }}
                />
                {axis?.truncated && <AxisBreakMark />}
                {/* The Typical-spend uncertainty band (projected months only). */}
                <Area
                    type="monotone"
                    dataKey="band"
                    stroke="none"
                    fill={`url(#${bandGradientId})`}
                    fillOpacity={1}
                    isAnimationActive={false}
                    connectNulls
                />
                {/* Ledger actuals — solid, left of today. */}
                <Line
                    type="monotone"
                    dataKey="actual"
                    name="Actual"
                    stroke="var(--color-fg-2)"
                    strokeWidth={1.25}
                    dot={false}
                    isAnimationActive={false}
                    connectNulls
                />
                {/* Projected mid — dashed, right of today. */}
                <Line
                    type="monotone"
                    dataKey="mid"
                    name="Projected"
                    stroke="var(--color-brand-primary)"
                    strokeWidth={1.25}
                    strokeDasharray="5 4"
                    dot={false}
                    isAnimationActive={false}
                    connectNulls
                />
                {hasScenario && (
                    <Line
                        type="monotone"
                        dataKey="scenario"
                        name="What-if"
                        stroke="var(--color-warning)"
                        strokeWidth={1.25}
                        strokeDasharray="2 3"
                        dot={false}
                        isAnimationActive={false}
                        connectNulls
                    />
                )}
            </ComposedChart>
        </ResponsiveContainer>
    );
}

type ProjectionTooltipProps = Partial<TooltipContentProps<number, string>> & {
    currencyCode: string;
    catalog: CurrencyCatalog;
    hasScenario: boolean;
};

function ProjectionTooltip({
    active,
    payload,
    label,
    currencyCode,
    catalog,
    hasScenario,
}: ProjectionTooltipProps) {
    const { t } = useLingui();
    if (!active || !payload || payload.length === 0) return null;

    const find = (key: string): number | undefined => {
        const entry = payload.find(p => p.dataKey === key);
        return typeof entry?.value === 'number' ? entry.value : undefined;
    };
    const band = payload.find(p => p.dataKey === 'band')?.value as [number, number] | undefined;
    // expectedIn/expectedOut aren't rendered series, so read them off the row the tooltip carries.
    const row = payload[0]?.payload as ChartRow | undefined;

    const actual = find('actual');
    const mid = find('mid');
    const scenario = find('scenario');

    return (
        <ChartTooltipShell
            heading={
                typeof label === 'string'
                    ? formatCalendarDate(label, 'year-month', { style: 'long' })
                    : ''
            }
        >
            {actual !== undefined && (
                <ChartTooltipRow
                    name={t`Actual`}
                    value={formatMoney(actual, currencyCode, catalog)}
                />
            )}
            {mid !== undefined && (
                <ChartTooltipRow
                    name={t`Projected`}
                    value={formatMoney(mid, currencyCode, catalog)}
                />
            )}
            {row?.expectedIn ? (
                <ChartTooltipRow
                    name={t`Expected in`}
                    value={formatMoney(row.expectedIn, currencyCode, catalog)}
                />
            ) : null}
            {row?.expectedOut ? (
                <ChartTooltipRow
                    name={t`Expected out`}
                    value={formatMoney(row.expectedOut, currencyCode, catalog)}
                />
            ) : null}
            {hasScenario && scenario !== undefined && (
                <ChartTooltipRow
                    name={t`What-if`}
                    value={formatMoney(scenario, currencyCode, catalog)}
                />
            )}
            {band && (
                <div className="text-fg-3 pt-0.5">
                    {formatMoney(band[0], currencyCode, catalog)} –{' '}
                    {formatMoney(band[1], currencyCode, catalog)}
                </div>
            )}
        </ChartTooltipShell>
    );
}
