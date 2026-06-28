import { msg } from '@lingui/core/macro';
import { Trans, useLingui } from '@lingui/react/macro';
import { type MessageDescriptor } from '@lingui/core';
import { createContext, useContext, useEffect, useState, type ReactNode } from 'react';
import { Button } from 'react-aria-components';
import { Link, useNavigate, useRouterState } from '@tanstack/react-router';
import logo from '../assets/logo.svg';
import { Icon, type IconName } from './Icon';
import { accountIdentifier, useAccounts, type Account } from '../api/accounts';
import { useCurrentUser, useLogout } from '../api/auth';
import { useCurrencyCatalog } from '../api/currencies';
import { AccountAvatar } from './AccountAvatar';
import { AccountTreeSections, type AccountRowContext } from './AccountTree';
import { TreeExpandButton } from './ui/Tree';
import { Skeleton } from './Skeleton';
import { ErrorState } from './ErrorState';
import { cx } from '../lib/cx';
import { isLedgerAccount, type AccountId, type AccountType } from '../lib/domain';
import { formatMoney } from '../lib/money';

const SIDEBAR_TYPE_ORDER: AccountType[] = ['Asset', 'Liability', 'Income', 'Expense'];
const SIDEBAR_TYPE_LABELS: Record<AccountType, MessageDescriptor> = {
    Asset: msg`Assets`,
    Liability: msg`Liabilities`,
    Equity: msg`Equity`,
    Income: msg`Income`,
    Expense: msg`Expenses`,
};

function SectionLabel({ children }: { children: ReactNode }) {
    return (
        <div className="text-xs font-medium text-fg-3 tracking-widest uppercase px-3 pt-3 pb-[6px]">
            {children}
        </div>
    );
}

type NavLink = {
    to: string;
    label: MessageDescriptor;
    iconName: IconName;
};

// Loans is the only built "Plan" surface, so it sits in the main nav after
// Outlook rather than alone under a one-item group. Budgets / Subscriptions /
// Piggy banks are deliberately omitted: they're unbuilt, and their framing
// (budget/envelope, "piggy bank", a "subscriptions" tab) is language the domain
// avoids — recurring items live inside Outlook. Add them under glossary-safe
// names once they actually ship.
const NAV_MAIN: NavLink[] = [
    { to: '/', label: msg`Dashboard`, iconName: 'layout-dashboard' },
    { to: '/activity', label: msg`Activity`, iconName: 'book-open' },
    { to: '/reports', label: msg`Insights`, iconName: 'line-chart' },
    { to: '/outlook', label: msg`Outlook`, iconName: 'binoculars' },
    { to: '/loans', label: msg`Loans`, iconName: 'landmark' },
];

const NAV_OTHER: NavLink[] = [
    { to: '/accounts', label: msg`Accounts`, iconName: 'wallet' },
    { to: '/counterparties', label: msg`Counterparties`, iconName: 'user' },
    { to: '/bank-transactions', label: msg`Bank transactions`, iconName: 'inbox' },
    { to: '/bank-imports', label: msg`Bank imports`, iconName: 'download' },
    { to: '/settings', label: msg`Settings`, iconName: 'settings' },
];

function NavGroup({
    title,
    items,
    currentPath,
}: {
    title?: ReactNode;
    items: NavLink[];
    currentPath: string;
}) {
    const { i18n } = useLingui();
    return (
        <div className="flex flex-col gap-[2px]">
            {title && <SectionLabel>{title}</SectionLabel>}
            {items.map(item => {
                const isActive =
                    item.to === '/' ? currentPath === '/' : currentPath.startsWith(item.to);
                return (
                    <Link
                        key={item.to}
                        to={item.to}
                        className={cx(
                            'flex items-center gap-3 px-3 py-[9px] rounded-lg select-none',
                            'text-sm font-medium transition-[background,color] duration-120',
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
                        <span>{i18n._(item.label)}</span>
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

/** The active-account id encoded in the current path (`/accounts/<id>`), or null. */
function matchActiveAccountId(pathname: string): string | null {
    const match = /^\/accounts\/([^/]+)/.exec(pathname);
    return match?.[1] ?? null;
}

// The active account is published via context, not a captured prop: RAC's Tree
// caches each rendered row by item reference (useCachedChildren), and the
// account objects are reference-stable across navigations, so a captured
// `active` flag would never update. Context propagation bypasses that cache.
const ActiveAccountContext = createContext<string | null>(null);

function SidebarAccountRow({ account, ctx }: { account: Account; ctx: AccountRowContext }) {
    const { t } = useLingui();
    const catalog = useCurrencyCatalog();
    const active = useContext(ActiveAccountContext) === String(account.id);
    const identifier = accountIdentifier(account);
    const isNegative = account.balance.amount < 0;
    // Income/Expense are category accounts: their "balance" is the lifetime
    // accumulated total, which isn't a meaningful figure in personal finance.
    // Only ledger (Asset/Liability) accounts have a balance worth showing here.
    const showBalance = isLedgerAccount(account);
    // No indentation — the chart-of-accounts nests several levels deep and the
    // sidebar is narrow. Instead each nested row sits on a translucent shade that
    // compounds with depth, matching the old nested `bg-surface-2` containers:
    // each level layers another surface-2 (alpha 0.04), so the effective alpha is
    // 1 - 0.96^(level-1). The base channels flip black→white in dark mode via
    // --surface-lift-rgb. The first two levels get a rounded pill; deeper rows
    // are square.
    const nested = ctx.level > 1;
    const roundedPill = ctx.level <= 2;
    const shade = nested
        ? {
              backgroundColor: `rgb(var(--surface-lift-rgb) / ${String(
                  1 - Math.pow(1 - 0.04, ctx.level - 1),
              )})`,
          }
        : undefined;
    return (
        <div className="relative" style={shade}>
            <div
                className={cx(
                    'relative flex items-center gap-3 pl-2 pr-8 py-2 cursor-pointer transition-colors',
                    roundedPill && 'rounded-lg',
                    active
                        ? 'bg-brand-primary-soft text-brand-primary'
                        : 'text-fg-1 group-data-[hovered]:bg-surface-2 group-data-[focus-visible]:bg-surface-2',
                )}
            >
                <AccountAvatar account={account} />
                <div className="flex-1 min-w-0 flex flex-col leading-tight">
                    <span className="truncate text-sm">{account.name}</span>
                    {identifier && (
                        <span className="text-xs text-fg-3 truncate tabular-nums">
                            {identifier}
                        </span>
                    )}
                </div>
                {showBalance && (
                    <span
                        className={cx(
                            'shrink-0 text-xs tabular-nums',
                            isNegative ? 'text-danger' : 'text-fg-2',
                        )}
                    >
                        {formatMoney(
                            account.balance.amount,
                            account.balance.currencyCode,
                            catalog,
                            {
                                decimals: false,
                            },
                        )}
                    </span>
                )}
                {ctx.hasChildren && (
                    // Floated over the row's reserved right padding (pr-8) so the
                    // active/hover background spans the full row width while the
                    // chevron stays its own clickable target.
                    <TreeExpandButton
                        ariaLabel={ctx.isExpanded ? t`Collapse` : t`Expand`}
                        isExpanded={ctx.isExpanded}
                        className="absolute right-1 top-1/2 -translate-y-1/2"
                    />
                )}
            </div>
        </div>
    );
}

function AccountsGroup() {
    const { t } = useLingui();
    const navigate = useNavigate();
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

    if (isPending) {
        return (
            <div className="flex flex-col gap-[2px]">
                <SectionLabel>
                    <Trans>Accounts</Trans>
                </SectionLabel>
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
                <SectionLabel>
                    <Trans>Accounts</Trans>
                </SectionLabel>
                <div className="px-3 py-2">
                    <ErrorState
                        message={t`Couldn't load accounts.`}
                        onRetry={() => void refetch()}
                    />
                </div>
            </div>
        );
    }

    if (data.length === 0) {
        return (
            <div className="flex flex-col gap-[2px]">
                <SectionLabel>
                    <Trans>Accounts</Trans>
                </SectionLabel>
                <div className="px-3 py-2 text-xs text-fg-3">
                    <Trans>No accounts yet.</Trans>
                </div>
            </div>
        );
    }

    // Auto-expand the ancestors of the account being viewed so it's never hidden
    // behind a collapsed parent — unioned with the user's persisted expansions.
    const activeAccountId = matchActiveAccountId(pathname);
    const parentOf = new Map<string, string | null>(data.map(a => [a.id, a.parentId]));
    const effectiveExpanded = new Set<string>(expandedIds);
    let cursor = parentOf.get(activeAccountId ?? '') ?? null;
    while (cursor) {
        effectiveExpanded.add(cursor);
        cursor = parentOf.get(cursor) ?? null;
    }

    return (
        <ActiveAccountContext.Provider value={activeAccountId}>
            <AccountTreeSections
                accounts={data}
                typeOrder={SIDEBAR_TYPE_ORDER}
                typeLabels={SIDEBAR_TYPE_LABELS}
                expandedKeys={effectiveExpanded}
                onExpandedChange={keys => {
                    setExpandedIds(new Set([...keys].map(String)));
                }}
                onAction={(key: AccountId) => {
                    void navigate({
                        to: '/accounts/$id',
                        params: { id: key },
                        search: {
                            page: 1,
                            q: '',
                            posted: '',
                            counter: '',
                            from: '',
                            to: '',
                            status: '',
                        },
                    });
                }}
                renderHeading={label => <SectionLabel>{label}</SectionLabel>}
                renderRow={(account, ctx) => <SidebarAccountRow account={account} ctx={ctx} />}
            />
        </ActiveAccountContext.Provider>
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
    const { t } = useLingui();
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
        <div className="mt-auto flex items-center gap-[10px] p-[10px] rounded-lg bg-surface-2">
            <div className="w-8 h-8 rounded-full bg-brand-primary-soft text-brand-primary flex items-center justify-center text-xs font-semibold">
                {initials(displayName)}
            </div>
            <div className="flex-1 flex flex-col leading-tight min-w-0">
                <span className="text-sm font-medium text-fg-1 truncate">{displayName}</span>
                <span className="text-sm text-fg-3 truncate">{email}</span>
            </div>
            <Button
                onPress={() => {
                    void signOut();
                }}
                isDisabled={logout.isPending}
                className="text-fg-3 cursor-pointer outline-none data-[hovered]:text-fg-1 data-[focus-visible]:ring-1 data-[focus-visible]:ring-brand-primary rounded-sm"
                aria-label={t`Sign out`}
            >
                <Icon name="log-out" size={16} />
            </Button>
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
                    'md:hidden fixed inset-0 z-30 bg-surface-overlay backdrop-blur-sm transition-opacity duration-220',
                    open ? 'opacity-100' : 'opacity-0 pointer-events-none',
                )}
            />
            <aside
                className={cx(
                    'w-64 shrink-0 h-screen flex flex-col gap-5 px-4 py-6 border-r border-border-soft bg-surface-1 backdrop-blur-xl',
                    'fixed top-0 left-0 z-40 transition-transform duration-220',
                    open ? 'translate-x-0' : '-translate-x-full',
                    'md:sticky md:top-0 md:left-auto md:z-auto md:translate-x-0 md:transition-none',
                )}
            >
                <div className="flex items-center gap-[10px] px-[10px] py-1">
                    <img src={logo} alt="" className="w-8 h-8 rounded-[6px]" />
                    <span className="text-lg font-normal tracking-[-0.01em]">
                        Balance<span className="text-brand-primary">.</span>
                    </span>
                </div>

                {/* `-mr-4 pr-4` extends the scroll container to the aside's right
                    edge so the scrollbar sits flush, while keeping the content
                    inset where it was. */}
                <nav className="flex flex-col gap-1 overflow-y-auto -mr-4 pr-4">
                    <NavGroup items={NAV_MAIN} currentPath={pathname} />
                    <AccountsGroup />
                    <NavGroup
                        title={<Trans>Manage</Trans>}
                        items={NAV_OTHER}
                        currentPath={pathname}
                    />
                </nav>

                <CurrentUserCard />
            </aside>
        </>
    );
}
