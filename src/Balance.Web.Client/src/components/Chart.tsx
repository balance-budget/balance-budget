import type { ReactNode } from 'react';
import { Chart as TanStackChart, type ChartProps } from '@tanstack/charts/react/tooltip';
import type { ChartPoint, ChartValue } from '@tanstack/charts';
import { useCurrencyCatalog } from '../api/currencies';
import { formatMoney } from '../lib/money';
import { ChartTooltipShell, ChartTooltipRow, ChartTooltipTotalRow } from './ChartTooltip';

export type ChartTooltipRowSpec = {
    color?: string;
    name: ReactNode;
    value: ReactNode;
    /** Renders below the series rows, separated by a rule (stacked-chart totals). */
    total?: boolean;
};

type Props<TDatum, TXValue extends ChartValue, TYValue extends ChartValue> = ChartProps<
    TDatum,
    TXValue,
    TYValue
> & {
    /** Currency the default tooltip rows format their values in (ADR-0002 minor units). */
    currency?: string;
    tooltipHeading?: (points: readonly ChartPoint<TDatum, TXValue, TYValue>[]) => ReactNode;
    tooltipRows?: (
        points: readonly ChartPoint<TDatum, TXValue, TYValue>[],
        formatValue: (amount: number) => string,
    ) => readonly ChartTooltipRowSpec[];
};

/**
 * Every chart in the app renders through this wrapper so they share tooltip
 * chrome and accessible naming. It adds defaults only: pass any TanStack Charts
 * prop straight through, or `renderTooltipBody` to take the body over entirely.
 */
export function Chart<
    TDatum,
    TXValue extends ChartValue = ChartValue,
    TYValue extends ChartValue = ChartValue,
>({ currency, tooltipHeading, tooltipRows, ...props }: Props<TDatum, TXValue, TYValue>) {
    const catalog = useCurrencyCatalog();

    return (
        <TanStackChart
            {...props}
            renderTooltipBody={
                props.renderTooltipBody ??
                (({ points }) => {
                    const formatValue = (amount: number) =>
                        currency === undefined
                            ? String(amount)
                            : formatMoney(amount, currency, catalog);
                    const rows: readonly ChartTooltipRowSpec[] =
                        tooltipRows?.(points, formatValue) ??
                        points.map(point => ({
                            color: point.color,
                            name: point.groupLabel,
                            value:
                                typeof point.yValue === 'number'
                                    ? formatValue(point.yValue)
                                    : String(point.yValue),
                        }));

                    return (
                        <ChartTooltipShell heading={tooltipHeading?.(points)}>
                            {rows.map((row, i) =>
                                row.total ? (
                                    <ChartTooltipTotalRow
                                        key={i}
                                        name={row.name}
                                        value={row.value}
                                    />
                                ) : (
                                    <ChartTooltipRow
                                        key={i}
                                        color={row.color}
                                        name={row.name}
                                        value={row.value}
                                    />
                                ),
                            )}
                        </ChartTooltipShell>
                    );
                })
            }
        />
    );
}
