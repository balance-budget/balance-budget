import { useMemo } from 'react';
import { areaY, defineChart } from '@tanstack/charts';
import { crosshair } from '@tanstack/charts/crosshair';
import { d3Curve } from '@tanstack/charts/d3/shape';
import { scaleLinear } from '@tanstack/charts/scales/linear';
import { scalePoint } from '@tanstack/charts/scales/point';
import { curveMonotoneX } from 'd3-shape';
import { useLingui } from '@lingui/react/macro';
import { useCurrencyCatalog } from '../api/currencies';
import type { NetWorthPoint } from '../api/dashboard';
import { chartTooltip } from '../lib/chart';
import { formatMonthAxisDate, formatTrendTooltipDate } from '../lib/dates';
import { formatMoneyAxis } from '../lib/money';
import { chartColorByIndex } from '../lib/visualHints';
import { Chart } from './Chart';

type NetWorthChartProps = {
    points: NetWorthPoint[];
    currencyCode: string;
    height?: number;
};

// The two stacked bands are a fixed, ordered pair (liquid below, illiquid
// above), so they take the first two palette slots by position.
type Band = 'liquid' | 'illiquid';
const BAND_COLOR: Record<Band, string> = {
    liquid: chartColorByIndex(0),
    illiquid: chartColorByIndex(1),
};

type Row = { date: string; band: Band; amount: number };

/**
 * The long-horizon dashboard chart (ADR-0030): net worth as a signed stack of its two components,
 * liquid (bottom) and illiquid (top), so the top edge is total net worth. The illiquid band is a
 * house amortizing against its mortgage; watching it grow over the years is the wealth-building
 * story. The stack is diverging, so a net-debt component stays honestly below zero.
 */
export function NetWorthChart({ points, currencyCode, height = 240 }: NetWorthChartProps) {
    const { t } = useLingui();
    const catalog = useCurrencyCatalog();
    const label: Record<Band, string> = { liquid: t`Liquid`, illiquid: t`Illiquid` };

    const rows = useMemo<Row[]>(
        () =>
            points.flatMap(p => [
                { date: p.date, band: 'liquid' as const, amount: p.liquidMinor },
                {
                    date: p.date,
                    band: 'illiquid' as const,
                    amount: p.netWorthMinor - p.liquidMinor,
                },
            ]),
        [points],
    );

    // Aim for roughly six date labels regardless of range length.
    const tickValues = useMemo(() => {
        const dates = [...new Set(rows.map(r => r.date))];
        const step = Math.max(1, Math.ceil(dates.length / 6));
        return dates.filter((_, i) => i % step === 0);
    }, [rows]);

    const definition = useMemo(
        () =>
            defineChart({
                marks: [
                    areaY(rows, {
                        x: 'date',
                        y: 'amount',
                        z: 'band',
                        curve: d3Curve(curveMonotoneX),
                        fillOpacity: 0.55,
                        stroke: row => BAND_COLOR[row.band],
                        strokeWidth: 1.25,
                    }),
                    crosshair({ x: true, y: false }),
                ],
                x: {
                    scale: scalePoint,
                    axis: {
                        line: false,
                        ticks: { values: tickValues, format: formatMonthAxisDate },
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
                    domain: Object.keys(BAND_COLOR),
                    range: Object.values(BAND_COLOR),
                },
                focus: 'group-x',
                tooltip: chartTooltip,
            }),
        [rows, tickValues, currencyCode, catalog],
    );

    return (
        <Chart
            definition={definition}
            height={height}
            currency={currencyCode}
            ariaLabel={t`Net worth over time`}
            tooltipHeading={points => formatTrendTooltipDate(points[0]?.xValue ?? '')}
            tooltipRows={(points, formatValue) => [
                ...points.map(point => ({
                    color: point.color,
                    name: label[point.datum.band],
                    value: formatValue(point.datum.amount),
                })),
                {
                    name: t`Net worth`,
                    value: formatValue(points.reduce((sum, p) => sum + p.datum.amount, 0)),
                    total: true,
                },
            ]}
        />
    );
}
