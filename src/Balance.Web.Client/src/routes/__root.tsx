import { createRootRoute, Outlet, useRouterState } from '@tanstack/react-router';
import { Sidebar } from '../components/Sidebar';
import { TopBar } from '../components/TopBar';

export const Route = createRootRoute({
    component: function RootLayout() {
        // The deepest matched route owns the page title via its staticData.
        const title = useRouterState({
            select: s => s.matches.at(-1)?.staticData.title ?? 'Balance',
        });

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
