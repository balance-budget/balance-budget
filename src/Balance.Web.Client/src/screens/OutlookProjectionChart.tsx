import { useMemo } from 'react';
import { useLingui } from '@lingui/react/macro';
import { areaY, defineChart, lineY, ruleX, ruleY } from '@tanstack/charts';
import { crosshair } from '@tanstack/charts/crosshair';
import { d3Curve } from '@tanstack/charts/d3/shape';
import { scaleLinear } from '@tanstack/charts/scales/linear';
import { scalePoint } from '@tanstack/charts/scales/point';
import { curveMonotoneX } from 'd3-shape';
import { useCurrencyCatalog } from '../api/currencies';
import type { OutlookAccountProjection } from '../api/outlook';
import { chartTooltip } from '../lib/chart';
import { formatMonthAxisDate } from '../lib/dates';
import { formatCalendarDate } from '../i18n/format';
import { formatMoneyAxis } from '../lib/money';
import { Chart } from '../components/Chart';

type ActualRow = { kind: 'actual'; month: string; balance: number };
type ProjectedRow = {
    kind: 'projected';
    month: string;
    mid: number;
    low: number;
    high: number;
    expectedIn?: number;
    expectedOut?: number;
};
type ScenarioRow = { kind: 'scenario'; month: string; balance: number };

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

    // The projection is seeded at the anchor (last actual) so the baseline, band and
    // scenario start from today's real balance rather than floating.
    const { actuals, projected, scenario } = useMemo(() => {
        const anchor = account.actuals.at(-1);
        const seed: ProjectedRow[] =
            anchor === undefined
                ? []
                : [
                      {
                          kind: 'projected' as const,
                          month: anchor.month,
                          mid: anchor.endBalance,
                          low: anchor.endBalance,
                          high: anchor.endBalance,
                      },
                  ];

        return {
            actuals: account.actuals.map((a): ActualRow => ({
                kind: 'actual',
                month: a.month,
                balance: a.endBalance,
            })),
            projected: [
                ...seed,
                ...account.baseline.map((b): ProjectedRow => ({
                    kind: 'projected',
                    month: b.month,
                    mid: b.endBalanceMid,
                    low: b.endBalanceLow,
                    high: b.endBalanceHigh,
                    expectedIn: b.expectedIn,
                    expectedOut: b.expectedOut,
                })),
            ],
            scenario:
                account.scenario === null
                    ? []
                    : [
                          ...(anchor
                              ? [
                                    {
                                        kind: 'scenario' as const,
                                        month: anchor.month,
                                        balance: anchor.endBalance,
                                    },
                                ]
                              : []),
                          ...account.scenario.map((s): ScenarioRow => ({
                              kind: 'scenario',
                              month: s.month,
                              balance: s.endBalanceMid,
                          })),
                      ],
        };
    }, [account]);

    // The December row's category key, so the year-end marker lands on the right tick (absent when
    // the horizon stops before December).
    const yearEndMonth = `${account.yearEnd.date.slice(0, 7)}-01`;

    const definition = useMemo(
        () =>
            defineChart({
                marks: [
                    // The Typical-spend uncertainty band (projected months only).
                    areaY(projected, {
                        id: 'band',
                        x: 'month',
                        y1: 'low',
                        y2: 'high',
                        curve: d3Curve(curveMonotoneX),
                        fill: 'var(--color-brand-primary)',
                        fillOpacity: 0.12,
                    }),
                    ruleY([0], {
                        stroke: 'var(--color-danger)',
                        strokeDasharray: '3 3',
                        strokeOpacity: 1,
                    }),
                    ruleX([yearEndMonth], {
                        stroke: 'var(--color-border-strong)',
                        strokeDasharray: '3 3',
                    }),
                    // Ledger actuals — solid, left of today.
                    lineY(actuals, {
                        id: 'actual',
                        x: 'month',
                        y: 'balance',
                        curve: d3Curve(curveMonotoneX),
                        stroke: 'var(--color-fg-2)',
                        strokeWidth: 1.25,
                    }),
                    // Projected mid — dashed, right of today.
                    lineY(projected, {
                        id: 'mid',
                        x: 'month',
                        y: 'mid',
                        curve: d3Curve(curveMonotoneX),
                        stroke: 'var(--color-brand-primary)',
                        strokeWidth: 1.25,
                        strokeDasharray: '5 4',
                    }),
                    ...(scenario.length > 0
                        ? [
                              lineY(scenario, {
                                  id: 'scenario',
                                  x: 'month',
                                  y: 'balance',
                                  curve: d3Curve(curveMonotoneX),
                                  stroke: 'var(--color-warning)',
                                  strokeWidth: 1.25,
                                  strokeDasharray: '2 3',
                              }),
                          ]
                        : []),
                    crosshair({ x: true, y: false }),
                ],
                x: {
                    scale: scalePoint,
                    axis: {
                        line: false,
                        ticks: { format: formatMonthAxisDate },
                        tickLabels: { thin: { minGap: 16 } },
                    },
                },
                y: {
                    scale: scaleLinear,
                    nice: true,
                    grid: true,
                    axis: {
                        line: false,
                        ticks: {
                            format: (v: number) =>
                                formatMoneyAxis(v, account.currencyCode, catalog),
                        },
                    },
                },
                focus: 'group-x',
                tooltip: chartTooltip,
            }),
        [actuals, projected, scenario, yearEndMonth, account.currencyCode, catalog],
    );

    return (
        <Chart
            definition={definition}
            height={height}
            currency={account.currencyCode}
            ariaLabel={t`Projected balance`}
            tooltipHeading={points =>
                formatCalendarDate(points[0]?.xValue ?? '', 'year-month', { style: 'long' })
            }
            tooltipRows={(points, money) =>
                points.flatMap(point => {
                    const row = point.datum;
                    if (row.kind === 'actual') {
                        return [{ name: t`Actual`, value: money(row.balance) }];
                    }
                    if (row.kind === 'scenario') {
                        return [{ name: t`What-if`, value: money(row.balance) }];
                    }
                    // The band and the mid line share their rows, so the mark decides
                    // whether this point reads as the range or as the projected balance.
                    if (point.markId === 'band') {
                        return [
                            {
                                name: t`Typical range`,
                                value: `${money(row.low)} – ${money(row.high)}`,
                            },
                        ];
                    }
                    return [
                        { name: t`Projected`, value: money(row.mid) },
                        ...(row.expectedIn
                            ? [{ name: t`Expected in`, value: money(row.expectedIn) }]
                            : []),
                        ...(row.expectedOut
                            ? [{ name: t`Expected out`, value: money(row.expectedOut) }]
                            : []),
                    ];
                })
            }
        />
    );
}
