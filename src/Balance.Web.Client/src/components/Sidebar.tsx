import { Link, useRouterState } from '@tanstack/react-router';
import logo from '../assets/logo.svg';
import { Icon } from './Icon';
import { useAccounts, type Account } from '../api/accounts';
import { Skeleton } from './Skeleton';
import { ErrorState } from './ErrorState';
import { isCategoryAccount, isLedgerAccount } from '../lib/domain';
import { formatMoney } from '../lib/money';
import { visualHintFor } from '../lib/visualHints';

type NavLink = {
    to: string;
    label: string;
    iconName: string;
};

const NAV_MAIN: NavLink[] = [
    { to: '/', label: 'Dashboard', iconName: 'layout-dashboard' },
    { to: '/accounts', label: 'Accounts', iconName: 'wallet' },
    { to: '/journal', label: 'Journal', iconName: 'book-open' },
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
            <div className="eyebrow px-3 pt-3 pb-[6px]">{title}</div>
            {items.map(item => {
                const isActive =
                    item.to === '/' ? currentPath === '/' : currentPath.startsWith(item.to);
                return (
                    <Link
                        key={item.to}
                        to={item.to}
                        className={[
                            'flex items-center gap-3 px-3 py-[9px] rounded-sm select-none',
                            'text-[13.5px] font-medium',
                            'transition-[background,color] duration-fast',
                            isActive
                                ? 'bg-brand-primary-soft text-brand-primary'
                                : 'text-fg-2 hover:bg-surface-2 hover:text-fg-1',
                        ].join(' ')}
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

function lastFourIdentifier(account: Account): string | null {
    const raw = account.bankAccount?.iban ?? account.bankAccount?.accountNumber ?? null;
    if (!raw) return null;
    const compact = raw.replace(/\s+/g, '');
    return compact.length <= 4 ? compact : `· ${compact.slice(-4)}`;
}

function AccountRow({ account }: { account: Account }) {
    const visual = visualHintFor(account.type, account.id);
    const tail = lastFourIdentifier(account);
    const isNegative = account.balance.amount < 0;
    return (
        <div className="flex items-center gap-3 px-3 py-2 rounded-sm">
            <span
                className="shrink-0 inline-flex items-center justify-center w-6 h-6 rounded-sm"
                style={{
                    background: `color-mix(in srgb, ${visual.accentColor} 12%, transparent)`,
                    color: visual.accentColor,
                }}
            >
                <Icon name={visual.iconName} size={14} strokeWidth={1.75} />
            </span>
            <div className="flex-1 min-w-0 flex flex-col leading-tight">
                <span className="truncate text-[13px] text-fg-1">{account.name}</span>
                {tail && <span className="text-[11px] text-fg-3 truncate">{tail}</span>}
            </div>
            <span
                className={[
                    'shrink-0 text-[12px] tabular-nums',
                    isNegative ? 'text-danger' : 'text-fg-2',
                ].join(' ')}
            >
                {formatMoney(account.balance.amount, account.balance.currencyCode, {
                    decimals: false,
                })}
            </span>
        </div>
    );
}

function AccountSection({ title, accounts }: { title: string; accounts: Account[] }) {
    if (accounts.length === 0) return null;
    return (
        <div className="flex flex-col gap-[2px]">
            <div className="eyebrow px-3 pt-3 pb-[6px]">{title}</div>
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
                <div className="eyebrow px-3 pt-3 pb-[6px]">Accounts</div>
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
                <div className="eyebrow px-3 pt-3 pb-[6px]">Accounts</div>
                <div className="px-3 py-2">
                    <ErrorState message="Couldn't load accounts." onRetry={() => refetch()} />
                </div>
            </div>
        );
    }

    const ledgerAccounts = data.filter(isLedgerAccount);
    const categoryAccounts = data.filter(isCategoryAccount);

    if (ledgerAccounts.length === 0 && categoryAccounts.length === 0) {
        return (
            <div className="flex flex-col gap-[2px]">
                <div className="eyebrow px-3 pt-3 pb-[6px]">Accounts</div>
                <div className="px-3 py-2 text-[12px] text-fg-3">No accounts yet.</div>
            </div>
        );
    }

    return (
        <>
            <AccountSection title="Accounts" accounts={ledgerAccounts} />
            <AccountSection title="Categories" accounts={categoryAccounts} />
        </>
    );
}

export function Sidebar() {
    const pathname = useRouterState({ select: s => s.location.pathname });

    return (
        <aside className="w-60 shrink-0 h-screen sticky top-0 flex flex-col gap-5 px-4 py-6 border-r border-border-soft bg-black/20 backdrop-blur-[20px]">
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

            <div className="mt-auto flex items-center gap-[10px] p-[10px] rounded-sm bg-surface-2">
                <div className="w-8 h-8 rounded-full bg-brand-primary-soft text-brand-primary flex items-center justify-center text-[12px] font-semibold">
                    MR
                </div>
                <div className="flex-1 flex flex-col leading-tight min-w-0">
                    <span className="text-[13px] font-medium text-fg-1 truncate">Maya Rivera</span>
                    <span className="text-[14px] text-fg-3 truncate">maya@balance.app</span>
                </div>
                <Icon name="chevron-right" size={16} className="text-fg-3" />
            </div>
        </aside>
    );
}
