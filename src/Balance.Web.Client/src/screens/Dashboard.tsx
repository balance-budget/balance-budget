import { Link } from '@tanstack/react-router';
import { Amount } from '../components/Amount';
import { BudgetBar } from '../components/BudgetBar';
import { Icon } from '../components/Icon';
import { Panel, SectionHead } from '../components/Panel';
import { TrendChart } from '../components/TrendChart';
import { ACCOUNTS, ACCOUNT_SAVINGS } from '../demo/accounts';
import { BUDGETS } from '../demo/budgets';
import { ENTRIES } from '../demo/entries';
import { SUBSCRIPTIONS } from '../demo/subscriptions';
import { TREND } from '../demo/trend';
import { formatMoney } from '../lib/money';
import type { AccountId, AccountSummary, JournalEntrySummary } from '../lib/domain';

const CURRENCY = 'EUR';

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

export function Dashboard() {
    const netWorthMinor = ACCOUNTS.reduce((sum, a) => sum + a.balanceMinor, 0);
    const incomeMinor = ENTRIES
        .filter(e => e.amountMinor > 0)
        .reduce((sum, e) => sum + e.amountMinor, 0);
    const expensesMinor = ENTRIES
        .filter(e => e.amountMinor < 0 && e.categoryAccountId !== ACCOUNT_SAVINGS)
        .reduce((sum, e) => sum + e.amountMinor, 0);
    const subsMonthlyMinor = SUBSCRIPTIONS.reduce((sum, s) => sum + s.amountMinor, 0);

    return (
        <>
            {/* KPI strip */}
            <section className="grid gap-[14px]" style={{ gridTemplateColumns: '1.3fr 1fr 1fr 1fr' }}>
                <Panel className="!p-[18px] flex flex-col gap-1 justify-between min-h-[120px]">
                    <span className="eyebrow truncate">Net worth</span>
                    <Amount minor={netWorthMinor} currencyCode={CURRENCY} size="big" />
                    <div className="flex items-center gap-[10px] flex-wrap">
                        <span className="inline-flex items-center gap-[5px] px-[9px] py-[3px] rounded-full text-[11px] font-semibold leading-none tracking-[0.02em] bg-success-soft text-success">
                            <Icon name="trending-up" size={12} strokeWidth={2} />
                            +€340 this month
                        </span>
                        <span className="text-14 text-fg-3">Across {ACCOUNTS.length} accounts</span>
                    </div>
                </Panel>

                <Panel className="!p-[18px] flex flex-col gap-1 justify-between min-h-[120px]">
                    <span className="eyebrow truncate">Income · MTD</span>
                    <Amount minor={incomeMinor} currencyCode={CURRENCY} size="medium" />
                    <span className="text-14 text-fg-3">1 deposit</span>
                </Panel>

                <Panel className="!p-[18px] flex flex-col gap-1 justify-between min-h-[120px]">
                    <span className="eyebrow truncate">Expenses · MTD</span>
                    <Amount minor={expensesMinor} currencyCode={CURRENCY} size="medium" />
                    <span className="text-14 text-danger">▼ 8% vs April</span>
                </Panel>

                <Panel className="!p-[18px] flex flex-col gap-1 justify-between min-h-[120px]">
                    <span className="eyebrow truncate">Subscriptions</span>
                    <Amount minor={-subsMonthlyMinor} currencyCode={CURRENCY} size="medium" />
                    <span className="text-14 text-fg-3">{SUBSCRIPTIONS.length} active · monthly</span>
                </Panel>
            </section>

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

            {/* Budgets + subscriptions */}
            <section className="grid gap-[18px]" style={{ gridTemplateColumns: '1.4fr 1fr' }}>
                <Panel>
                    <SectionHead
                        title="Budgets"
                        action={
                            <Link to="/budgets" className="text-[13px] font-medium text-fg-2 hover:text-brand-primary">
                                All →
                            </Link>
                        }
                    />
                    <div className="flex flex-col">
                        {BUDGETS.slice(0, 4).map(b => <BudgetBar key={b.id} budget={b} />)}
                    </div>
                </Panel>

                <Panel>
                    <SectionHead
                        title="Upcoming subscriptions"
                        action={
                            <Link to="/subscriptions" className="text-[13px] font-medium text-fg-2 hover:text-brand-primary">
                                All →
                            </Link>
                        }
                    />
                    <div className="flex flex-col gap-3">
                        {SUBSCRIPTIONS.slice(0, 5).map(s => (
                            <div key={s.id} className="flex items-center gap-3">
                                <span
                                    className="w-9 h-9 rounded-md flex items-center justify-center shrink-0"
                                    style={{
                                        background: 'color-mix(in srgb, var(--color-cat-bills) 12%, transparent)',
                                        color: 'var(--color-cat-bills)',
                                    }}
                                >
                                    <Icon name={s.iconName} size={16} strokeWidth={2} />
                                </span>
                                <div className="flex flex-col gap-[2px] flex-1 min-w-0">
                                    <span className="text-14 font-medium text-fg-1 truncate">{s.counterpartyName}</span>
                                    <span className="text-[12px] text-fg-3 truncate">
                                        {s.cadence} · next {s.nextDate}
                                    </span>
                                </div>
                                <span className="font-mono text-[12px] text-fg-2 tabular">
                                    {formatMoney(-s.amountMinor, s.currencyCode)}
                                </span>
                            </div>
                        ))}
                    </div>
                </Panel>
            </section>
        </>
    );
}
