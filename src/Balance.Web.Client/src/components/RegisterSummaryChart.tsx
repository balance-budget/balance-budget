import { useMemo } from 'react';
import { Trans, useLingui } from '@lingui/react/macro';
import {
    Bar,
    BarChart,
    CartesianGrid,
    Legend,
    ReferenceLine,
    ResponsiveContainer,
    Tooltip,
    type TooltipContentProps,
    XAxis,
    YAxis,
} from 'recharts';
import type { Account } from '../api/accounts';
import { useCurrencyCatalog, type CurrencyCatalog } from '../api/currencies';
import { useRegisterSummary, type RegisterSummary } from '../api/register';
import { formatBucketAxisDate, formatBucketTooltipDate } from '../lib/dates';
import { formatMoney, formatMoneyAxis } from '../lib/money';
import { moneyAxis } from '../lib/chartAxis';
import {
    effectiveSummaryRange,
    summaryBucketFor,
    type RegisterSummaryBucketSize,
} from '../lib/registerSummary';
import { chartColorByIndex } from '../lib/visualHints';
import { ChartTooltipShell, ChartTooltipRow, ChartTooltipTotalRow } from './ChartTooltip';
import { ErrorState } from './ErrorState';
import { Skeleton } from './Skeleton';

type ChartRow = { start: string } & Record<string, number | string>;

/** Zero-fill every segment on every bucket — recharts stacks by key, so a
 *  missing key would make segments jump stack positions between bars. */
function buildRows(summary: RegisterSummary): ChartRow[] {
    return summary.buckets.map(bucket => {
        const row: ChartRow = { start: bucket.start };
        for (const segment of summary.segments) {
            row[segment.accountId] = 0;
        }
        for (const value of bucket.values) {
            row[value.accountId] = value.amount;
        }
        return row;
    });
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
    const rows = useMemo(() => (query.data ? buildRows(query.data) : []), [query.data]);
    // Sign-stacked: positive segments stack up from zero, negatives down. Scale
    // to each bucket's positive and negative totals; zero-based, so the only
    // effect is a little headroom above and below the tallest bars.
    const axis = useMemo(() => {
        const bounds: number[] = [];
        for (const row of rows) {
            let positive = 0;
            let negative = 0;
            for (const [key, value] of Object.entries(row)) {
                if (key === 'start' || typeof value !== 'number') continue;
                if (value > 0) positive += value;
                else negative += value;
            }
            bounds.push(positive, negative);
        }
        return moneyAxis(bounds, { includeZero: true });
    }, [rows]);

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

    const summary = query.data;

    if (summary.segments.length === 0) {
        return (
            <div className="flex items-center justify-center text-sm text-fg-3" style={{ height }}>
                <Trans>No money moved in this period.</Trans>
            </div>
        );
    }

    return (
        <ResponsiveContainer width="100%" height={height}>
            <BarChart
                data={rows}
                stackOffset="sign"
                margin={{ top: 10, right: 12, bottom: 0, left: 0 }}
            >
                <CartesianGrid
                    stroke="var(--color-border-soft)"
                    vertical={false}
                    strokeDasharray="2 4"
                />
                <XAxis
                    dataKey="start"
                    tickFormatter={(d: string) => formatBucketAxisDate(d, summary.bucket)}
                    tick={{ fill: 'var(--color-fg-3)', fontSize: 11 }}
                    axisLine={false}
                    tickLine={false}
                />
                <YAxis
                    domain={axis?.domain ?? ['auto', 'auto']}
                    ticks={axis?.ticks}
                    tickFormatter={(v: number) => formatMoneyAxis(v, summary.currencyCode, catalog)}
                    tick={{ fill: 'var(--color-fg-3)', fontSize: 11 }}
                    axisLine={false}
                    tickLine={false}
                    width={60}
                />
                <ReferenceLine y={0} stroke="var(--color-border-strong)" />
                <Tooltip
                    content={
                        <SummaryTooltip
                            bucket={summary.bucket}
                            currencyCode={summary.currencyCode}
                            catalog={catalog}
                        />
                    }
                    cursor={{ fill: 'var(--color-surface-2)', fillOpacity: 0.5 }}
                />
                {summary.segments.length > 1 && (
                    <Legend
                        iconType="circle"
                        iconSize={8}
                        wrapperStyle={{ fontSize: 13, paddingTop: 8 }}
                    />
                )}
                {summary.segments.map((segment, i) => (
                    <Bar
                        key={segment.accountId}
                        dataKey={segment.accountId}
                        name={segment.accountName}
                        stackId="period"
                        fill={chartColorByIndex(i)}
                        isAnimationActive={false}
                    />
                ))}
            </BarChart>
        </ResponsiveContainer>
    );
}

type SummaryTooltipProps = Partial<TooltipContentProps<number, string>> & {
    bucket: RegisterSummaryBucketSize;
    currencyCode: string;
    catalog: CurrencyCatalog;
};

function SummaryTooltip({
    active,
    payload,
    label,
    bucket,
    currencyCode,
    catalog,
}: SummaryTooltipProps) {
    if (!active || !payload || payload.length === 0) return null;

    // Zero-filled rows put every segment in the payload; only money that
    // actually moved is informative.
    const moved = payload.filter(item => (Number(item.value) || 0) !== 0);
    if (moved.length === 0) return null;
    const total = moved.reduce((sum, item) => sum + (Number(item.value) || 0), 0);
    const heading = typeof label === 'string' ? formatBucketTooltipDate(label, bucket) : '';

    return (
        <ChartTooltipShell heading={bucket === 'Week' ? <Trans>Week of {heading}</Trans> : heading}>
            {moved.map(item => (
                <ChartTooltipRow
                    key={String(item.dataKey)}
                    color={item.color}
                    name={String(item.name ?? '')}
                    value={formatMoney(Number(item.value) || 0, currencyCode, catalog, {
                        sign: true,
                    })}
                />
            ))}
            {moved.length > 1 && (
                <ChartTooltipTotalRow
                    name={<Trans>Net</Trans>}
                    value={formatMoney(total, currencyCode, catalog, { sign: true })}
                />
            )}
        </ChartTooltipShell>
    );
}
