import { type Account } from '../api/accounts';
import { cx } from '../lib/cx';
import { visualHintFor } from '../lib/visualHints';
import { Icon } from './Icon';

type Size = 'sm' | 'md';

const BOX_CLASS: Record<Size, string> = {
    sm: 'w-6 h-6 rounded-sm',
    md: 'w-9 h-9 rounded-md',
};

const ICON_SIZE: Record<Size, number> = {
    sm: 14,
    md: 16,
};

const STROKE_WIDTH: Record<Size, number> = {
    sm: 1.75,
    md: 2,
};

type AccountAvatarProps = {
    account: Account;
    size?: Size;
    className?: string;
};

/**
 * Tinted square containing the per-AccountType icon. The background and
 * foreground are derived from visualHintFor — kept in one component so the
 * Sidebar and Dashboard rows can't drift apart visually.
 */
export function AccountAvatar({ account, size = 'sm', className }: AccountAvatarProps) {
    const visual = visualHintFor(account.type);
    return (
        <span
            className={cx(
                'shrink-0 inline-flex items-center justify-center',
                BOX_CLASS[size],
                className,
            )}
            style={{
                background: `color-mix(in srgb, ${visual.accentColor} 12%, transparent)`,
                color: visual.accentColor,
            }}
        >
            <Icon name={visual.iconName} size={ICON_SIZE[size]} strokeWidth={STROKE_WIDTH[size]} />
        </span>
    );
}
