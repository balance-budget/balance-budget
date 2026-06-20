import { Trans, useLingui } from '@lingui/react/macro';
import { useCurrencyCatalog } from '../api/currencies';
import { useSpendingByCategory } from '../api/dashboard';
import { Amount } from './Amount';
import { ErrorState } from './ErrorState';
import { Panel, SectionHead } from './Panel';
import { Skeleton } from './Skeleton';
import { formatMoney } from '../lib/money';

const BAR_COLOR = 'var(--color-chart-amber)';

/**
 * This-month spending by leaf Expense category (ADR-0030): the flow companion to the dashboard's
 * balance charts, answering "where did my money go?". Top categories with a share-of-total bar,
 * plus an Other bucket for the tail.
 */
export function SpendingByCategoryPanel() {
    const { t } = useLingui();
    const catalog = useCurrencyCatalog();
    const spending = useSpendingByCategory();

    return (
        <Panel>
            <SectionHead title={<Trans>Spending</Trans>} subtitle={t`By category · This month`} />
            {spending.isPending ? (
                <div className="flex flex-col gap-3">
                    <Skeleton className="h-4 w-full" />
                    <Skeleton className="h-4 w-full" />
                    <Skeleton className="h-4 w-2/3" />
                </div>
            ) : spending.isError ? (
                <ErrorState
                    message={t`Couldn't load spending.`}
                    onRetry={() => void spending.refetch()}
                />
            ) : spending.data.totalMinor === 0 ? (
                <div className="py-6 text-center text-sm text-fg-3">
                    <Trans>No spending yet this month.</Trans>
                </div>
            ) : (
                <SpendingList
                    data={spending.data}
                    catalog={catalog}
                    totalLabel={t`Total`}
                    otherLabel={t`Other`}
                />
            )}
        </Panel>
    );
}

type SpendingListProps = {
    data: NonNullable<ReturnType<typeof useSpendingByCategory>['data']>;
    catalog: ReturnType<typeof useCurrencyCatalog>;
    totalLabel: string;
    otherLabel: string;
};

function SpendingList({ data, catalog, totalLabel, otherLabel }: SpendingListProps) {
    const total = data.totalMinor;
    const rows = [
        ...data.slices.map(s => ({
            key: s.accountId,
            name: s.name,
            amount: s.amountMinor,
        })),
        ...(data.otherMinor > 0
            ? [{ key: 'other', name: otherLabel, amount: data.otherMinor }]
            : []),
    ];

    return (
        <div className="flex flex-col gap-3">
            <div className="flex items-baseline justify-between">
                <span className="text-xs font-medium text-fg-3 tracking-widest uppercase">
                    {totalLabel}
                </span>
                <Amount minor={total} currencyCode={data.currencyCode} size="medium" />
            </div>
            <div className="flex flex-col gap-2">
                {rows.map(row => {
                    const pct = total > 0 ? Math.round((row.amount / total) * 100) : 0;
                    return (
                        <div key={row.key} className="flex flex-col gap-1">
                            <div className="flex items-center justify-between gap-3">
                                <span className="text-sm text-fg-2 truncate">{row.name}</span>
                                <span className="font-mono text-xs tabular-nums text-fg-1 shrink-0">
                                    {formatMoney(row.amount, data.currencyCode, catalog)}
                                </span>
                            </div>
                            <div className="h-1.5 rounded-full bg-bg-2 overflow-hidden">
                                <div
                                    className="h-full rounded-full"
                                    style={{ width: `${pct}%`, background: BAR_COLOR }}
                                />
                            </div>
                        </div>
                    );
                })}
            </div>
        </div>
    );
}
