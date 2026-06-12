import { msg } from '@lingui/core/macro';
import { type MessageDescriptor } from '@lingui/core';
import { Plural, Trans, useLingui } from '@lingui/react/macro';
import { Link } from '@tanstack/react-router';
import { useState } from 'react';
import { accountIdentifier, useAccounts, type Account } from '../api/accounts';
import { useCurrencyCatalog } from '../api/currencies';
import {
    useAccountBalanceTrend,
    useDashboardRegisterPreviews,
    useDashboardSummary,
    TREND_RANGES,
    type RegisterPreviewRow,
    type TrendRange,
} from '../api/dashboard';
import { AccountAvatar } from '../components/AccountAvatar';
import { Amount } from '../components/Amount';
import { ErrorState } from '../components/ErrorState';
import { MtdDeltaChip } from '../components/MtdDeltaChip';
import { Panel, SectionHead } from '../components/Panel';
import { Skeleton } from '../components/Skeleton';
import { TrendChart } from '../components/TrendChart';
import { selectedKey } from '../components/ui/selection';
import { ToggleButton, ToggleButtonGroup } from '../components/ui/ToggleButtonGroup';
import { cx } from '../lib/cx';
import { isLedgerAccount } from '../lib/domain';
import { formatMoney } from '../lib/money';

function PreviewRow({ row }: { row: RegisterPreviewRow }) {
    const catalog = useCurrencyCatalog();
    const label = row.counterpartyName ?? row.entryDescription ?? row.lineDescription ?? '—';
    const negative = row.amount.amount < 0;
    return (
        <div className="flex items-center justify-between gap-2">
            <span className="text-xs text-fg-2 truncate">{label}</span>
            <span
                className={cx(
                    'font-mono text-xs tabular-nums',
                    negative ? 'text-fg-2' : 'text-success',
                )}
            >
                {formatMoney(row.amount.amount, row.amount.currencyCode, catalog, { sign: true })}
            </span>
        </div>
    );
}

function RegisterPreview({ rows, isPending }: { rows: RegisterPreviewRow[]; isPending: boolean }) {
    if (isPending) {
        return (
            <div className="pl-12 flex flex-col gap-1">
                <Skeleton className="h-3 w-2/3" />
                <Skeleton className="h-3 w-1/2" />
            </div>
        );
    }

    if (rows.length === 0) {
        return null;
    }

    return (
        <div className="pl-12 flex flex-col gap-1">
            {rows.map(r => (
                <PreviewRow key={r.journalLineId} row={r} />
            ))}
        </div>
    );
}

function AccountRow({
    account,
    previewRows,
    previewPending,
}: {
    account: Account;
    previewRows: RegisterPreviewRow[];
    previewPending: boolean;
}) {
    const identifier = accountIdentifier(account);
    const isNegative = account.balance.amount < 0;
    return (
        <div className="py-3 first:pt-0 last:pb-0 flex flex-col gap-2 border-b border-border-soft last:border-b-0">
            <div className="flex items-center gap-3">
                <AccountAvatar account={account} size="md" />
                <div className="flex flex-col gap-[2px] flex-1 min-w-0">
                    <span className="text-sm font-medium text-fg-1 truncate">{account.name}</span>
                    <span className="text-xs text-fg-3 truncate">
                        {account.type}
                        {identifier ? ` · ${identifier}` : ''}
                    </span>
                </div>
                <Amount
                    minor={account.balance.amount}
                    currencyCode={account.balance.currencyCode}
                    size="inline"
                    decimals={false}
                    className={isNegative ? 'text-danger' : ''}
                />
            </div>
            <RegisterPreview rows={previewRows} isPending={previewPending} />
        </div>
    );
}

function AccountsPanel() {
    const { t } = useLingui();
    const accounts = useAccounts();
    // One batched request for every account card's Register preview: fanning out one
    // register call per account piles up on resource-constrained hosts.
    const previews = useDashboardRegisterPreviews();

    if (accounts.isPending) {
        return (
            <div className="flex flex-col gap-3">
                <Skeleton className="h-12 w-full" />
                <Skeleton className="h-12 w-full" />
                <Skeleton className="h-12 w-full" />
            </div>
        );
    }

    if (accounts.isError) {
        return (
            <ErrorState
                message={t`Couldn't load accounts.`}
                onRetry={() => void accounts.refetch()}
            />
        );
    }

    // Postable leaves only — branch accounts have no register of their own, their
    // balance is just the roll-up of their descendants (ADR-0019).
    // Sorted by code then name (numeric-aware), matching the sidebar's ordering.
    const ledgerAccounts = accounts.data
        .filter(a => isLedgerAccount(a) && a.isPostable)
        .sort(
            (a, b) =>
                a.code.localeCompare(b.code, undefined, { numeric: true }) ||
                a.name.localeCompare(b.name),
        );

    if (ledgerAccounts.length === 0) {
        return (
            <span className="text-sm text-fg-3">
                <Trans>No accounts yet.</Trans>
            </span>
        );
    }

    return (
        <div>
            {previews.isError && (
                <ErrorState
                    message={t`Couldn't load recent activity.`}
                    onRetry={() => void previews.refetch()}
                />
            )}
            {ledgerAccounts.map(a => (
                <AccountRow
                    key={a.id}
                    account={a}
                    previewRows={previews.data?.get(a.id) ?? []}
                    previewPending={previews.isPending}
                />
            ))}
        </div>
    );
}

function KpiStrip() {
    const { t } = useLingui();
    const summary = useDashboardSummary();
    const accounts = useAccounts();

    if (summary.isPending) {
        return (
            <section className="grid gap-[14px] grid-cols-1 sm:grid-cols-[1.3fr_1fr_1fr]">
                <Panel padding="sm" className="flex flex-col gap-1 justify-between min-h-[120px]">
                    <span className="text-xs font-medium text-fg-3 tracking-widest uppercase truncate">
                        <Trans>Net worth</Trans>
                    </span>
                    <Skeleton className="h-[44px] w-[180px]" />
                </Panel>
                <Panel padding="sm" className="flex flex-col gap-1 justify-between min-h-[120px]">
                    <span className="text-xs font-medium text-fg-3 tracking-widest uppercase truncate">
                        <Trans>Income · MTD</Trans>
                    </span>
                    <Skeleton className="h-[22px] w-[120px]" />
                </Panel>
                <Panel padding="sm" className="flex flex-col gap-1 justify-between min-h-[120px]">
                    <span className="text-xs font-medium text-fg-3 tracking-widest uppercase truncate">
                        <Trans>Expenses · MTD</Trans>
                    </span>
                    <Skeleton className="h-[22px] w-[120px]" />
                </Panel>
            </section>
        );
    }

    if (summary.isError) {
        return (
            <section>
                <ErrorState
                    message={t`Couldn't load the dashboard summary.`}
                    onRetry={() => void summary.refetch()}
                />
            </section>
        );
    }

    const data = summary.data;
    // Postable leaves only, matching the Accounts panel — counting branch roll-ups
    // alongside their leaves would double-count.
    const accountCount = accounts.data?.filter(a => isLedgerAccount(a) && a.isPostable).length;
    const subtext =
        accountCount !== undefined ? (
            <Trans>
                Across <Plural value={accountCount} one="# account" other="# accounts" />
            </Trans>
        ) : (
            ' '
        );

    // The liquid-headline form only kicks in once an illiquid account exists — comparing
    // the two amounts instead would misfire when a house exactly offsets its mortgage.
    const hasIlliquid = accounts.data?.some(a => isLedgerAccount(a) && !a.isLiquid) ?? false;
    const headline = hasIlliquid ? data.liquidNetWorth : data.netWorth;

    return (
        <section className="grid gap-[14px] grid-cols-1 sm:grid-cols-[1.3fr_1fr_1fr]">
            <Panel padding="sm" className="flex flex-col gap-1 justify-between min-h-[120px]">
                <span className="text-xs font-medium text-fg-3 tracking-widest uppercase truncate">
                    {hasIlliquid ? t`Liquid net worth` : t`Net worth`}
                </span>
                <Amount
                    minor={headline.amount}
                    currencyCode={headline.currencyCode}
                    size="big"
                    className={headline.amount < 0 ? 'text-danger' : ''}
                />
                {hasIlliquid ? (
                    <span className="text-sm text-fg-3 inline-flex items-baseline gap-[0.35em]">
                        <Trans>Net worth</Trans>
                        <Amount
                            minor={data.netWorth.amount}
                            currencyCode={data.netWorth.currencyCode}
                            size="inline"
                            className={data.netWorth.amount < 0 ? 'text-danger' : ''}
                        />
                    </span>
                ) : (
                    <span className="text-sm text-fg-3">{subtext}</span>
                )}
            </Panel>

            <Panel padding="sm" className="flex flex-col gap-1 justify-between min-h-[120px]">
                <span className="text-xs font-medium text-fg-3 tracking-widest uppercase truncate">
                    <Trans>Income · MTD</Trans>
                </span>
                <Amount
                    minor={data.incomeMtd.amount}
                    currencyCode={data.incomeMtd.currencyCode}
                    size="medium"
                />
                <MtdDeltaChip
                    current={data.incomeMtd}
                    prior={data.incomeMtdPrior}
                    polarity="higher-is-good"
                />
            </Panel>

            <Panel padding="sm" className="flex flex-col gap-1 justify-between min-h-[120px]">
                <span className="text-xs font-medium text-fg-3 tracking-widest uppercase truncate">
                    <Trans>Expenses · MTD</Trans>
                </span>
                <Amount
                    minor={data.expensesMtd.amount}
                    currencyCode={data.expensesMtd.currencyCode}
                    size="medium"
                    className={data.expensesMtd.amount < 0 ? 'text-danger' : ''}
                />
                <MtdDeltaChip
                    current={data.expensesMtd}
                    prior={data.expensesMtdPrior}
                    polarity="lower-is-good"
                />
            </Panel>
        </section>
    );
}

const RANGE_SUBTITLE: Record<TrendRange, MessageDescriptor> = {
    '1M': msg`Balance over time · Last month`,
    '3M': msg`Balance over time · Last 3 months`,
    '6M': msg`Balance over time · Last 6 months`,
    '1Y': msg`Balance over time · Last year`,
};

function AccountBalanceTrendPanel() {
    const { t, i18n } = useLingui();
    const [range, setRange] = useState<TrendRange>('3M');
    const trend = useAccountBalanceTrend(range);
    // Lives here (not in TrendChart) so toggles survive a range switch, during
    // which TrendChart briefly unmounts behind the loading skeleton. Keyed by
    // account id, so the same line stays hidden across ranges.
    const [hiddenAccountIds, setHiddenAccountIds] = useState<Set<string>>(() => new Set());
    const toggleSeries = (accountId: string) => {
        setHiddenAccountIds(prev => {
            const next = new Set(prev);
            if (next.has(accountId)) next.delete(accountId);
            else next.add(accountId);
            return next;
        });
    };

    const pills = (
        <ToggleButtonGroup
            aria-label={t`Trend range`}
            disallowEmptySelection
            selectedKeys={[range]}
            onSelectionChange={keys => {
                const next = selectedKey(keys);
                if (next !== undefined) setRange(next as TrendRange);
            }}
        >
            {TREND_RANGES.map(p => (
                <ToggleButton key={p} id={p}>
                    {p}
                </ToggleButton>
            ))}
        </ToggleButtonGroup>
    );

    return (
        <Panel>
            <SectionHead
                title={<Trans>Account balances</Trans>}
                subtitle={i18n._(RANGE_SUBTITLE[range])}
                action={pills}
            />
            {trend.isPending ? (
                <Skeleton className="h-[240px] w-full" />
            ) : trend.isError ? (
                <ErrorState
                    message={t`Couldn't load the balance trend.`}
                    onRetry={() => void trend.refetch()}
                />
            ) : trend.data.series.length === 0 ? (
                <div className="h-[240px] flex items-center justify-center text-sm text-fg-3">
                    <Trans>No balance history yet.</Trans>
                </div>
            ) : (
                <TrendChart
                    series={trend.data.series}
                    range={range}
                    currencyCode={trend.data.currencyCode}
                    height={240}
                    hiddenAccountIds={hiddenAccountIds}
                    onToggleSeries={toggleSeries}
                />
            )}
        </Panel>
    );
}

export function Dashboard() {
    return (
        <>
            <KpiStrip />

            {/* Trend over accounts, stacked full-width — a short chart beside a
                long accounts list left a large empty quadrant. */}
            <section className="grid gap-[18px] grid-cols-1">
                <AccountBalanceTrendPanel />

                <Panel>
                    <SectionHead
                        title={<Trans>Accounts</Trans>}
                        action={
                            <Link
                                to="/accounts"
                                className="text-sm font-medium text-fg-2 hover:text-brand-primary"
                            >
                                <Trans>All →</Trans>
                            </Link>
                        }
                    />
                    <AccountsPanel />
                </Panel>
            </section>
        </>
    );
}
