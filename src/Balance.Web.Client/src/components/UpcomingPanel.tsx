import { Trans, useLingui } from '@lingui/react/macro';
import { Link } from '@tanstack/react-router';
import { useCurrencyCatalog } from '../api/currencies';
import { useOutlookProjection, type OutlookExpectedItem } from '../api/outlook';
import { Amount } from './Amount';
import { ErrorState } from './ErrorState';
import { Panel, SectionHead } from './Panel';
import { Skeleton } from './Skeleton';
import { cx } from '../lib/cx';
import { formatTableDate } from '../lib/dates';
import { formatMoney } from '../lib/money';

// Single-currency posture for now (matches the server-side dashboard defaults).
const CURRENCY = 'EUR';

/**
 * What's still due before month-end (ADR-0030), flattened across liquid accounts from the Outlook
 * projection and led by the projected month-end liquid balance. Reuses the Outlook engine as-is, so
 * it thins out late in the month — hence the "all settled" empty state.
 */
export function UpcomingPanel() {
    const { t } = useLingui();
    const catalog = useCurrencyCatalog();
    // Only this month is needed; a one-month horizon keeps the projection cheap.
    const projection = useOutlookProjection(CURRENCY, 1, null);

    return (
        <Panel>
            <SectionHead
                title={<Trans>Upcoming</Trans>}
                subtitle={t`Rest of this month`}
                action={
                    <Link
                        to="/outlook"
                        className="text-sm font-medium text-fg-2 hover:text-brand-primary"
                    >
                        <Trans>Outlook →</Trans>
                    </Link>
                }
            />
            {projection.isPending ? (
                <div className="flex flex-col gap-3">
                    <Skeleton className="h-4 w-1/2" />
                    <Skeleton className="h-4 w-full" />
                    <Skeleton className="h-4 w-2/3" />
                </div>
            ) : projection.isError ? (
                <ErrorState
                    message={t`Couldn't load upcoming items.`}
                    onRetry={() => void projection.refetch()}
                />
            ) : (
                <UpcomingList
                    items={projection.data.accounts
                        .flatMap(a => a.thisMonth.items)
                        .sort((a, b) => a.dueDate.localeCompare(b.dueDate))}
                    projectedLiquidMinor={projection.data.accounts.reduce(
                        (sum, a) => sum + a.thisMonth.endBalanceMid,
                        0,
                    )}
                    catalog={catalog}
                    projectedLabel={t`Projected month-end liquid`}
                />
            )}
        </Panel>
    );
}

type UpcomingListProps = {
    items: OutlookExpectedItem[];
    projectedLiquidMinor: number;
    catalog: ReturnType<typeof useCurrencyCatalog>;
    projectedLabel: string;
};

function UpcomingList({ items, projectedLiquidMinor, catalog, projectedLabel }: UpcomingListProps) {
    return (
        <div className="flex flex-col gap-3">
            <div className="flex items-baseline justify-between">
                <span className="text-xs font-medium text-fg-3 tracking-widest uppercase">
                    {projectedLabel}
                </span>
                <Amount minor={projectedLiquidMinor} currencyCode={CURRENCY} size="medium" />
            </div>

            {items.length === 0 ? (
                <div className="py-4 text-center text-sm text-fg-3">
                    <Trans>All settled for this month.</Trans>
                </div>
            ) : (
                <div className="flex flex-col gap-2">
                    {items.map((item, i) => {
                        const inflow = item.amount >= 0;
                        return (
                            <div
                                key={`${item.dueDate}-${item.name}-${i}`}
                                className="flex items-center justify-between gap-3"
                            >
                                <span className="flex items-baseline gap-2 min-w-0">
                                    <span className="font-mono text-xs tabular-nums text-fg-3 shrink-0">
                                        {formatTableDate(item.dueDate)}
                                    </span>
                                    <span className="text-sm text-fg-2 truncate">
                                        {item.counterpartyName ?? item.name}
                                    </span>
                                </span>
                                <span
                                    className={cx(
                                        'font-mono text-xs tabular-nums shrink-0',
                                        inflow ? 'text-success' : 'text-fg-2',
                                    )}
                                >
                                    {formatMoney(item.amount, CURRENCY, catalog, { sign: true })}
                                </span>
                            </div>
                        );
                    })}
                </div>
            )}
        </div>
    );
}
