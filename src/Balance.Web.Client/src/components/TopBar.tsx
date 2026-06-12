import { Trans, useLingui } from '@lingui/react/macro';
import { Button } from 'react-aria-components';
import { Icon } from './Icon';
import { composeTailwindRenderProps } from './ui/compose';

type TopBarProps = {
    title: string;
    period?: string;
    onMenuClick: () => void;
    onSearchClick: () => void;
};

const isMac = typeof navigator !== 'undefined' && /Mac|iPhone|iPad|iPod/.test(navigator.userAgent);

export function TopBar({ title, period, onMenuClick, onSearchClick }: TopBarProps) {
    const { t } = useLingui();
    return (
        <header className="min-h-[56px] px-4 py-3 md:min-h-[72px] md:px-8 md:py-4 flex items-center gap-3 md:gap-4 border-b border-border-soft">
            <TopBarButton
                onPress={onMenuClick}
                aria-label={t`Open navigation`}
                className="md:hidden -ml-1 p-2 rounded-lg text-fg-2 data-[hovered]:text-fg-1 data-[hovered]:bg-surface-2"
            >
                <Icon name="menu" size={20} strokeWidth={2} />
            </TopBarButton>
            <div className="flex flex-col gap-[2px] min-w-0">
                <h1 className="text-lg md:text-xl font-semibold truncate">{title}</h1>
                {period ? (
                    <span className="text-sm text-fg-3 whitespace-nowrap">{period}</span>
                ) : null}
            </div>
            <div className="ml-auto flex items-center gap-[10px]">
                <TopBarButton
                    onPress={onSearchClick}
                    aria-label={t`Search`}
                    className="hidden md:flex w-[240px] h-9 px-[14px] items-center gap-2 rounded-lg bg-surface-2 border border-border-soft text-fg-3 text-sm data-[hovered]:bg-surface-3 data-[hovered]:text-fg-1"
                >
                    <Icon name="search" size={16} strokeWidth={1.75} />
                    <span className="flex-1 min-w-0 text-left truncate">
                        <Trans>Search…</Trans>
                    </span>
                    <kbd className="px-1.5 py-0.5 rounded bg-bg-1 text-xs tabular-nums border border-border-soft">
                        {isMac ? '⌘K' : 'Ctrl K'}
                    </kbd>
                </TopBarButton>
                <TopBarButton
                    onPress={onSearchClick}
                    aria-label={t`Search`}
                    className="md:hidden w-9 h-9 rounded-lg bg-surface-2 border border-border-soft flex items-center justify-center text-fg-2 data-[hovered]:bg-surface-3 data-[hovered]:text-fg-1"
                >
                    <Icon name="search" size={18} strokeWidth={1.75} />
                </TopBarButton>
            </div>
        </header>
    );
}

function TopBarButton(props: React.ComponentProps<typeof Button>) {
    return (
        <Button
            {...props}
            className={composeTailwindRenderProps(
                props.className,
                'outline-none cursor-pointer data-[focus-visible]:ring-1 data-[focus-visible]:ring-brand-primary',
            )}
        />
    );
}
