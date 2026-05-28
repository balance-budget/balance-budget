import { useEffect, useState } from 'react';
import { createRootRoute, Outlet, useRouterState } from '@tanstack/react-router';
import { Sidebar } from '../components/Sidebar';
import { TopBar } from '../components/TopBar';

export const Route = createRootRoute({
    component: function RootLayout() {
        // The deepest matched route owns the page title via its staticData.
        const title = useRouterState({
            select: s => s.matches.at(-1)?.staticData.title ?? 'Balance',
        });
        const pathname = useRouterState({ select: s => s.location.pathname });

        const [drawerOpen, setDrawerOpen] = useState(false);

        useEffect(() => {
            setDrawerOpen(false);
        }, [pathname]);

        useEffect(() => {
            if (!drawerOpen) return;
            function onKeyDown(e: KeyboardEvent) {
                if (e.key === 'Escape') setDrawerOpen(false);
            }
            window.addEventListener('keydown', onKeyDown);
            return () => {
                window.removeEventListener('keydown', onKeyDown);
            };
        }, [drawerOpen]);

        return (
            <div className="flex min-h-screen">
                <Sidebar
                    open={drawerOpen}
                    onClose={() => {
                        setDrawerOpen(false);
                    }}
                />
                <main className="flex-1 min-w-0 flex flex-col">
                    <TopBar
                        title={title}
                        onMenuClick={() => {
                            setDrawerOpen(true);
                        }}
                    />
                    <div className="flex-1 min-h-0 overflow-y-auto px-4 pt-4 pb-6 md:px-8 md:pt-6 md:pb-10 flex flex-col gap-[18px]">
                        <Outlet />
                    </div>
                </main>
            </div>
        );
    },
});
