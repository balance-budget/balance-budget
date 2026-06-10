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
import {
    effectiveSummaryRange,
    summaryBucketFor,
    type RegisterSummaryBucketSize,
} from '../lib/registerSummary';
import { chartBaseColorForType, shadeOf } from '../lib/visualHints';
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

    // Same-hue shade ramp as the distribution donut: every segment shares the
    // subtree's AccountType, so the type accent with per-segment shading reads
    // better than a grab-bag palette.
    const baseColor = chartBaseColorForType(account.type);

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
                    domain={['auto', 'auto']}
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
                        fill={shadeOf(baseColor, i, summary.segments.length)}
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
        <div className="rounded-xl border border-border-soft bg-bg-1 px-3 py-2 shadow-sm text-xs">
            <div className="text-fg-3 mb-1">
                {bucket === 'Week' ? <Trans>Week of {heading}</Trans> : heading}
            </div>
            <div className="flex flex-col gap-1">
                {moved.map(item => (
                    <div
                        key={String(item.dataKey)}
                        className="flex items-center justify-between gap-x-4"
                    >
                        <span className="flex items-center gap-1.5">
                            <span
                                className="w-2 h-2 rounded-full inline-block"
                                style={{ background: item.color }}
                            />
                            <span className="text-fg-2">{String(item.name ?? '')}</span>
                        </span>
                        <span className="font-mono tabular-nums text-fg-1">
                            {formatMoney(Number(item.value) || 0, currencyCode, catalog, {
                                sign: true,
                            })}
                        </span>
                    </div>
                ))}
                {moved.length > 1 && (
                    <div className="flex items-center justify-between gap-x-4 mt-1 pt-1 border-t border-border-soft">
                        <span className="text-fg-2">
                            <Trans>Net</Trans>
                        </span>
                        <span className="font-mono tabular-nums text-fg-1">
                            {formatMoney(total, currencyCode, catalog, { sign: true })}
                        </span>
                    </div>
                )}
            </div>
        </div>
    );
}
