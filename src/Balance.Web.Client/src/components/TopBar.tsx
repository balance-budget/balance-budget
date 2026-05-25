import { Icon } from './Icon';

type TopBarProps = {
    title: string;
    period?: string;
};

// TODO: search input + notifications button are visual scaffolding; wire to real handlers
// once those features land.
export function TopBar({ title, period }: TopBarProps) {
    return (
        <header className="min-h-[72px] px-8 py-4 flex items-center gap-4 border-b border-border-soft">
            <div className="flex flex-col gap-[2px] min-w-0">
                <h1 className="text-[22px] font-semibold whitespace-nowrap">{title}</h1>
                {period ? (
                    <span className="text-14 text-fg-3 whitespace-nowrap">{period}</span>
                ) : null}
            </div>
            <div className="ml-auto flex items-center gap-[10px]">
                <label className="w-[240px] h-9 px-[14px] flex items-center gap-2 rounded-sm bg-surface-2 border border-border-soft text-fg-3 text-14">
                    <Icon name="search" size={16} strokeWidth={1.75} />
                    <input
                        className="flex-1 min-w-0 bg-transparent border-0 outline-none text-fg-1 placeholder:text-fg-3"
                        placeholder="Search journal"
                    />
                </label>
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
