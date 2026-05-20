import { Link } from '@tanstack/react-router';
import { useAccounts } from '../api/accounts';
import { useDashboardSummary } from '../api/dashboard';
import { Amount } from '../components/Amount';
import { ErrorState } from '../components/ErrorState';
import { Icon } from '../components/Icon';
import { Panel, SectionHead } from '../components/Panel';
import { Skeleton } from '../components/Skeleton';
import { TrendChart } from '../components/TrendChart';
import { ACCOUNTS } from '../demo/accounts';
import { ENTRIES } from '../demo/entries';
import { TREND } from '../demo/trend';
import { formatMoney } from '../lib/money';
import type { AccountId, AccountSummary, JournalEntrySummary } from '../lib/domain';

function recentEntriesForAccount(accountId: AccountId, n: number): JournalEntrySummary[] {
    return ENTRIES.filter(e => e.accountId === accountId).slice(0, n);
}

function AccountRow({ account }: { account: AccountSummary }) {
    const recent = recentEntriesForAccount(account.id, 2);
    return (
        <div className="py-3 first:pt-0 last:pb-0 flex flex-col gap-2 border-b border-border-soft last:border-b-0">
            <div className="flex items-center gap-3">
                <span
                    className="w-9 h-9 rounded-md flex items-center justify-center shrink-0"
                    style={{
                        background: `color-mix(in srgb, ${account.accentColor} 12%, transparent)`,
                        color: account.accentColor,
                    }}
                >
                    <Icon name={account.iconName} size={16} strokeWidth={2} />
                </span>
                <div className="flex flex-col gap-[2px] flex-1 min-w-0">
                    <span className="text-14 font-medium text-fg-1 truncate">{account.name}</span>
                    <span className="text-[12px] text-fg-3 truncate">
                        {account.type}
                        {account.bankAccountNumber ? ` ${account.bankAccountNumber}` : ''}
                    </span>
                </div>
                <Amount
                    minor={account.balanceMinor}
                    currencyCode={account.currencyCode}
                    size="inline"
                    decimals={false}
                    className={account.balanceMinor < 0 ? 'text-danger' : ''}
                />
            </div>
            {recent.length > 0 && (
                <div className="pl-12 flex flex-col gap-1">
                    {recent.map(e => (
                        <div key={e.id} className="flex items-center justify-between gap-2">
                            <span className="text-[12px] text-fg-2 truncate">
                                {e.counterpartyName ?? e.description ?? '—'}
                            </span>
                            <span className={['font-mono text-[11px] tabular', e.amountMinor < 0 ? 'text-fg-2' : 'text-success'].join(' ')}>
                                {formatMoney(e.amountMinor, e.currencyCode, { sign: true })}
                            </span>
                        </div>
                    ))}
                </div>
            )}
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
            : ' ';

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
            </Panel>

            <Panel className="!p-[18px] flex flex-col gap-1 justify-between min-h-[120px]">
                <span className="eyebrow truncate">Expenses · MTD</span>
                <Amount
                    minor={data.expensesMtd.amount}
                    currencyCode={data.expensesMtd.currencyCode}
                    size="medium"
                    className={data.expensesMtd.amount < 0 ? 'text-danger' : ''}
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
                    <TrendChart series={TREND} days={30} today={15} height={240} />
                    <div className="mt-3 flex flex-wrap gap-x-5 gap-y-2">
                        {TREND.map(s => {
                            const account = ACCOUNTS.find(a => a.id === s.accountId);
                            return (
                                <div key={s.accountId} className="flex items-center gap-2 text-14 text-fg-2">
                                    <span className="w-2 h-2 rounded-full" style={{ background: s.accentColor }} />
                                    <span>{s.name}</span>
                                    <span className="text-fg-3 tabular font-mono text-[12px]">
                                        {account ? formatMoney(account.balanceMinor, account.currencyCode, { decimals: false }) : ''}
                                    </span>
                                </div>
                            );
                        })}
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
                    <div>
                        {ACCOUNTS.map(a => <AccountRow key={a.id} account={a} />)}
                    </div>
                </Panel>
            </section>
        </>
    );
}
