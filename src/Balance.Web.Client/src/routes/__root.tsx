import { useEffect, useState } from 'react';
import { createRootRoute, Outlet, useNavigate, useRouterState } from '@tanstack/react-router';
import { useCurrentUser } from '../api/auth';
import { Launcher } from '../components/Launcher';
import { Sidebar } from '../components/Sidebar';
import { TopBar } from '../components/TopBar';

export const Route = createRootRoute({
    component: function RootLayout() {
        const navigate = useNavigate();
        const pathname = useRouterState({ select: s => s.location.pathname });
        const title = useRouterState({
            select: s => s.matches.at(-1)?.staticData.title ?? 'Balance',
        });

        const isAuthRoute = pathname === '/login' || pathname === '/setup';
        const currentUserQuery = useCurrentUser();

        useEffect(() => {
            if (currentUserQuery.isLoading) return;
            if (isAuthRoute) return;
            if (currentUserQuery.data === null) {
                void navigate({
                    to: '/login',
                    search: { returnTo: pathname },
                    replace: true,
                });
            }
        }, [currentUserQuery.isLoading, currentUserQuery.data, isAuthRoute, navigate, pathname]);

        const [drawerOpen, setDrawerOpen] = useState(false);
        const [launcherOpen, setLauncherOpen] = useState(false);

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

        // Cmd-K / Ctrl-K opens the launcher modal. Bound globally so it works
        // regardless of which screen the user is on.
        useEffect(() => {
            function onKeyDown(e: KeyboardEvent) {
                if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === 'k') {
                    e.preventDefault();
                    setLauncherOpen(true);
                }
            }
            window.addEventListener('keydown', onKeyDown);
            return () => {
                window.removeEventListener('keydown', onKeyDown);
            };
        }, []);

        if (isAuthRoute) {
            return (
                <div className="min-h-screen flex items-center justify-center px-4">
                    <Outlet />
                </div>
            );
        }

        if (currentUserQuery.isLoading || currentUserQuery.data === null) {
            return (
                <div className="min-h-screen flex items-center justify-center text-[13px] text-fg-3">
                    Loading…
                </div>
            );
        }

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
                        onSearchClick={() => {
                            setLauncherOpen(true);
                        }}
                    />
                    <div className="flex-1 min-h-0 overflow-y-auto px-4 pt-4 pb-6 md:px-8 md:pt-6 md:pb-10 flex flex-col gap-[18px]">
                        <Outlet />
                    </div>
                </main>
                <Launcher
                    open={launcherOpen}
                    onClose={() => {
                        setLauncherOpen(false);
                    }}
                />
            </div>
        );
    },
});
