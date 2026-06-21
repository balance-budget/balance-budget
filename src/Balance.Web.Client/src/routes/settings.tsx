import type { MessageDescriptor } from '@lingui/core';
import { msg } from '@lingui/core/macro';
import { createFileRoute, Link, Outlet, useRouterState } from '@tanstack/react-router';
import { i18n } from '../i18n/i18n';
import { cx } from '../lib/cx';

export const Route = createFileRoute('/settings')({
    component: SettingsLayout,
    staticData: { title: msg`Settings` },
});

const SUB_NAV: {
    to:
        | '/settings/bank-accounts'
        | '/settings/currencies'
        | '/settings/users'
        | '/settings/tokens'
        | '/settings/preferences';
    label: MessageDescriptor;
}[] = [
    { to: '/settings/bank-accounts', label: msg`Bank accounts` },
    { to: '/settings/currencies', label: msg`Currencies` },
    { to: '/settings/users', label: msg`Users` },
    { to: '/settings/tokens', label: msg`API tokens` },
    { to: '/settings/preferences', label: msg`Preferences` },
];

// eslint-disable-next-line react-refresh/only-export-components -- TanStack file routes export `Route` (metadata) alongside the component; that's the documented pattern.
function SettingsLayout() {
    const pathname = useRouterState({ select: s => s.location.pathname });
    return (
        <div className="flex flex-col md:flex-row gap-6 min-h-0">
            <nav className="w-full md:w-48 md:shrink-0 flex flex-col gap-1">
                {SUB_NAV.map(item => {
                    const isActive = pathname.startsWith(item.to);
                    return (
                        <Link
                            key={item.to}
                            to={item.to}
                            className={cx(
                                'px-3 py-[7px] rounded-lg text-sm font-medium transition-colors',
                                isActive
                                    ? 'bg-brand-primary-soft text-brand-primary'
                                    : 'text-fg-2 hover:bg-surface-2 hover:text-fg-1',
                            )}
                        >
                            {i18n._(item.label)}
                        </Link>
                    );
                })}
            </nav>
            <div className="flex-1 min-w-0 flex flex-col gap-[18px]">
                <Outlet />
            </div>
        </div>
    );
}
