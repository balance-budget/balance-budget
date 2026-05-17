import { createRootRoute, Outlet, useRouterState } from '@tanstack/react-router';
import { Sidebar } from '../components/Sidebar';
import { TopBar } from '../components/TopBar';

const ROUTE_TITLES: Record<string, string> = {
    '/': 'Dashboard',
    '/accounts': 'Accounts',
    '/journal': 'Journal entries',
    '/bank-imports': 'Bank imports',
    '/budgets': 'Budgets',
    '/subscriptions': 'Subscriptions',
    '/piggy-banks': 'Piggy banks',
    '/reports': 'Reports',
    '/settings': 'Settings',
};

export const Route = createRootRoute({
    component: function RootLayout() {
        const pathname = useRouterState({ select: s => s.location.pathname });
        const title = ROUTE_TITLES[pathname] ?? 'Balance';

        return (
            <div className="flex min-h-screen">
                <Sidebar />
                <main className="flex-1 min-w-0 flex flex-col">
                    <TopBar title={title} />
                    <div className="flex-1 min-h-0 overflow-y-auto px-8 pt-6 pb-10 flex flex-col gap-[18px]">
                        <Outlet />
                    </div>
                </main>
            </div>
        );
    },
});
