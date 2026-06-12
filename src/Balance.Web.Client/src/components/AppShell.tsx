import { Outlet } from '@tanstack/react-router';
import { usePageHeaderValue } from './PageHeader';
import { TopBar } from './TopBar';

/** The authenticated shell. Lives inside PageHeaderProvider so it can read the
 *  contextual header a screen sets (a specific title + breadcrumb), falling back
 *  to the route's static title for plain section pages. */
export function AppShell({
    fallbackTitle,
    onMenuClick,
    onSearchClick,
}: {
    fallbackTitle: string;
    onMenuClick: () => void;
    onSearchClick: () => void;
}) {
    const header = usePageHeaderValue();
    return (
        <main className="flex-1 min-w-0 flex flex-col">
            <TopBar
                title={header?.title ?? fallbackTitle}
                breadcrumb={header?.breadcrumb}
                onMenuClick={onMenuClick}
                onSearchClick={onSearchClick}
            />
            <div className="flex-1 min-h-0 overflow-y-auto px-4 pt-4 pb-6 md:px-8 md:pt-6 md:pb-10 flex flex-col gap-[18px]">
                <Outlet />
            </div>
        </main>
    );
}
