import { Link } from '@tanstack/react-router';
import { useAccounts, type Account } from '../api/accounts';
import { useDashboardSummary } from '../api/dashboard';
import { useAccountRegister, type RegisterRow } from '../api/register';
import { Amount } from '../components/Amount';
import { ErrorState } from '../components/ErrorState';
import { Icon } from '../components/Icon';
import { MtdDeltaChip } from '../components/MtdDeltaChip';
import { Panel, SectionHead } from '../components/Panel';
import { Skeleton } from '../components/Skeleton';
import { TrendChart } from '../components/TrendChart';
import { TREND } from '../demo/trend';
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
                className={[
                    'font-mono text-[11px] tabular',
                    negative ? 'text-fg-2' : 'text-success',
                ].join(' ')}
            >
                {formatMoney(row.amount.amount, row.amount.currencyCode, { sign: true })}
            </span>
        </div>
    );
}

function RecentActivity({ account }: { account: Account }) {
    const register = useAccountRegister(account.id, 2);

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
        return (
            <ErrorState
                message="Couldn't load accounts."
                onRetry={() => accounts.refetch()}
            />
        );
    }

    // Only render Asset and Liability accounts — Equity / Income / Expense are
    // bookkeeping plumbing the user doesn't think of as "their accounts".
    const ledgerAccounts = accounts.data.filter(
        a => a.type === 'Asset' || a.type === 'Liability',
    );

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
                <Panel className="!p-[18px] flex flex-col gap-1 justify-between min-h-[120px]">
                    <span className="eyebrow truncate">Net worth</span>
                    <Skeleton className="h-[44px] w-[180px]" />
                </Panel>
                <Panel className="!p-[18px] flex flex-col gap-1 justify-between min-h-[120px]">
                    <span className="eyebrow truncate">Income · MTD</span>
                    <Skeleton className="h-[22px] w-[120px]" />
                </Panel>
                <Panel className="!p-[18px] flex flex-col gap-1 justify-between min-h-[120px]">
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
    const accountCount = accounts.data?.filter(
        a => a.type === 'Asset' || a.type === 'Liability',
    ).length;
    const subtext =
        accountCount !== undefined
            ? `Across ${accountCount} ${accountCount === 1 ? 'account' : 'accounts'}`
            : ' ';

    return (
        <section className="grid gap-[14px]" style={{ gridTemplateColumns: '1.3fr 1fr 1fr' }}>
            <Panel className="!p-[18px] flex flex-col gap-1 justify-between min-h-[120px]">
                <span className="eyebrow truncate">Net worth</span>
                <Amount
                    minor={data.netWorth.amount}
                    currencyCode={data.netWorth.currencyCode}
                    size="big"
                    className={data.netWorth.amount < 0 ? 'text-danger' : ''}
                />
                <span className="text-14 text-fg-3">{subtext}</span>
            </Panel>

            <Panel className="!p-[18px] flex flex-col gap-1 justify-between min-h-[120px]">
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

            <Panel className="!p-[18px] flex flex-col gap-1 justify-between min-h-[120px]">
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

export function Dashboard() {
    return (
        <>
            <KpiStrip />

            {/* Trend + accounts */}
            <section className="grid gap-[18px]" style={{ gridTemplateColumns: '1.4fr 1fr' }}>
                <Panel>
                    <SectionHead
                        title="Your accounts"
                        subtitle="Balance over time · May"
                        action={
                            <div className="flex items-center gap-[6px]">
                                {['1M', '3M', '6M', '1Y'].map(p => (
                                    <span
                                        key={p}
                                        className={[
                                            'px-[10px] py-[5px] rounded-full text-[11px] font-medium select-none cursor-pointer',
                                            p === '3M'
                                                ? 'bg-brand-primary-soft text-brand-primary'
                                                : 'text-fg-3 hover:text-fg-1',
                                        ].join(' ')}
                                    >
                                        {p}
                                    </span>
                                ))}
                            </div>
                        }
                    />
                    {/* Trend stays demo-driven — running-balance projection is a later slice. */}
                    <TrendChart series={TREND} days={30} today={15} height={240} />
                    <div className="mt-3 flex flex-wrap gap-x-5 gap-y-2">
                        {TREND.map(s => (
                            <div key={s.accountId} className="flex items-center gap-2 text-14 text-fg-2">
                                <span className="w-2 h-2 rounded-full" style={{ background: s.accentColor }} />
                                <span>{s.name}</span>
                            </div>
                        ))}
                    </div>
                </Panel>

                <Panel>
                    <SectionHead
                        title="Accounts"
                        action={
                            <Link to="/accounts" className="text-[13px] font-medium text-fg-2 hover:text-brand-primary">
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
