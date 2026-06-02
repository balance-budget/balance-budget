import { Icon } from './Icon';

type TopBarProps = {
    title: string;
    period?: string;
    onMenuClick: () => void;
    onSearchClick: () => void;
};

const isMac = typeof navigator !== 'undefined' && /Mac|iPhone|iPad|iPod/.test(navigator.platform);

export function TopBar({ title, period, onMenuClick, onSearchClick }: TopBarProps) {
    return (
        <header className="min-h-[56px] px-4 py-3 md:min-h-[72px] md:px-8 md:py-4 flex items-center gap-3 md:gap-4 border-b border-border-soft">
            <button
                type="button"
                onClick={onMenuClick}
                aria-label="Open navigation"
                className="md:hidden -ml-1 p-2 rounded-sm text-fg-2 hover:text-fg-1 hover:bg-surface-2"
            >
                <Icon name="menu" size={20} strokeWidth={2} />
            </button>
            <div className="flex flex-col gap-[2px] min-w-0">
                <h1 className="text-[18px] md:text-[22px] font-semibold truncate">{title}</h1>
                {period ? (
                    <span className="text-14 text-fg-3 whitespace-nowrap">{period}</span>
                ) : null}
            </div>
            <div className="ml-auto flex items-center gap-[10px]">
                <button
                    type="button"
                    onClick={onSearchClick}
                    aria-label="Search"
                    title="Search (⌘K / Ctrl-K)"
                    className="hidden md:flex w-[240px] h-9 px-[14px] items-center gap-2 rounded-sm bg-surface-2 border border-border-soft text-fg-3 text-14 hover:bg-surface-3 hover:text-fg-1"
                >
                    <Icon name="search" size={16} strokeWidth={1.75} />
                    <span className="flex-1 min-w-0 text-left truncate">Search…</span>
                    <kbd className="px-1.5 py-0.5 rounded bg-bg-1 text-[11px] tabular border border-border-soft">
                        {isMac ? '⌘K' : 'Ctrl K'}
                    </kbd>
                </button>
                <button
                    type="button"
                    onClick={onSearchClick}
                    aria-label="Search"
                    className="md:hidden w-9 h-9 rounded-sm bg-surface-2 border border-border-soft flex items-center justify-center text-fg-2 hover:bg-surface-3 hover:text-fg-1"
                >
                    <Icon name="search" size={18} strokeWidth={1.75} />
                </button>
                <button
                    type="button"
                    title="Notifications"
                    className="w-9 h-9 rounded-sm bg-surface-2 border border-border-soft flex items-center justify-center text-fg-2 transition-colors duration-fast hover:bg-surface-3 hover:text-fg-1"
                >
                    <Icon name="bell" size={18} strokeWidth={1.75} />
                </button>
            </div>
        </header>
    );
}
