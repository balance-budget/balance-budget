import { useMemo } from 'react';
import { Trans, useLingui } from '@lingui/react/macro';
import { barY, defineChart, ruleY } from '@tanstack/charts';
import { crosshair } from '@tanstack/charts/crosshair';
import { colorLegend } from '@tanstack/charts/legend';
import { scaleBand } from '@tanstack/charts/scales/band';
import { scaleLinear } from '@tanstack/charts/scales/linear';
import type { Account } from '../api/accounts';
import { useCurrencyCatalog } from '../api/currencies';
import { useRegisterSummary, type RegisterSummary } from '../api/register';
import { chartTooltip } from '../lib/chart';
import { formatBucketAxisDate, formatBucketTooltipDate } from '../lib/dates';
import { formatMoney, formatMoneyAxis } from '../lib/money';
import { effectiveSummaryRange, summaryBucketFor } from '../lib/registerSummary';
import { chartColorByIndex } from '../lib/visualHints';
import { Chart } from './Chart';
import { ErrorState } from './ErrorState';
import { Skeleton } from './Skeleton';

type Row = { start: string; segment: string; amount: number };

/** One row per segment value that actually moved; a missing position/series pair
 *  contributes zero to the stack layout without emitting a point. */
function buildRows(summary: RegisterSummary): Row[] {
    const nameById = new Map(summary.segments.map(s => [s.accountId, s.accountName]));
    return summary.buckets.flatMap(bucket =>
        bucket.values.flatMap(value => {
            const segment = nameById.get(value.accountId);
            return segment === undefined || value.amount === 0
                ? []
                : [{ start: bucket.start, segment, amount: value.amount }];
        }),
    );
}

/**
 * The Register summary (CONTEXT.md) as a stacked bar chart: net movement per
 * time bucket, one stack segment per direct child (a leaf is its own single
 * segment). Net-negative segments stack below the zero line. The range follows
 * the register's date filter; the bucket size adapts to the range length.
 */
export function RegisterSummaryChart({
    account,
    filterFrom,
    filterTo,
    height = 240,
}: {
    account: Account;
    filterFrom: string;
    filterTo: string;
    height?: number;
}) {
    const { t } = useLingui();
    const catalog = useCurrencyCatalog();
    const range = useMemo(
        () => effectiveSummaryRange(filterFrom, filterTo),
        [filterFrom, filterTo],
    );
    const bucket = summaryBucketFor(range);
    const query = useRegisterSummary(account.id, range, bucket);
    const summary = query.data;
    const rows = useMemo(() => (summary ? buildRows(summary) : []), [summary]);

    const definition = useMemo(() => {
        const segments = summary?.segments ?? [];
        return defineChart({
            marks: [
                barY(rows, { x: 'start', y: 'amount', color: 'segment' }),
                ruleY([0], { stroke: 'var(--color-border-strong)', strokeOpacity: 1 }),
                crosshair({ x: { band: true }, y: false }),
            ],
            x: {
                scale: () => scaleBand().padding(0.2),
                axis: {
                    line: false,
                    ticks: {
                        format: (d: string) => formatBucketAxisDate(d, bucket),
                    },
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
                            formatMoneyAxis(v, summary?.currencyCode ?? '', catalog),
                    },
                },
            },
            color: {
                domain: segments.map(s => s.accountName),
                range: segments.map((_, i) => chartColorByIndex(i)),
                legend: segments.length > 1 ? colorLegend({ placement: 'bottom' }) : undefined,
            },
            focus: 'group-x',
            tooltip: chartTooltip,
        });
    }, [rows, summary, bucket, catalog]);

    if (query.isPending) {
        return <Skeleton className="w-full h-[240px]" />;
    }

    if (query.isError) {
        return (
            <ErrorState
                message={t`Couldn't load register summary.`}
                onRetry={() => void query.refetch()}
            />
        );
    }

    if (query.data.segments.length === 0) {
        return (
            <div className="flex items-center justify-center text-sm text-fg-3" style={{ height }}>
                <Trans>No money moved in this period.</Trans>
            </div>
        );
    }

    return (
        <Chart
            definition={definition}
            height={height}
            ariaLabel={t`Net movement per period`}
            tooltipHeading={points => {
                const heading = formatBucketTooltipDate(points[0]?.xValue ?? '', bucket);
                return bucket === 'Week' ? <Trans>Week of {heading}</Trans> : heading;
            }}
            tooltipRows={points => {
                // Movement is signed: a bucket reads as +€120 in / -€80 out.
                const money = (amount: number) =>
                    formatMoney(amount, query.data.currencyCode, catalog, { sign: true });
                const specs = points.map(point => ({
                    color: point.color,
                    name: point.datum.segment,
                    value: money(point.datum.amount),
                }));
                if (specs.length < 2) return specs;
                return [
                    ...specs,
                    {
                        name: t`Net`,
                        value: money(points.reduce((sum, p) => sum + p.datum.amount, 0)),
                        total: true,
                    },
                ];
            }}
        />
    );
}
