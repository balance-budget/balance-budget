import { useMemo, useState } from 'react';
import { Pie, PieChart, ResponsiveContainer, Tooltip } from 'recharts';
import { useCurrencyCatalog } from '../api/currencies';
import {
    useDistribution,
    type Distribution,
    type DistributionSlice,
    type DistributionType,
} from '../api/reports';
import { cx } from '../lib/cx';
import type { AccountId } from '../lib/domain';
import { formatMoney } from '../lib/money';
import type { ReportPeriod } from '../lib/reportPeriod';
import { chartColorFor } from '../lib/visualHints';
import { ErrorState } from './ErrorState';
import { Panel, SectionHead } from './Panel';
import { Skeleton } from './Skeleton';

type DistributionChartProps = {
    period: ReportPeriod;
    currency: string;
};

type Crumb = { id: AccountId; name: string };

const TYPES: { token: DistributionType; label: string }[] = [
    { token: 'expense', label: 'Expenses' },
    { token: 'income', label: 'Income' },
];

export function DistributionChart({ period, currency }: DistributionChartProps) {
    const [type, setType] = useState<DistributionType>('expense');
    const [trail, setTrail] = useState<Crumb[]>([]);
    const parentId = trail.at(-1)?.id ?? null;

    const distribution = useDistribution(type, period, currency, parentId);

    const switchType = (next: DistributionType) => {
        setType(next);
        setTrail([]);
    };

    const drillInto = (slice: DistributionSlice) => {
        if (slice.hasChildren)
            setTrail(prev => [...prev, { id: slice.accountId, name: slice.name }]);
    };

    const jumpTo = (index: number) => {
        // index -1 = root; otherwise keep the trail up to and including index.
        setTrail(prev => (index < 0 ? [] : prev.slice(0, index + 1)));
    };

    const toggle = (
        <div className="flex items-center gap-[6px]">
            {TYPES.map(t => (
                <button
                    key={t.token}
                    type="button"
                    onClick={() => {
                        switchType(t.token);
                    }}
                    className={cx(
                        'px-[10px] py-[5px] rounded-full text-11 font-medium select-none',
                        t.token === type
                            ? 'bg-brand-primary-soft text-brand-primary'
                            : 'text-fg-3 hover:text-fg-1',
                    )}
                >
                    {t.label}
                </button>
            ))}
        </div>
    );

    return (
        <Panel>
            <SectionHead
                title="Distribution"
                subtitle={<Breadcrumbs type={type} trail={trail} onJump={jumpTo} />}
                action={toggle}
            />
            {distribution.isPending ? (
                <Skeleton className="h-[280px] w-full" />
            ) : distribution.isError ? (
                <ErrorState
                    message="Couldn't load the distribution."
                    onRetry={() => void distribution.refetch()}
                />
            ) : (
                <DistributionBody
                    data={distribution.data}
                    currency={currency}
                    onDrill={drillInto}
                />
            )}
        </Panel>
    );
}

function Breadcrumbs({
    type,
    trail,
    onJump,
}: {
    type: DistributionType;
    trail: Crumb[];
    onJump: (index: number) => void;
}) {
    const rootLabel = type === 'income' ? 'All income' : 'All expenses';
    return (
        <span className="flex flex-wrap items-center gap-1 text-14 text-fg-3">
            <button
                type="button"
                onClick={() => {
                    onJump(-1);
                }}
                className={cx(trail.length > 0 && 'hover:text-brand-primary')}
                disabled={trail.length === 0}
            >
                {rootLabel}
            </button>
            {trail.map((crumb, i) => (
                <span key={crumb.id} className="flex items-center gap-1">
                    <span className="text-fg-3">/</span>
                    <button
                        type="button"
                        onClick={() => {
                            onJump(i);
                        }}
                        className={cx(
                            i < trail.length - 1 ? 'hover:text-brand-primary' : 'text-fg-1',
                        )}
                        disabled={i === trail.length - 1}
                    >
                        {crumb.name}
                    </button>
                </span>
            ))}
        </span>
    );
}

function DistributionBody({
    data,
    currency,
    onDrill,
}: {
    data: Distribution;
    currency: string;
    onDrill: (slice: DistributionSlice) => void;
}) {
    const catalog = useCurrencyCatalog();

    const positive = useMemo(() => data.slices.filter(s => s.amount.amount > 0), [data.slices]);
    const negative = useMemo(() => data.slices.filter(s => s.amount.amount < 0), [data.slices]);
    const positiveTotal = positive.reduce((sum, s) => sum + s.amount.amount, 0);

    const pieData = positive.map(s => ({
        name: s.name,
        value: s.amount.amount,
        // recharts spreads each datum onto its sector, so a per-entry `fill`
        // colours the slice — no deprecated <Cell> needed.
        fill: chartColorFor(s.accountId),
    }));

    if (positive.length === 0 && negative.length === 0) {
        return (
            <div className="h-[280px] flex items-center justify-center text-13 text-fg-3">
                No money moved in this period.
            </div>
        );
    }

    return (
        <div className="grid gap-5 grid-cols-1 md:grid-cols-[240px_1fr] items-center">
            <div className="relative h-[240px]">
                {pieData.length > 0 ? (
                    <ResponsiveContainer width="100%" height="100%">
                        <PieChart>
                            <Pie
                                data={pieData}
                                dataKey="value"
                                nameKey="name"
                                innerRadius={64}
                                outerRadius={104}
                                paddingAngle={1}
                                stroke="var(--color-bg-1)"
                                strokeWidth={2}
                                isAnimationActive={false}
                            />
                            <Tooltip
                                formatter={(value, name) =>
                                    [
                                        formatMoney(Number(value), currency, catalog),
                                        String(name),
                                    ] as [string, string]
                                }
                                contentStyle={{
                                    background: 'var(--color-bg-1)',
                                    border: '1px solid var(--color-border-soft)',
                                    borderRadius: 6,
                                    fontSize: 12,
                                }}
                            />
                        </PieChart>
                    </ResponsiveContainer>
                ) : (
                    <div className="h-full flex items-center justify-center text-12 text-fg-3 text-center px-4">
                        Net negative this period — see the breakdown.
                    </div>
                )}
                {pieData.length > 0 && (
                    <div className="absolute inset-0 flex flex-col items-center justify-center pointer-events-none">
                        <span className="text-11 text-fg-3">Total</span>
                        <span className="text-16 font-semibold text-fg-1">
                            {formatMoney(data.total.amount, currency, catalog, { decimals: false })}
                        </span>
                    </div>
                )}
            </div>

            <div className="flex flex-col">
                {positive.map(s => (
                    <SliceRow
                        key={s.accountId}
                        slice={s}
                        share={positiveTotal > 0 ? s.amount.amount / positiveTotal : 0}
                        currency={currency}
                        onDrill={onDrill}
                    />
                ))}
                {negative.length > 0 && (
                    <div className="mt-2 pt-2 border-t border-border-soft flex flex-col gap-1">
                        <span className="text-11 text-fg-3">
                            Net negative this period (excluded from the chart)
                        </span>
                        {negative.map(s => (
                            <SliceRow
                                key={s.accountId}
                                slice={s}
                                share={null}
                                currency={currency}
                                onDrill={onDrill}
                            />
                        ))}
                    </div>
                )}
            </div>
        </div>
    );
}

function SliceRow({
    slice,
    share,
    currency,
    onDrill,
}: {
    slice: DistributionSlice;
    share: number | null;
    currency: string;
    onDrill: (slice: DistributionSlice) => void;
}) {
    const catalog = useCurrencyCatalog();
    const negative = slice.amount.amount < 0;
    const content = (
        <>
            <span className="flex items-center gap-2 min-w-0">
                <span
                    className="w-2.5 h-2.5 rounded-full shrink-0"
                    style={{ background: chartColorFor(slice.accountId) }}
                />
                <span className="text-13 text-fg-1 truncate">{slice.name}</span>
                {slice.hasChildren && <span className="text-11 text-fg-3">›</span>}
            </span>
            <span className="flex items-center gap-3 shrink-0">
                {share !== null && (
                    <span className="text-11 text-fg-3 tabular w-10 text-right">
                        {Math.round(share * 100)}%
                    </span>
                )}
                <span
                    className={cx(
                        'font-mono text-12 tabular',
                        negative ? 'text-danger' : 'text-fg-1',
                    )}
                >
                    {formatMoney(slice.amount.amount, currency, catalog)}
                </span>
            </span>
        </>
    );

    const rowClass =
        'flex items-center justify-between gap-3 py-[7px] border-b border-border-soft last:border-b-0';

    if (slice.hasChildren) {
        return (
            <button
                type="button"
                onClick={() => {
                    onDrill(slice);
                }}
                className={cx(
                    rowClass,
                    'w-full text-left hover:bg-surface-2 rounded-sm px-1 -mx-1',
                )}
            >
                {content}
            </button>
        );
    }
    return <div className={rowClass}>{content}</div>;
}
