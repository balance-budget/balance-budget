import { useMemo, useState } from 'react';
import { areaY, defineChart, lineY, ruleX } from '@tanstack/charts';
import { crosshair } from '@tanstack/charts/crosshair';
import { scaleLinear } from '@tanstack/charts/scales/linear';
import { scalePoint } from '@tanstack/charts/scales/point';
import { t } from '@lingui/core/macro';
import { useLingui } from '@lingui/react/macro';
import type { LoanProjection } from '../api/loans';
import { useCurrencyCatalog } from '../api/currencies';
import { formatCalendarDate } from '../i18n/format';
import { chartTooltip } from '../lib/chart';
import { cx } from '../lib/cx';
import { formatMoneyAxis } from '../lib/money';
import { buildChartColorMap, chartColorByIndex } from '../lib/visualHints';
import { buildChartRows, buildPaymentRows } from '../screens/loanDetail.state';
import { Chart } from './Chart';

type LoanChartProps = {
    projection: LoanProjection;
    height?: number;
};

type ChartMode = 'balance' | 'payments';

/** Series id: `<prefix>:<loan part id>`, or the standalone what-if total. */
type Row = { period: string; series: string; amount: number };

const SCENARIO_SERIES = 'scenarioTotal';
/** Series prefixes: balance actuals, balance projection, repayment, interest. */
const PREFIX = { actual: 'a', projected: 'p', repay: 'pr', interest: 'pi' } as const;

function seriesId(prefix: (typeof PREFIX)[keyof typeof PREFIX], partId: string): string {
    return `${prefix}:${partId}`;
}

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
    const { t: tt } = useLingui();
    const catalog = useCurrencyCatalog();
    const [mode, setMode] = useState<ChartMode>('balance');

    const labelByPart = useMemo(
        () => new Map(projection.parts.map(p => [p.id as string, p.label])),
        [projection.parts],
    );
    // One stable hue per loan account, assigned by the parts' order so a part
    // always keeps its color across renders and modes.
    const colorByAccount = useMemo(
        () => buildChartColorMap(projection.parts.map(p => p.accountId)),
        [projection.parts],
    );
    // Series stack in first-seen order, so iterate parts outermost. In the payment
    // view each part's repayment and interest are emitted back-to-back, keeping the
    // part one contiguous two-band block of a single hue.
    const { actual, projected, scenario, payments } = useMemo(() => {
        const chartRows = buildChartRows(projection);
        const paymentRows = buildPaymentRows(projection);
        const series = (
            rows: readonly { period: string }[],
            prefix: (typeof PREFIX)[keyof typeof PREFIX],
            partId: string,
            read: (index: number) => number | undefined,
        ): Row[] =>
            rows.flatMap((row, i) => {
                const amount = read(i);
                return amount === undefined
                    ? []
                    : [{ period: row.period, series: seriesId(prefix, partId), amount }];
            });

        return {
            actual: projection.parts.flatMap(p =>
                series(chartRows, PREFIX.actual, p.id, i => chartRows[i]?.actual[p.id]),
            ),
            projected: projection.parts.flatMap(p =>
                series(chartRows, PREFIX.projected, p.id, i => chartRows[i]?.proj[p.id]),
            ),
            payments: projection.parts.flatMap(p => [
                ...series(paymentRows, PREFIX.repay, p.id, i => paymentRows[i]?.repay[p.id]),
                ...series(paymentRows, PREFIX.interest, p.id, i => paymentRows[i]?.interest[p.id]),
            ]),
            scenario: chartRows.flatMap(r =>
                r.scenarioTotal === null
                    ? []
                    : [{ period: r.period, series: SCENARIO_SERIES, amount: r.scenarioTotal }],
            ),
        };
    }, [projection]);

    const tickValues = useMemo(() => {
        // January of every nth year, thinned so long mortgages stay readable.
        const periods = [...new Set([...actual, ...projected, ...payments].map(r => r.period))];
        const januaries = periods.filter(p => p.slice(5, 7) === '01').sort();
        const step = Math.max(1, Math.ceil(januaries.length / 8));
        return januaries.filter((_, i) => i % step === 0);
    }, [actual, projected, payments]);

    const definition = useMemo(() => {
        const colorOf = (accountId: string) =>
            colorByAccount.get(accountId) ?? chartColorByIndex(0);
        // Repayment reads darker than interest inside one hue, so bake the shade into
        // the fill: one stack cannot carry two fill opacities.
        const shaded = (accountId: string, percent: number) =>
            // eslint-disable-next-line lingui/no-unlocalized-strings -- CSS color value, not UI copy.
            `color-mix(in srgb, ${colorOf(accountId)} ${percent}%, transparent)`;
        const partOf = (series: string) =>
            projection.parts.find(p => p.id === series.slice(series.indexOf(':') + 1));
        const hueOf = (row: Row) => {
            const part = partOf(row.series);
            return part ? colorOf(part.accountId) : chartColorByIndex(0);
        };

        const marks =
            mode === 'balance'
                ? [
                      areaY(actual, {
                          x: 'period',
                          y: 'amount',
                          color: 'series',
                          fillOpacity: 0.45,
                          stroke: hueOf,
                          strokeWidth: 1.25,
                      }),
                      areaY(projected, {
                          x: 'period',
                          y: 'amount',
                          color: 'series',
                          fillOpacity: 0.16,
                          stroke: hueOf,
                          strokeWidth: 1.25,
                          strokeDasharray: '4 3',
                      }),
                      ...(scenario.length > 0
                          ? [
                                lineY(scenario, {
                                    x: 'period',
                                    y: 'amount',
                                    color: 'series',
                                    strokeWidth: 1.75,
                                    strokeDasharray: '6 3',
                                }),
                            ]
                          : []),
                  ]
                : [
                      areaY(payments, {
                          x: 'period',
                          y: 'amount',
                          color: 'series',
                          fill: row => {
                              const part = partOf(row.series);
                              return part
                                  ? shaded(
                                        part.accountId,
                                        row.series.startsWith(PREFIX.repay) ? 80 : 32,
                                    )
                                  : chartColorByIndex(0);
                          },
                          fillOpacity: 1,
                          stroke: hueOf,
                          strokeWidth: 0.5,
                      }),
                  ];

        const series = [
            ...projection.parts.flatMap(p =>
                Object.values(PREFIX).map(prefix => seriesId(prefix, p.id)),
            ),
            SCENARIO_SERIES,
        ];

        return defineChart({
            marks: [
                ...marks,
                // Today: actuals to the left, projection to the right.
                ruleX([projection.anchorMonth], {
                    stroke: 'var(--color-border-strong)',
                    strokeDasharray: '3 3',
                }),
                // Rate-fixation boundaries: where the projection stops being contractual.
                ...projection.parts.flatMap(p =>
                    p.fixedUntil === null
                        ? []
                        : [
                              ruleX([firstOfMonth(p.fixedUntil)], {
                                  stroke: colorOf(p.accountId),
                                  strokeDasharray: '2 4',
                              }),
                          ],
                ),
                crosshair({ x: true, y: false }),
            ],
            x: {
                scale: scalePoint,
                axis: {
                    line: false,
                    ticks: { values: tickValues, format: (p: string) => p.slice(0, 4) },
                },
            },
            y: {
                scale: scaleLinear,
                nice: true,
                grid: true,
                axis: {
                    line: false,
                    ticks: {
                        format: (v: number) => formatMoneyAxis(v, projection.currencyCode, catalog),
                    },
                },
            },
            color: {
                domain: series,
                range: series.map(s =>
                    s === SCENARIO_SERIES
                        ? 'var(--color-fg-1)'
                        : colorOf(partOf(s)?.accountId ?? ''),
                ),
            },
            focus: 'group-x',
            tooltip: chartTooltip,
        });
    }, [
        mode,
        actual,
        projected,
        scenario,
        payments,
        tickValues,
        projection,
        catalog,
        colorByAccount,
    ]);

    return (
        <div className="flex flex-col gap-2">
            <div className="flex justify-end">
                <SegmentedToggle mode={mode} onChange={setMode} />
            </div>
            <Chart
                definition={definition}
                height={height}
                currency={projection.currencyCode}
                ariaLabel={
                    mode === 'balance' ? tt`Loan balance over time` : tt`Loan payments over time`
                }
                tooltipHeading={points =>
                    formatCalendarDate((points[0]?.xValue ?? '').slice(0, 7), 'year-month', {
                        style: 'long',
                    })
                }
                tooltipRows={(points, formatValue) => {
                    // The what-if total is an alternative total, not a stack component, so it
                    // renders as its own row and stays out of the sum.
                    const components = points.filter(p => p.datum.series !== SCENARIO_SERIES);
                    const scenarioPoint = points.find(p => p.datum.series === SCENARIO_SERIES);
                    return [
                        ...components.map(point => ({
                            color: point.color,
                            name: chartSeriesLabel(point.datum.series, labelByPart),
                            value: formatValue(point.datum.amount),
                        })),
                        ...(components.length > 1
                            ? [
                                  {
                                      name: tt`Total`,
                                      value: formatValue(
                                          components.reduce((sum, p) => sum + p.datum.amount, 0),
                                      ),
                                      total: true,
                                  },
                              ]
                            : []),
                        ...(scenarioPoint
                            ? [
                                  {
                                      color: scenarioPoint.color,
                                      name: chartSeriesLabel(SCENARIO_SERIES, labelByPart),
                                      value: formatValue(scenarioPoint.datum.amount),
                                  },
                              ]
                            : []),
                    ];
                }}
            />
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

function chartSeriesLabel(seriesName: string, labelByPart: Map<string, string>): string {
    if (seriesName === SCENARIO_SERIES) return t`What-if total`;
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
