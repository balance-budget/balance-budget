import { Link } from '@tanstack/react-router';
import { useState } from 'react';
import { useAccounts, type Account } from '../api/accounts';
import {
    useAccountBalanceTrend,
    useDashboardSummary,
    TREND_RANGES,
    type TrendRange,
} from '../api/dashboard';
import { useAccountRegister, type RegisterRow } from '../api/register';
import { Amount } from '../components/Amount';
import { ErrorState } from '../components/ErrorState';
import { Icon } from '../components/Icon';
import { MtdDeltaChip } from '../components/MtdDeltaChip';
import { Panel, SectionHead } from '../components/Panel';
import { Skeleton } from '../components/Skeleton';
import { TrendChart } from '../components/TrendChart';
import { cx } from '../lib/cx';
import { isLedgerAccount } from '../lib/domain';
import { formatMoney } from '../lib/money';
import { visualHintFor } from '../lib/visualHints';

function lastFourIdentifier(account: Account): string | null {
    const raw = account.bankAccount?.iban ?? account.bankAccount?.accountNumber ?? null;
    if (!raw) return null;
    const compact = raw.replace(/\s+/g, '');
    return compact.length <= 4 ? compact : `· ${compact.slice(-4)}`;
}

function RecentRow({ row }: { row: RegisterRow }) {
    const label = row.counterpartyName ?? row.entryDescription ?? row.lineDescription ?? '—';
    const negative = row.amount.amount < 0;
    return (
        <div className="flex items-center justify-between gap-2">
            <span className="text-[12px] text-fg-2 truncate">{label}</span>
            <span
                className={cx(
                    'font-mono text-[11px] tabular',
                    negative ? 'text-fg-2' : 'text-success',
                )}
            >
                {formatMoney(row.amount.amount, row.amount.currencyCode, { sign: true })}
            </span>
        </div>
    );
}

function RecentActivity({ account }: { account: Account }) {
    const register = useAccountRegister(account.id, 0, 5);

    if (register.isPending) {
        return (
            <div className="pl-12 flex flex-col gap-1">
                <Skeleton className="h-3 w-2/3" />
                <Skeleton className="h-3 w-1/2" />
            </div>
        );
    }

    if (register.isError) {
        return (
            <div className="pl-12">
                <ErrorState
                    message="Couldn't load recent activity."
                    onRetry={() => register.refetch()}
                />
            </div>
        );
    }

    const rows = register.data;
    if (rows.length === 0) {
        return null;
    }

    return (
        <div className="pl-12 flex flex-col gap-1">
            {rows.map(r => (
                <RecentRow key={r.journalLineId} row={r} />
            ))}
        </div>
    );
}

function AccountRow({ account }: { account: Account }) {
    const visual = visualHintFor(account.type, account.id);
    const tail = lastFourIdentifier(account);
    const isNegative = account.balance.amount < 0;
    return (
        <div className="py-3 first:pt-0 last:pb-0 flex flex-col gap-2 border-b border-border-soft last:border-b-0">
            <div className="flex items-center gap-3">
                <span
                    className="w-9 h-9 rounded-md flex items-center justify-center shrink-0"
                    style={{
                        background: `color-mix(in srgb, ${visual.accentColor} 12%, transparent)`,
                        color: visual.accentColor,
                    }}
                >
                    <Icon name={visual.iconName} size={16} strokeWidth={2} />
                </span>
                <div className="flex flex-col gap-[2px] flex-1 min-w-0">
                    <span className="text-14 font-medium text-fg-1 truncate">{account.name}</span>
                    <span className="text-[12px] text-fg-3 truncate">
                        {account.type}
                        {tail ? ` ${tail}` : ''}
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
            <RecentActivity account={account} />
        </div>
    );
}

function AccountsPanel() {
    const accounts = useAccounts();

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
        return <ErrorState message="Couldn't load accounts." onRetry={() => accounts.refetch()} />;
    }

    const ledgerAccounts = accounts.data.filter(isLedgerAccount);

    if (ledgerAccounts.length === 0) {
        return <span className="text-[13px] text-fg-3">No accounts yet.</span>;
    }

    return (
        <div>
            {ledgerAccounts.map(a => (
                <AccountRow key={a.id} account={a} />
            ))}
        </div>
    );
}

function KpiStrip() {
    const summary = useDashboardSummary();
    const accounts = useAccounts();

    if (summary.isPending) {
        return (
            <section className="grid gap-[14px]" style={{ gridTemplateColumns: '1.3fr 1fr 1fr' }}>
                <Panel padding="sm" className="flex flex-col gap-1 justify-between min-h-[120px]">
                    <span className="eyebrow truncate">Net worth</span>
                    <Skeleton className="h-[44px] w-[180px]" />
                </Panel>
                <Panel padding="sm" className="flex flex-col gap-1 justify-between min-h-[120px]">
                    <span className="eyebrow truncate">Income · MTD</span>
                    <Skeleton className="h-[22px] w-[120px]" />
                </Panel>
                <Panel padding="sm" className="flex flex-col gap-1 justify-between min-h-[120px]">
                    <span className="eyebrow truncate">Expenses · MTD</span>
                    <Skeleton className="h-[22px] w-[120px]" />
                </Panel>
            </section>
        );
    }

    if (summary.isError) {
        return (
            <section>
                <ErrorState
                    message="Couldn't load the dashboard summary."
                    onRetry={() => summary.refetch()}
                />
            </section>
        );
    }

    const data = summary.data;
    const accountCount = accounts.data?.filter(isLedgerAccount).length;
    const subtext =
        accountCount !== undefined
            ? `Across ${accountCount} ${accountCount === 1 ? 'account' : 'accounts'}`
            : ' ';

    return (
        <section className="grid gap-[14px]" style={{ gridTemplateColumns: '1.3fr 1fr 1fr' }}>
            <Panel padding="sm" className="flex flex-col gap-1 justify-between min-h-[120px]">
                <span className="eyebrow truncate">Net worth</span>
                <Amount
                    minor={data.netWorth.amount}
                    currencyCode={data.netWorth.currencyCode}
                    size="big"
                    className={data.netWorth.amount < 0 ? 'text-danger' : ''}
                />
                <span className="text-14 text-fg-3">{subtext}</span>
            </Panel>

            <Panel padding="sm" className="flex flex-col gap-1 justify-between min-h-[120px]">
                <span className="eyebrow truncate">Income · MTD</span>
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
                <span className="eyebrow truncate">Expenses · MTD</span>
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

const RANGE_SUBTITLE: Record<TrendRange, string> = {
    '1M': 'Balance over time · Last month',
    '3M': 'Balance over time · Last 3 months',
    '6M': 'Balance over time · Last 6 months',
    '1Y': 'Balance over time · Last year',
};

function AccountBalanceTrendPanel() {
    const [range, setRange] = useState<TrendRange>('3M');
    const trend = useAccountBalanceTrend(range);

    const pills = (
        <div className="flex items-center gap-[6px]">
            {TREND_RANGES.map(p => (
                <button
                    key={p}
                    type="button"
                    onClick={() => setRange(p)}
                    className={cx(
                        'px-[10px] py-[5px] rounded-full text-[11px] font-medium select-none',
                        p === range
                            ? 'bg-brand-primary-soft text-brand-primary'
                            : 'text-fg-3 hover:text-fg-1',
                    )}
                >
                    {p}
                </button>
            ))}
        </div>
    );

    return (
        <Panel>
            <SectionHead title="Account balances" subtitle={RANGE_SUBTITLE[range]} action={pills} />
            {trend.isPending ? (
                <Skeleton className="h-[240px] w-full" />
            ) : trend.isError ? (
                <ErrorState
                    message="Couldn't load the balance trend."
                    onRetry={() => trend.refetch()}
                />
            ) : trend.data.series.length === 0 ? (
                <div className="h-[240px] flex items-center justify-center text-[13px] text-fg-3">
                    No balance history yet.
                </div>
            ) : (
                <TrendChart
                    series={trend.data.series}
                    range={range}
                    currencyCode={trend.data.currencyCode}
                    height={240}
                />
            )}
        </Panel>
    );
}

export function Dashboard() {
    return (
        <>
            <KpiStrip />

            {/* Trend + accounts */}
            <section className="grid gap-[18px]" style={{ gridTemplateColumns: '1.4fr 1fr' }}>
                <AccountBalanceTrendPanel />

                <Panel>
                    <SectionHead
                        title="Accounts"
                        action={
                            <Link
                                to="/accounts"
                                className="text-[13px] font-medium text-fg-2 hover:text-brand-primary"
                            >
                                All →
                            </Link>
                        }
                    />
                    <AccountsPanel />
                </Panel>
            </section>
        </>
    );
}
