import { useEffect, useState, type ReactNode } from 'react';
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

const EXPANDED_STORAGE_KEY = 'balance.sidebar.expanded-accounts';

function loadExpandedIds(): Set<string> {
    try {
        const raw = localStorage.getItem(EXPANDED_STORAGE_KEY);
        if (!raw) return new Set();
        const parsed: unknown = JSON.parse(raw);
        if (!Array.isArray(parsed)) return new Set();
        return new Set(parsed.filter((x): x is string => typeof x === 'string'));
    } catch {
        return new Set();
    }
}

/** Maps a parent id to its children, sorted by code then name; the `null` key
 *  holds the roots. Mirrors the Accounts screen's ordering. */
function buildChildrenMap(accounts: Account[]): Map<string | null, Account[]> {
    const map = new Map<string | null, Account[]>();
    for (const a of accounts) {
        const bucket = map.get(a.parentId) ?? [];
        bucket.push(a);
        map.set(a.parentId, bucket);
    }
    const sort = (a: Account, b: Account) =>
        a.code.localeCompare(b.code, undefined, { numeric: true }) || a.name.localeCompare(b.name);
    for (const bucket of map.values()) bucket.sort(sort);
    return map;
}

/** The active-account id encoded in the current path (`/accounts/<id>`), or null. */
function matchActiveAccountId(pathname: string): string | null {
    const match = /^\/accounts\/([^/]+)/.exec(pathname);
    return match?.[1] ?? null;
}

function AccountTreeNode({
    account,
    childrenByParent,
    expandedIds,
    onToggle,
}: {
    account: Account;
    childrenByParent: Map<string | null, Account[]>;
    expandedIds: Set<string>;
    onToggle: (id: string) => void;
}) {
    const catalog = useCurrencyCatalog();
    const identifier = accountIdentifier(account);
    const isNegative = account.balance.amount < 0;
    // Income/Expense are category accounts: their "balance" is the lifetime
    // accumulated total, which isn't a meaningful figure in personal finance.
    // Only ledger (Asset/Liability) accounts have a balance worth showing here.
    const showBalance = isLedgerAccount(account);
    const children = childrenByParent.get(account.id) ?? [];
    const hasChildren = children.length > 0;
    const expanded = expandedIds.has(account.id);

    return (
        <div className="flex flex-col gap-[2px]">
            <div className="flex items-center gap-1">
                <Link
                    to="/accounts/$id"
                    params={{ id: account.id }}
                    search={{ page: 1, q: '' }}
                    className="flex-1 min-w-0 flex items-center gap-3 px-2 py-2 rounded-sm text-fg-1 hover:bg-surface-2 transition-colors"
                    activeProps={{ className: 'bg-brand-primary-soft text-brand-primary' }}
                >
                    <AccountAvatar account={account} />
                    <div className="flex-1 min-w-0 flex flex-col leading-tight">
                        <span className="truncate text-13">{account.name}</span>
                        {identifier && (
                            <span className="text-11 text-fg-3 truncate tabular">{identifier}</span>
                        )}
                    </div>
                    {showBalance && (
                        <span
                            className={cx(
                                'shrink-0 text-12 tabular-nums',
                                isNegative ? 'text-danger' : 'text-fg-2',
                            )}
                        >
                            {formatMoney(
                                account.balance.amount,
                                account.balance.currencyCode,
                                catalog,
                                { decimals: false },
                            )}
                        </span>
                    )}
                </Link>
                {hasChildren ? (
                    <button
                        type="button"
                        onClick={() => {
                            onToggle(account.id);
                        }}
                        aria-label={expanded ? 'Collapse' : 'Expand'}
                        aria-expanded={expanded}
                        className="shrink-0 p-1 rounded-sm text-fg-3 hover:text-fg-1 hover:bg-surface-2"
                    >
                        <Icon
                            name="chevron-right"
                            size={14}
                            className={cx(
                                'transition-transform duration-fast',
                                expanded && 'rotate-90',
                            )}
                        />
                    </button>
                ) : (
                    // Reserve the toggle slot so balances right-align identically
                    // whether or not the row has an expand chevron.
                    <span className="shrink-0 w-[22px]" aria-hidden="true" />
                )}
            </div>
            {hasChildren && expanded && (
                // No indentation — the chart-of-accounts can nest several levels
                // deep and the sidebar is narrow. Instead, each expanded group sits
                // on a translucent shade that compounds with depth, so nested
                // subtrees read progressively brighter without eating width.
                <div className="flex flex-col gap-[2px] rounded-md bg-white/[0.03] py-[2px]">
                    {children.map(child => (
                        <AccountTreeNode
                            key={child.id}
                            account={child}
                            childrenByParent={childrenByParent}
                            expandedIds={expandedIds}
                            onToggle={onToggle}
                        />
                    ))}
                </div>
            )}
        </div>
    );
}

function AccountTreeSection({
    title,
    roots,
    childrenByParent,
    expandedIds,
    onToggle,
}: {
    title: string;
    roots: Account[];
    childrenByParent: Map<string | null, Account[]>;
    expandedIds: Set<string>;
    onToggle: (id: string) => void;
}) {
    if (roots.length === 0) return null;
    return (
        <div className="flex flex-col gap-[2px]">
            <SectionLabel>{title}</SectionLabel>
            {roots.map(root => (
                <AccountTreeNode
                    key={root.id}
                    account={root}
                    childrenByParent={childrenByParent}
                    expandedIds={expandedIds}
                    onToggle={onToggle}
                />
            ))}
        </div>
    );
}

function AccountsGroup() {
    const { data, isPending, isError, refetch } = useAccounts();
    const pathname = useRouterState({ select: s => s.location.pathname });
    const [expandedIds, setExpandedIds] = useState<Set<string>>(loadExpandedIds);

    useEffect(() => {
        try {
            localStorage.setItem(EXPANDED_STORAGE_KEY, JSON.stringify([...expandedIds]));
        } catch {
            // Persistence is best-effort; ignore quota/availability errors.
        }
    }, [expandedIds]);

    const toggle = (id: string) => {
        setExpandedIds(prev => {
            const next = new Set(prev);
            if (next.has(id)) next.delete(id);
            else next.add(id);
            return next;
        });
    };

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

    if (data.length === 0) {
        return (
            <div className="flex flex-col gap-[2px]">
                <SectionLabel>Accounts</SectionLabel>
                <div className="px-3 py-2 text-12 text-fg-3">No accounts yet.</div>
            </div>
        );
    }

    const childrenByParent = buildChildrenMap(data);
    const ledgerRoots = data.filter(a => a.parentId === null && isLedgerAccount(a));
    const incomeRoots = data.filter(a => a.parentId === null && a.type === 'Income');
    const expenseRoots = data.filter(a => a.parentId === null && a.type === 'Expense');

    // Auto-expand the ancestors of the account being viewed so it's never hidden
    // behind a collapsed parent — unioned with the user's persisted expansions.
    const parentOf = new Map<string, string | null>(data.map(a => [a.id, a.parentId]));
    const effectiveExpanded = new Set(expandedIds);
    let cursor = parentOf.get(matchActiveAccountId(pathname) ?? '') ?? null;
    while (cursor) {
        effectiveExpanded.add(cursor);
        cursor = parentOf.get(cursor) ?? null;
    }

    return (
        <>
            <AccountTreeSection
                title="Accounts"
                roots={ledgerRoots}
                childrenByParent={childrenByParent}
                expandedIds={effectiveExpanded}
                onToggle={toggle}
            />
            <AccountTreeSection
                title="Income"
                roots={incomeRoots}
                childrenByParent={childrenByParent}
                expandedIds={effectiveExpanded}
                onToggle={toggle}
            />
            <AccountTreeSection
                title="Expenses"
                roots={expenseRoots}
                childrenByParent={childrenByParent}
                expandedIds={effectiveExpanded}
                onToggle={toggle}
            />
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
            <div className="w-8 h-8 rounded-full bg-brand-primary-soft text-brand-primary flex items-center justify-center text-12 font-semibold">
                {initials(displayName)}
            </div>
            <div className="flex-1 flex flex-col leading-tight min-w-0">
                <span className="text-13 font-medium text-fg-1 truncate">{displayName}</span>
                <span className="text-14 text-fg-3 truncate">{email}</span>
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
                    'w-64 shrink-0 h-screen flex flex-col gap-5 px-4 py-6 border-r border-border-soft bg-surface-1 backdrop-blur-card',
                    'fixed top-0 left-0 z-40 transition-transform duration-base',
                    open ? 'translate-x-0' : '-translate-x-full',
                    'md:sticky md:top-0 md:left-auto md:z-auto md:translate-x-0 md:transition-none',
                )}
            >
                <div className="flex items-center gap-[10px] px-[10px] py-1">
                    <img src={logo} alt="" className="w-8 h-8 rounded-[6px]" />
                    <span className="text-18 font-normal tracking-[-0.01em]">
                        Balance<span className="text-brand-primary">.</span>
                    </span>
                </div>

                {/* `-mr-4 pr-4` extends the scroll container to the aside's right
                    edge so the scrollbar sits flush, while keeping the content
                    inset where it was. */}
                <nav className="flex flex-col gap-1 overflow-y-auto scrollbar-sleek -mr-4 pr-4">
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
