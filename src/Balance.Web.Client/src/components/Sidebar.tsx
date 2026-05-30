import type { ReactNode } from 'react';
import { Link, useNavigate, useRouterState } from '@tanstack/react-router';
import logo from '../assets/logo.svg';
import { Icon } from './Icon';
import { accountIdentifier, useAccounts, type Account } from '../api/accounts';
import { useCurrentUser, useLogout } from '../api/auth';
import { useCurrencyCatalog } from '../api/currencies';
import { AccountAvatar } from './AccountAvatar';
import { Skeleton } from './Skeleton';
import { ErrorState } from './ErrorState';
import { cx } from '../lib/cx';
import { isLedgerAccount } from '../lib/domain';
import { formatMoney } from '../lib/money';

function SectionLabel({ children }: { children: ReactNode }) {
    return <div className="eyebrow px-3 pt-3 pb-[6px]">{children}</div>;
}

type NavLink = {
    to: string;
    label: string;
    iconName: string;
};

const NAV_MAIN: NavLink[] = [
    { to: '/', label: 'Dashboard', iconName: 'layout-dashboard' },
    { to: '/accounts', label: 'Accounts', iconName: 'wallet' },
    { to: '/counterparties', label: 'Counterparties', iconName: 'user' },
    { to: '/activity', label: 'Activity', iconName: 'book-open' },
    { to: '/bank-transactions', label: 'Bank transactions', iconName: 'inbox' },
    { to: '/bank-imports', label: 'Bank imports', iconName: 'download' },
];

const NAV_PLAN: NavLink[] = [
    { to: '/budgets', label: 'Budgets', iconName: 'line-chart' },
    { to: '/subscriptions', label: 'Subscriptions', iconName: 'repeat' },
    { to: '/piggy-banks', label: 'Piggy banks', iconName: 'piggy-bank' },
];

const NAV_OTHER: NavLink[] = [
    { to: '/reports', label: 'Reports', iconName: 'line-chart' },
    { to: '/settings', label: 'Settings', iconName: 'settings' },
];

function NavGroup({
    title,
    items,
    currentPath,
}: {
    title: string;
    items: NavLink[];
    currentPath: string;
}) {
    return (
        <div className="flex flex-col gap-[2px]">
            <SectionLabel>{title}</SectionLabel>
            {items.map(item => {
                const isActive =
                    item.to === '/' ? currentPath === '/' : currentPath.startsWith(item.to);
                return (
                    <Link
                        key={item.to}
                        to={item.to}
                        className={cx(
                            'flex items-center gap-3 px-3 py-[9px] rounded-sm select-none',
                            'text-[13.5px] font-medium transition-[background,color] duration-fast',
                            isActive
                                ? 'bg-brand-primary-soft text-brand-primary'
                                : 'text-fg-2 hover:bg-surface-2 hover:text-fg-1',
                        )}
                    >
                        <Icon
                            name={item.iconName}
                            size={18}
                            strokeWidth={1.75}
                            className="shrink-0"
                        />
                        <span>{item.label}</span>
                    </Link>
                );
            })}
        </div>
    );
}

function AccountRow({ account }: { account: Account }) {
    const catalog = useCurrencyCatalog();
    const identifier = accountIdentifier(account);
    const isNegative = account.balance.amount < 0;
    return (
        <Link
            to="/accounts/$id"
            params={{ id: account.id }}
            search={{ page: 1, q: '' }}
            className="flex items-center gap-3 px-3 py-2 rounded-sm text-fg-1 hover:bg-surface-2 transition-colors"
            activeProps={{ className: 'bg-brand-primary-soft text-brand-primary' }}
        >
            <AccountAvatar account={account} />
            <div className="flex-1 min-w-0 flex flex-col leading-tight">
                <span className="truncate text-[13px]">{account.name}</span>
                {identifier && (
                    <span className="text-[11px] text-fg-3 truncate tabular">{identifier}</span>
                )}
            </div>
            <span
                className={cx(
                    'shrink-0 text-[12px] tabular-nums',
                    isNegative ? 'text-danger' : 'text-fg-2',
                )}
            >
                {formatMoney(account.balance.amount, account.balance.currencyCode, catalog, {
                    decimals: false,
                })}
            </span>
        </Link>
    );
}

function AccountSection({ title, accounts }: { title: string; accounts: Account[] }) {
    if (accounts.length === 0) return null;
    return (
        <div className="flex flex-col gap-[2px]">
            <SectionLabel>{title}</SectionLabel>
            {accounts.map(account => (
                <AccountRow key={account.id} account={account} />
            ))}
        </div>
    );
}

function AccountsGroup() {
    const { data, isPending, isError, refetch } = useAccounts();

    if (isPending) {
        return (
            <div className="flex flex-col gap-[2px]">
                <SectionLabel>Accounts</SectionLabel>
                <div className="flex flex-col gap-[6px] px-3 py-2">
                    <Skeleton className="h-[14px] w-32" />
                    <Skeleton className="h-[14px] w-24" />
                    <Skeleton className="h-[14px] w-28" />
                </div>
            </div>
        );
    }

    if (isError) {
        return (
            <div className="flex flex-col gap-[2px]">
                <SectionLabel>Accounts</SectionLabel>
                <div className="px-3 py-2">
                    <ErrorState message="Couldn't load accounts." onRetry={() => void refetch()} />
                </div>
            </div>
        );
    }

    const ledgerAccounts = data.filter(isLedgerAccount);
    const incomeAccounts = data.filter(a => a.type === 'Income');
    const expenseAccounts = data.filter(a => a.type === 'Expense');

    if (
        ledgerAccounts.length === 0 &&
        incomeAccounts.length === 0 &&
        expenseAccounts.length === 0
    ) {
        return (
            <div className="flex flex-col gap-[2px]">
                <SectionLabel>Accounts</SectionLabel>
                <div className="px-3 py-2 text-[12px] text-fg-3">No accounts yet.</div>
            </div>
        );
    }

    return (
        <>
            <AccountSection title="Accounts" accounts={ledgerAccounts} />
            <AccountSection title="Income" accounts={incomeAccounts} />
            <AccountSection title="Expenses" accounts={expenseAccounts} />
        </>
    );
}

function initials(name: string): string {
    const parts = name.split(/\s+/).filter(Boolean);
    const first = parts[0];
    const last = parts[parts.length - 1];
    if (!first) return '?';
    if (parts.length === 1 || !last) return first.slice(0, 2).toUpperCase();
    return ((first[0] ?? '') + (last[0] ?? '')).toUpperCase();
}

function CurrentUserCard() {
    const me = useCurrentUser();
    const logout = useLogout();
    const navigate = useNavigate();

    if (!me.data) return null;
    const displayName = me.data.displayName;
    const email = me.data.email;

    async function signOut() {
        try {
            await logout.mutateAsync();
        } finally {
            await navigate({ to: '/login', replace: true });
        }
    }

    return (
        <div className="mt-auto flex items-center gap-[10px] p-[10px] rounded-sm bg-surface-2">
            <div className="w-8 h-8 rounded-full bg-brand-primary-soft text-brand-primary flex items-center justify-center text-[12px] font-semibold">
                {initials(displayName)}
            </div>
            <div className="flex-1 flex flex-col leading-tight min-w-0">
                <span className="text-[13px] font-medium text-fg-1 truncate">{displayName}</span>
                <span className="text-[14px] text-fg-3 truncate">{email}</span>
            </div>
            <button
                type="button"
                onClick={() => {
                    void signOut();
                }}
                disabled={logout.isPending}
                className="text-fg-3 hover:text-fg-1"
                aria-label="Sign out"
                title="Sign out"
            >
                <Icon name="log-out" size={16} />
            </button>
        </div>
    );
}

export function Sidebar({ open, onClose }: { open: boolean; onClose: () => void }) {
    const pathname = useRouterState({ select: s => s.location.pathname });

    return (
        <>
            <div
                aria-hidden="true"
                onClick={onClose}
                className={cx(
                    'md:hidden fixed inset-0 z-30 bg-surface-overlay backdrop-blur-sm transition-opacity duration-base',
                    open ? 'opacity-100' : 'opacity-0 pointer-events-none',
                )}
            />
            <aside
                className={cx(
                    'w-60 shrink-0 h-screen flex flex-col gap-5 px-4 py-6 border-r border-border-soft bg-surface-1 backdrop-blur-card',
                    'fixed top-0 left-0 z-40 transition-transform duration-base',
                    open ? 'translate-x-0' : '-translate-x-full',
                    'md:sticky md:top-0 md:left-auto md:z-auto md:translate-x-0 md:transition-none',
                )}
            >
                <div className="flex items-center gap-[10px] px-[10px] py-1">
                    <img src={logo} alt="" className="w-8 h-8 rounded-[6px]" />
                    <span className="text-[18px] font-normal tracking-[-0.01em]">
                        Balance<span className="text-brand-primary">.</span>
                    </span>
                </div>

                <nav className="flex flex-col gap-1 overflow-y-auto">
                    <NavGroup title="Main" items={NAV_MAIN} currentPath={pathname} />
                    <AccountsGroup />
                    <NavGroup title="Plan" items={NAV_PLAN} currentPath={pathname} />
                    <NavGroup title="Other" items={NAV_OTHER} currentPath={pathname} />
                </nav>

                <CurrentUserCard />
            </aside>
        </>
    );
}
