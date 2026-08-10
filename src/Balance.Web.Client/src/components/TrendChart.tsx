import { useMemo } from 'react';
import { areaY, defineChart, lineY } from '@tanstack/charts';
import { crosshair } from '@tanstack/charts/crosshair';
import { d3Curve } from '@tanstack/charts/d3/shape';
import { controlledSignal } from '@tanstack/charts/interaction/signal';
import { interactiveColorLegend, type InteractiveColorLegendChange } from '@tanstack/charts/legend';
import { scaleLinear } from '@tanstack/charts/scales/linear';
import { scalePoint } from '@tanstack/charts/scales/point';
import { curveMonotoneX } from 'd3-shape';
import { useLingui } from '@lingui/react/macro';
import { useCurrencyCatalog } from '../api/currencies';
import type { TrendRange } from '../api/dashboard';
import { chartTooltip } from '../lib/chart';
import { formatTrendAxisDate, formatTrendTooltipDate } from '../lib/dates';
import type { AccountId, AccountTrend } from '../lib/domain';
import { formatMoneyAxis } from '../lib/money';
import { buildChartColorMap, chartColorByIndex } from '../lib/visualHints';
import { Chart } from './Chart';

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

type Row = { date: string; accountId: string; name: string; balance: number };

function buildRows(series: AccountTrend[]): Row[] {
    return series.flatMap(s =>
        s.points.map(p => ({
            date: p.date,
            accountId: s.accountId,
            name: s.name,
            balance: p.balanceMinor,
        })),
    );
}

function computeTicks(rows: Row[], range: TrendRange): string[] {
    const dates = [...new Set(rows.map(r => r.date))];
    if (range === '1M') {
        // Weekly cadence; day-of-month carries information at this scale.
        return dates.filter((_, i) => i % 7 === 0);
    }
    // Monthly cadence at the 1st of the month — the day-of-month is noise
    // for 3M / 6M / 1Y, so anchor to month boundaries instead.
    return dates.filter(d => d.endsWith('-01'));
}

/**
 * Multi-account balance trend. Each series is one Asset Account; the chart shows a
 * unified crosshair tooltip with all balances at the snapped date, sorted
 * value-descending, and a legend that toggles a series off. Axes auto-fit; y-axis
 * labels are compact above €10k, full below.
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
    const { t } = useLingui();
    const catalog = useCurrencyCatalog();
    const rows = useMemo(() => buildRows(series), [series]);
    const tickValues = useMemo(() => computeTicks(rows, range), [rows, range]);
    const nameByAccount = useMemo(() => new Map(series.map(s => [s.accountId, s.name])), [series]);
    // This chart owns its colors: one stable hue per account by the series'
    // (API) order, so each TrendChart is self-contained and always starts at the
    // first palette slot regardless of what other charts show.
    const colorByAccount = useMemo(
        () => buildChartColorMap(series.map(s => s.accountId)),
        [series],
    );
    const visible = useMemo(
        () => series.map(s => s.accountId).filter(id => !hiddenAccountIds.has(id)),
        [series, hiddenAccountIds],
    );

    const definition = useMemo(() => {
        const paint = {
            x: 'date',
            y: 'balance',
            color: 'accountId',
            curve: d3Curve(curveMonotoneX),
        } as const;

        return defineChart({
            marks: [
                variant === 'stacked'
                    ? areaY(rows, {
                          ...paint,
                          fillOpacity: 0.55,
                          stroke: row => colorByAccount.get(row.accountId) ?? chartColorByIndex(0),
                          strokeWidth: 1.25,
                      })
                    : lineY(rows, { ...paint, strokeWidth: 1.75 }),
                crosshair({ x: true, y: false }),
            ],
            x: {
                scale: scalePoint,
                axis: {
                    line: false,
                    ticks: {
                        values: tickValues,
                        format: (d: string) => formatTrendAxisDate(d, range),
                    },
                },
            },
            y: {
                scale: scaleLinear,
                nice: true,
                grid: true,
                axis: {
                    line: false,
                    ticks: { format: (v: number) => formatMoneyAxis(v, currencyCode, catalog) },
                },
            },
            color: {
                domain: [...colorByAccount.keys()],
                range: [...colorByAccount.values()],
                legend: interactiveColorLegend({
                    visible: controlledSignal<
                        readonly AccountId[],
                        InteractiveColorLegendChange<AccountId>
                    >(visible, (_next, { reason }) => {
                        onToggleSeries(reason.value);
                    }),
                    placement: 'bottom',
                    ariaLabel: t`Series visibility`,
                    format: id => nameByAccount.get(id) ?? id,
                }),
            },
            focus: 'group-x',
            tooltip: chartTooltip,
        });
    }, [
        rows,
        tickValues,
        range,
        variant,
        currencyCode,
        catalog,
        colorByAccount,
        nameByAccount,
        visible,
        onToggleSeries,
        t,
    ]);

    return (
        <Chart
            definition={definition}
            height={height}
            currency={currencyCode}
            ariaLabel={t`Account balances over time`}
            tooltipHeading={points => formatTrendTooltipDate(points[0]?.xValue ?? '')}
            tooltipRows={(points, formatValue) => {
                const sorted = [...points].sort((a, b) => b.datum.balance - a.datum.balance);
                const rowSpecs = sorted.map(point => ({
                    color: point.color,
                    name: point.datum.name,
                    value: formatValue(point.datum.balance),
                }));
                if (variant !== 'stacked' || sorted.length < 2) return rowSpecs;
                return [
                    ...rowSpecs,
                    {
                        name: t`Total`,
                        value: formatValue(sorted.reduce((sum, p) => sum + p.datum.balance, 0)),
                        total: true,
                    },
                ];
            }}
        />
    );
}
