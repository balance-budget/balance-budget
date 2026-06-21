import { useMemo, useState } from 'react';
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
import { t } from '@lingui/core/macro';
import { Trans, useLingui } from '@lingui/react/macro';
import type { LoanProjection } from '../api/loans';
import { useCurrencyCatalog, type CurrencyCatalog } from '../api/currencies';
import { formatCalendarDate } from '../i18n/format';
import { cx } from '../lib/cx';
import { formatMoney, formatMoneyAxis } from '../lib/money';
import { moneyAxis } from '../lib/chartAxis';
import { ChartTooltipShell, ChartTooltipRow, ChartTooltipTotalRow } from './ChartTooltip';
import { buildChartColorMap, chartColorByIndex } from '../lib/visualHints';
import { buildChartRows, buildPaymentRows } from '../screens/loanDetail.state';

type LoanChartProps = {
    projection: LoanProjection;
    height?: number;
};

type FlatRow = { period: string } & Record<string, number | string | null>;

type ChartMode = 'balance' | 'payments';

/**
 * Two views of the loan over one month axis (toggleable):
 *  - "Balance" — outstanding balance stacked by part, posted actuals left of
 *    today (solid) and the engine projection right of today (faded), with the
 *    scenario total overlaid when the simulator is active.
 *  - "Payments" — the monthly payment composition stacked by part, each part's
 *    repayment and interest in the same hue at two shades (ADR-0026).
 * Rate-fixation boundaries and the "today" marker show in both (ADR-0025).
 */
export function LoanChart({ projection, height = 280 }: LoanChartProps) {
    const { t } = useLingui();
    const catalog = useCurrencyCatalog();
    const [mode, setMode] = useState<ChartMode>('balance');

    const { balanceRows, hasScenario } = useMemo(() => {
        const chartRows = buildChartRows(projection);
        const flat: FlatRow[] = chartRows.map(r => {
            const row: FlatRow = { period: r.period, scenarioTotal: r.scenarioTotal };
            for (const [partId, balance] of Object.entries(r.actual)) row[`a:${partId}`] = balance;
            for (const [partId, balance] of Object.entries(r.proj)) row[`p:${partId}`] = balance;
            return row;
        });
        return { balanceRows: flat, hasScenario: chartRows.some(r => r.scenarioTotal !== null) };
    }, [projection]);

    const paymentRows = useMemo(() => {
        return buildPaymentRows(projection).map(r => {
            const row: FlatRow = { period: r.period };
            for (const [partId, v] of Object.entries(r.repay)) row[`pr:${partId}`] = v;
            for (const [partId, v] of Object.entries(r.interest)) row[`pi:${partId}`] = v;
            return row;
        });
    }, [projection]);

    const rows = mode === 'balance' ? balanceRows : paymentRows;

    // Scale to the tallest stack per period (actuals and projection stack
    // separately on either side of today; payments share one stack), plus the
    // scenario line. Zero-based, so this only adds headroom above the peak.
    const axis = useMemo(() => {
        const totals: number[] = [];
        for (const row of rows) {
            let actual = 0;
            let proj = 0;
            let pay = 0;
            for (const [key, value] of Object.entries(row)) {
                if (typeof value !== 'number') continue;
                const colon = key.indexOf(':');
                const prefix = colon === -1 ? key : key.slice(0, colon);
                if (prefix === 'a') actual += value;
                else if (prefix === 'p') proj += value;
                else if (prefix === 'pr' || prefix === 'pi') pay += value;
                else if (key === 'scenarioTotal') totals.push(value);
            }
            totals.push(actual, proj, pay);
        }
        return moneyAxis(totals, { includeZero: true });
    }, [rows]);

    const ticks = useMemo(() => {
        // January of every nth year, thinned so long mortgages stay readable.
        const januaries = rows.map(r => r.period).filter(p => p.slice(5, 7) === '01');
        const step = Math.max(1, Math.ceil(januaries.length / 8));
        return januaries.filter((_, i) => i % step === 0);
    }, [rows]);

    const labelByPart = new Map(projection.parts.map(p => [p.id as string, p.label]));
    // One stable hue per loan account, assigned by the parts' order so a part
    // always keeps its color across renders and modes.
    const colorByAccount = buildChartColorMap(projection.parts.map(p => p.accountId));
    const colorOf = (accountId: string) => colorByAccount.get(accountId) ?? chartColorByIndex(0);

    return (
        <div className="flex flex-col gap-2">
            <div className="flex justify-end">
                <SegmentedToggle mode={mode} onChange={setMode} />
            </div>
            <ResponsiveContainer width="100%" height={height}>
                <ComposedChart data={rows} margin={{ top: 10, right: 12, bottom: 0, left: 0 }}>
                    <CartesianGrid
                        stroke="var(--color-border-soft)"
                        vertical={false}
                        strokeDasharray="2 4"
                    />
                    <XAxis
                        dataKey="period"
                        ticks={ticks}
                        interval={0}
                        tickFormatter={(p: string) => p.slice(0, 4)}
                        tick={{ fill: 'var(--color-fg-3)', fontSize: 11 }}
                        axisLine={false}
                        tickLine={false}
                    />
                    <YAxis
                        domain={axis?.domain ?? [0, 'auto']}
                        ticks={axis?.ticks}
                        tickFormatter={(v: number) =>
                            formatMoneyAxis(v, projection.currencyCode, catalog)
                        }
                        tick={{ fill: 'var(--color-fg-3)', fontSize: 11 }}
                        axisLine={false}
                        tickLine={false}
                        width={64}
                    />
                    <Tooltip
                        cursor={{
                            stroke: 'var(--color-border-strong)',
                            strokeWidth: 1,
                            strokeDasharray: '2 2',
                        }}
                        content={
                            <LoanTooltip
                                currencyCode={projection.currencyCode}
                                catalog={catalog}
                                labelByPart={labelByPart}
                            />
                        }
                    />
                    {/* Today: actuals to the left, projection to the right. */}
                    <ReferenceLine
                        x={projection.anchorMonth}
                        stroke="var(--color-border-strong)"
                        strokeDasharray="3 3"
                        label={{
                            value: t`today`,
                            position: 'insideTopLeft',
                            fill: 'var(--color-fg-3)',
                            fontSize: 10,
                        }}
                    />
                    {/* Rate-fixation boundaries: where the projection stops being contractual. */}
                    {projection.parts.map(p =>
                        p.fixedUntil === null ? null : (
                            <ReferenceLine
                                key={`fix-${p.id}`}
                                x={firstOfMonth(p.fixedUntil)}
                                stroke={colorOf(p.accountId)}
                                strokeDasharray="2 4"
                                label={{
                                    value: t`${p.label} fixed until`,
                                    position: 'insideTopRight',
                                    fill: 'var(--color-fg-3)',
                                    fontSize: 10,
                                }}
                            />
                        ),
                    )}

                    {mode === 'balance' && (
                        <>
                            {projection.parts.map(p => (
                                <Area
                                    key={`a:${p.id}`}
                                    dataKey={`a:${p.id}`}
                                    stackId="actual"
                                    name={`a:${p.id}`}
                                    stroke={colorOf(p.accountId)}
                                    fill={colorOf(p.accountId)}
                                    fillOpacity={0.45}
                                    strokeWidth={1.25}
                                    isAnimationActive={false}
                                />
                            ))}
                            {projection.parts.map(p => (
                                <Area
                                    key={`p:${p.id}`}
                                    dataKey={`p:${p.id}`}
                                    stackId="proj"
                                    name={`p:${p.id}`}
                                    stroke={colorOf(p.accountId)}
                                    strokeDasharray="4 3"
                                    fill={colorOf(p.accountId)}
                                    fillOpacity={0.16}
                                    strokeWidth={1.25}
                                    isAnimationActive={false}
                                />
                            ))}
                            {hasScenario && (
                                <Line
                                    dataKey="scenarioTotal"
                                    name="scenarioTotal"
                                    stroke="var(--color-fg-1)"
                                    strokeWidth={1.75}
                                    strokeDasharray="6 3"
                                    dot={false}
                                    isAnimationActive={false}
                                />
                            )}
                        </>
                    )}

                    {mode === 'payments' && (
                        <>
                            {/* Repayment (darker shade) then interest (lighter shade) per part,
                                emitted back-to-back so each part stacks as one contiguous
                                two-band block of a single hue (parts otherwise interleave). */}
                            {projection.parts.flatMap(p => [
                                <Area
                                    key={`pr:${p.id}`}
                                    dataKey={`pr:${p.id}`}
                                    stackId="pay"
                                    name={`pr:${p.id}`}
                                    stroke={colorOf(p.accountId)}
                                    fill={colorOf(p.accountId)}
                                    fillOpacity={0.8}
                                    strokeWidth={0.5}
                                    isAnimationActive={false}
                                />,
                                <Area
                                    key={`pi:${p.id}`}
                                    dataKey={`pi:${p.id}`}
                                    stackId="pay"
                                    name={`pi:${p.id}`}
                                    stroke={colorOf(p.accountId)}
                                    fill={colorOf(p.accountId)}
                                    fillOpacity={0.32}
                                    strokeWidth={0.5}
                                    isAnimationActive={false}
                                />,
                            ])}
                        </>
                    )}
                </ComposedChart>
            </ResponsiveContainer>
        </div>
    );
}

function SegmentedToggle({
    mode,
    onChange,
}: {
    mode: ChartMode;
    onChange: (m: ChartMode) => void;
}) {
    const { t } = useLingui();
    const item = (value: ChartMode, label: string) => (
        <button
            type="button"
            onClick={() => {
                onChange(value);
            }}
            className={cx(
                'px-2.5 py-1 text-xs font-medium rounded-md transition-colors',
                mode === value ? 'bg-bg-1 text-fg-1 shadow-sm' : 'text-fg-3 hover:text-fg-1',
            )}
            aria-pressed={mode === value}
        >
            {label}
        </button>
    );
    return (
        <div className="inline-flex items-center gap-0.5 rounded-lg bg-surface-2 p-0.5">
            {item('balance', t`Balance`)}
            {item('payments', t`Payments`)}
        </div>
    );
}

type LoanTooltipProps = Partial<TooltipContentProps<number, string>> & {
    currencyCode: string;
    catalog: CurrencyCatalog;
    labelByPart: Map<string, string>;
};

// Stacked balances/payments per loan part, plus the total they stack to. The
// "What-if total" line is an alternative total (not a stack component), so it
// renders as its own row and stays out of the sum.
function LoanTooltip({
    active,
    payload,
    label,
    currencyCode,
    catalog,
    labelByPart,
}: LoanTooltipProps) {
    if (!active || !payload || payload.length === 0) return null;

    const moved = payload.filter(item => (Number(item.value) || 0) !== 0);
    if (moved.length === 0) return null;
    const components = moved.filter(item => item.dataKey !== 'scenarioTotal');
    const scenario = moved.find(item => item.dataKey === 'scenarioTotal');
    const total = components.reduce((sum, item) => sum + (Number(item.value) || 0), 0);
    const heading =
        typeof label === 'string'
            ? formatCalendarDate(label.slice(0, 7), 'year-month', { style: 'long' })
            : '';

    return (
        <ChartTooltipShell heading={heading}>
            {components.map(item => (
                <ChartTooltipRow
                    key={String(item.dataKey)}
                    color={item.color}
                    name={chartSeriesLabel(String(item.name ?? item.dataKey), labelByPart)}
                    value={formatMoney(Number(item.value) || 0, currencyCode, catalog)}
                />
            ))}
            {components.length > 1 && (
                <ChartTooltipTotalRow
                    name={<Trans>Total</Trans>}
                    value={formatMoney(total, currencyCode, catalog)}
                />
            )}
            {scenario && (
                <ChartTooltipRow
                    color={scenario.color}
                    name={chartSeriesLabel('scenarioTotal', labelByPart)}
                    value={formatMoney(Number(scenario.value) || 0, currencyCode, catalog)}
                />
            )}
        </ChartTooltipShell>
    );
}

function chartSeriesLabel(seriesName: string, labelByPart: Map<string, string>): string {
    if (seriesName === 'scenarioTotal') return t`What-if total`;
    const colon = seriesName.indexOf(':');
    const prefix = seriesName.slice(0, colon);
    const label = labelByPart.get(seriesName.slice(colon + 1)) ?? t`Part`;
    switch (prefix) {
        case 'p':
            return t`${label} (projected)`;
        case 'pr':
            return t`${label} - repayment`;
        case 'pi':
            return t`${label} - interest`;
        default:
            return label;
    }
}

function firstOfMonth(date: string): string {
    return `${date.slice(0, 7)}-01`;
}
