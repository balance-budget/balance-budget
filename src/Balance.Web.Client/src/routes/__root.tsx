import { useEffect, useState } from 'react';
import { Trans } from '@lingui/react/macro';
import { createRootRoute, Outlet, useNavigate, useRouterState } from '@tanstack/react-router';
import { useCurrentUser } from '../api/auth';
import { i18n } from '../i18n/i18n';
import { AppShell } from '../components/AppShell';
import { Launcher } from '../components/Launcher';
import { PageHeaderProvider } from '../components/PageHeader';
import { Sidebar } from '../components/Sidebar';

export const Route = createRootRoute({
    component: function RootLayout() {
        const navigate = useNavigate();
        const pathname = useRouterState({ select: s => s.location.pathname });
        const title = useRouterState({
            select: s => {
                const descriptor = s.matches.at(-1)?.staticData.title;
                return descriptor ? i18n._(descriptor) : 'Balance';
            },
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
                <div className="min-h-[100dvh] flex items-center justify-center px-4">
                    <Outlet />
                </div>
            );
        }

        if (currentUserQuery.isLoading || currentUserQuery.data === null) {
            return (
                <div className="min-h-screen flex items-center justify-center text-sm text-fg-3">
                    <Trans>Loading…</Trans>
                </div>
            );
        }

        return (
            <PageHeaderProvider>
                <div className="flex min-h-screen">
                    <Sidebar
                        open={drawerOpen}
                        onClose={() => {
                            setDrawerOpen(false);
                        }}
                    />
                    <AppShell
                        fallbackTitle={title}
                        onMenuClick={() => {
                            setDrawerOpen(true);
                        }}
                        onSearchClick={() => {
                            setLauncherOpen(true);
                        }}
                    />
                    <Launcher
                        open={launcherOpen}
                        onClose={() => {
                            setLauncherOpen(false);
                        }}
                    />
                </div>
            </PageHeaderProvider>
        );
    },
});
